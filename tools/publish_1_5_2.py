"""Publish the verified Launcher 1.5.2 APK and update the website.

Run from a Windows checkout as: python tools/publish_1_5_2.py
Dry run:                          python tools/publish_1_5_2.py --dry-run

Expected local build: artifacts/release-1.5.2/Launcher-1.5.2.apk
Expected build metadata: artifacts/release-1.5.2/release-info.json
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shlex
import subprocess
import sys

import paramiko

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "release-1.5.2"
APK = OUT / "Launcher-1.5.2.apk"
BUILD_METADATA = OUT / "release-info.json"
# Server publisher is embedded so no separate, outdated remote helper can be uploaded.
REMOTE_PUBLISHER_SOURCE = r'''
"""Publish Launcher 1.5.2 on kevlog.co.za.

Invoked by tools/publish_1_5_2.py on the server as the 'kevlog' user.
Publishes the verified APK, updates download buttons, website notes, release
history and blog text. No migration preconditions or migration instructions.
Previous APK files and their download URLs are not deleted.
"""

from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import re
import secrets
import shutil
import sys

os.environ["KEVLOG_DATA"] = "/var/lib/kevlog"
sys.path.insert(0, "/opt/kevlog/app")
import cms

stage = Path(sys.argv[1]).resolve()
if stage.parent != Path("/tmp") or not stage.name.startswith("kevlog-launcher-update-"):
    raise RuntimeError("Unexpected staging directory")

metadata = json.loads((stage / "releases.json").read_text(encoding="utf-8"))
release = dict(metadata["production"])
if release["version"] != "1.5.2" or int(release["build"]) != 28:
    raise RuntimeError("Expected Launcher 1.5.2, build 28")

source = stage / "Launcher-1.5.2.apk"
if not source.is_file():
    raise FileNotFoundError(source)
with source.open("rb") as apk:
    actual_hash = hashlib.file_digest(apk, "sha256").hexdigest().upper()
if source.stat().st_size != int(release["bytes"]) or actual_hash != str(release["sha256"]).upper():
    raise RuntimeError("Uploaded APK does not match the verified 1.5.2 artifact")

projects = cms.config("projects.json")
project = next((item for item in projects if item.get("slug") == "launcher"), None)
if project is None:
    raise RuntimeError("Launcher project is missing from projects.json")
if any(str(item.get("version")) == "1.5.2" for item in project.get("releases", [])):
    raise RuntimeError("Launcher 1.5.2 is already published; refusing duplicate release")

# Retain all existing files and their IDs. Do not assume the website has a
# particular old APK button or any specific previous release version.
old_buttons = [dict(button) for button in project.get("buttons", [])]
old_ids = sorted({str(button["file_id"]) for button in old_buttons if button.get("file_id")})

# Register the new APK. Write the created ID first so the SSH wrapper can
# remove only this new file during a failed publication.
file_id = secrets.token_hex(16)
(stage / "created.json").write_text(json.dumps([file_id]), encoding="utf-8")
target = cms.PROJECT_FILES / file_id
with source.open("rb") as incoming, target.open("xb") as outgoing:
    shutil.copyfileobj(incoming, outgoing)
target.chmod(0o640)

release["url"] = f"https://kevlog.co.za/downloads/projects/launcher/{file_id}/"
release["notes"] = "https://kevlog.co.za/projects/launcher/#release-launcher-1-5-2"

latest_button = {
    "filename": "Launcher-1.5.2.apk",
    "file_id": file_id,
    "label": "Download Launcher 1.5.2",
    "icon": "file_download",
    "colour": "#e4ff00",
    "text_colour": "#11120f",
    "note": "Latest Android release · Android 7+ · 64-bit",
}

# Keep prior releases and companion downloads intact. Remove the obsolete
# *button* for the old bridge package, but do not delete its file or URL.
# All other Android download buttons become clearly labelled archives.
previous_buttons = []
for original in old_buttons:
    button = dict(original)
    filename = str(button.get("filename") or "")
    if filename.lower().startswith("launcher-bridge-") and filename.lower().endswith(".apk"):
        continue
    if filename.lower().endswith(".apk"):
        match = re.search(r"(\d+\.\d+\.\d+)", filename)
        version = match.group(1) if match else "older version"
        button["label"] = f"Previous Android APK · {version}"
        button["note"] = "Archived version · available for manual download"
    previous_buttons.append(button)

project["buttons"] = [latest_button, *previous_buttons]

# Update the current release note without retaining old migration guidance.
notes = []
release_note_found = False
for original in project.get("notes", []):
    if not isinstance(original, str):
        notes.append(original)
        continue
    if any(phrase in original.lower() for phrase in (
        "signing migration:", "migration bridge", "signing bridge",
        "install 1.5.0 first", "update to 1.5.0 first",
    )):
        continue
    updated, count = re.subn(
        r"Android release:\s*\d+\.\d+\.\d+,\s*build\s*\d+\.",
        "Android release: 1.5.2, build 28.",
        original,
    )
    release_note_found |= count > 0
    notes.append(updated)
if not release_note_found:
    notes.insert(0, "Android release: 1.5.2, build 28.")
project["notes"] = notes

now = datetime.now(timezone.utc)
project.setdefault("releases", []).append({
    "id": "launcher-1-5-2",
    "version": "1.5.2",
    "date": now.date().isoformat(),
    "status": "published",
    "kind": "release",
    "platforms": "Android 7+ · 64-bit",
    "title": "Google Discover and feed options",
    "summary": (
        "Optional Google Discover integration, with RSS sources, saved stories "
        "and existing Launcher preferences retained."
    ),
    "body": (
        "<p>Launcher 1.5.2 adds Google Discover as an optional feed provider. "
        "The Today page can be hidden when Discover is selected, while "
        "existing RSS feeds, saved stories and home layout settings are preserved.</p>"
        "<p>Native Google Discover overlay support depends on the installed Google "
        "app and device compatibility. Where the overlay is unavailable, "
        "Launcher can open the Google app instead.</p>"
        "<p>The Android APK is signed with the existing Launcher release identity. "
        "Earlier APK downloads remain available.</p>"
    ),
    "created_at": now.isoformat(),
    "updated_at": now.isoformat(),
})

# The SSH wrapper has already stopped the site and backed up its database.
post_body = None
with cms.connect() as db:
    db.execute("BEGIN IMMEDIATE")
    db.execute(
        "UPDATE config SET value=? WHERE name=?",
        (json.dumps(projects, ensure_ascii=False), "projects.json"),
    )
    post = db.execute(
        "SELECT id,body FROM posts WHERE slug=?",
        ("building-launcher-for-android",),
    ).fetchone()
    if post:
        post_body = re.sub(
            r"current downloads are Android \d+\.\d+\.\d+",
            "current downloads are Android 1.5.2",
            post["body"],
        )
        db.execute(
            "UPDATE posts SET body=?, updated_at=? WHERE id=?",
            (post_body, now.isoformat(), post["id"]),
        )

cms.rebuild()
client = cms.app.test_client()
with client.head(release["url"].removeprefix("https://kevlog.co.za")) as response:
    if response.status_code != 200 or response.content_length != int(release["bytes"]):
        raise RuntimeError("New 1.5.2 APK URL failed the download verification")
for old_id in old_ids:
    with client.head(f"/downloads/projects/launcher/{old_id}/") as response:
        if response.status_code != 200:
            raise RuntimeError(f"Previous download no longer works: {old_id}")

cms.audit("Published Launcher 1.5.2; updated buttons/history, retained earlier downloads.")
print(json.dumps({
    "releases": {"production": release},
    "old_file_ids": old_ids,
    "created_file_ids": [file_id],
    "content": {"project": project, "postBody": post_body},
}), flush=True)
'''

KEY_DIRECTORY = Path.home() / ".android" / "launcher-release"
ANDROID_BUILD_TOOLS = Path(r"C:\Program Files (x86)\Android\android-sdk\build-tools\37.0.0")


def digest(path: Path) -> str:
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest().upper()


def check_local_release() -> dict:
    for file in [APK, BUILD_METADATA]:
        if not file.is_file():
            raise FileNotFoundError(f"Missing required release file: {file}")

    metadata = json.loads(BUILD_METADATA.read_text(encoding="utf-8-sig"))
    if metadata.get("version") != "1.5.2" or int(metadata.get("build", 0)) != 28:
        raise RuntimeError("release-info.json must describe version 1.5.2, build 28")
    if APK.stat().st_size != metadata.get("bytes") or digest(APK) != str(metadata.get("sha256", "")).upper():
        raise RuntimeError("APK hash/size differs from release-info.json")

    # Older 1.5.x scripts recorded a source snapshot at build time. Verify if present.
    snapshot_path = OUT / "source-sha256.json"
    if snapshot_path.is_file():
        entries = json.loads(snapshot_path.read_text(encoding="utf-8-sig"))
        expected = {e["path"]: e["sha256"].upper() for e in entries}
        actual = {
            path.relative_to(ROOT).as_posix(): digest(path)
            for path in (ROOT / "src").rglob("*")
            if path.is_file() and not {"bin", "obj"}.intersection(path.relative_to(ROOT).parts)
        }
        if expected != actual:
            raise RuntimeError("Source changed since the release build snapshot")
        print("Source snapshot verified.")
    else:
        print("NOTE: No source-sha256.json for 1.5.2; only the signed APK identity is verified.")

    apksigner = ANDROID_BUILD_TOOLS / "apksigner.bat"
    zipalign = ANDROID_BUILD_TOOLS / "zipalign.exe"
    if not apksigner.exists() or not zipalign.exists():
        raise RuntimeError("Android build tools not found; correct ANDROID_BUILD_TOOLS")

    result = subprocess.run(
        [str(apksigner), "verify", "--verbose", "--print-certs", str(APK)],
        capture_output=True, text=True, check=True,
    )
    expected_signer = json.loads(
        (KEY_DIRECTORY / "signing-metadata.json").read_text(encoding="utf-8-sig")
    )["certificateSha256"].upper()
    if expected_signer not in result.stdout.upper():
        raise RuntimeError("APK certificate differs from the registered production signer")
    subprocess.run([str(zipalign), "-c", "-P", "16", "4", str(APK)], check=True)

    print(f"Verified: {APK}\nSHA-256: {metadata['sha256']}\nBytes: {metadata['bytes']}")
    return {
        "production": {
            "version": "1.5.2",
            "build": 28,
            "bytes": metadata["bytes"],
            "sha256": metadata["sha256"].upper(),
        }
    }


def ssh_command(client: paramiko.SSHClient, command: str, *, sudo: bool = False, password: str = "") -> str:
    script = ('sudo -S -p "" -- ' if sudo else "") + command
    stdin, stdout, stderr = client.exec_command(script, timeout=240)
    if sudo:
        stdin.write(password + "\n")
        stdin.flush()
    stdin.channel.shutdown_write()
    out = stdout.read().decode("utf-8", errors="replace")
    err = stderr.read().decode("utf-8", errors="replace")
    if stdout.channel.recv_exit_status() != 0:
        raise RuntimeError(f"Remote command failed:\n{err or out}")
    return out.strip()


# Atomic update of local site files AFTER the remote transaction succeeded.
def write_json_atomic(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".next")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def update_local_site(receipt: dict, content: dict) -> None:
    site = ROOT.parent / "Kevin" / "site" / "source"
    projects_file = site / "projects.json"
    blog_file = site / "blog-seed.json"
    manifest_file = site / "updates" / "launcher.json"
    if not projects_file.is_file():
        print(f"Local website source not found at {site}. Remote release is published; local site files skipped.")
        return

    projects = json.loads(projects_file.read_text(encoding="utf-8-sig"))
    found = False
    for project in projects:
        if project.get("slug") == "launcher":
            for key in ["buttons", "notes", "releases"]:
                project[key] = content["project"][key]
            found = True
    if not found:
        raise RuntimeError("Local projects.json has no launcher project. Remote release is already live.")

    write_json_atomic(projects_file, projects)
    # Both update channels now point to the current 1.5.2 release.
    write_json_atomic(manifest_file, receipt["manifest"])

    if blog_file.is_file():
        posts = json.loads(blog_file.read_text(encoding="utf-8-sig"))
        for post in posts:
            if post.get("slug") == "building-launcher-for-android":
                for key in ("body", "body_markdown"):
                    if key in post and isinstance(post[key], str):
                        post[key] = re.sub(
                            r"current downloads are Android \d+\.\d+\.\d+",
                            "current downloads are Android 1.5.2", post[key],
                        )
        write_json_atomic(blog_file, posts)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="Verify APK and signing only; do not connect or publish")
    args = parser.parse_args()

    print("Publishing with embedded 1.5.2-only server helper; no migration prerequisite.")
    metadata = check_local_release()
    if args.dry_run:
        print("Dry run passed. No upload or website changes were made.")
        return

    password_file = ROOT.parent / "Kevin" / "server_password.txt"
    password = os.environ.get("KEVLOG_SSH_PASSWORD")
    if not password:
        password = password_file.read_text(encoding="utf-8-sig").strip()

    client = paramiko.SSHClient()
    client.load_host_keys(str(Path.home() / ".ssh" / "known_hosts"))
    client.set_missing_host_key_policy(paramiko.RejectPolicy())
    client.connect(
        "kevlog.co.za", username="kevin", password=password,
        timeout=20, look_for_keys=False, allow_agent=False,
    )
    stage = None
    try:
        stage = ssh_command(client, "mktemp -d /tmp/kevlog-launcher-update-XXXXXXXX")
        if not re.fullmatch(r"/tmp/kevlog-launcher-update-[A-Za-z0-9]+", stage):
            raise RuntimeError("Unexpected server staging path")
        ssh_command(client, "chmod 755 " + shlex.quote(stage))

        with client.open_sftp() as sftp:
            sftp.put(str(APK), stage + "/Launcher-1.5.2.apk")
            # Upload the embedded, version-locked server publisher.
            with sftp.open(stage + "/apply.py", "w") as remote_file:
                remote_file.write(REMOTE_PUBLISHER_SOURCE)
            with sftp.open(stage + "/releases.json", "w") as stream:
                stream.write(json.dumps(metadata))
        print("Uploaded verified APK; backing up server and publishing release 1.5.2.", flush=True)
        ssh_command(client, "chown kevlog:kevlog " + shlex.quote(stage), sudo=True, password=password)

        # Preserve all existing download files; no migration prerequisites.
        wrapper = r'''
from pathlib import Path
import os,sys,json,sqlite3,shutil,subprocess,pwd
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
root=Path('/opt/kevlog/app');data=Path('/var/lib/kevlog');backup=Path('/var/www/kevlog/backups')/stage.name
backup.mkdir(mode=0o700);owner=pwd.getpwnam('kevlog')
manifests=[root/'source/updates/launcher.json',root/'dist/updates/launcher.json']
subprocess.run(['systemctl','stop','kevlog'],check=True)
try:
    with sqlite3.connect(data/'portfolio.sqlite3') as source,sqlite3.connect(backup/'database.sqlite3') as target:source.backup(target)
    (backup/'database.sqlite3').chmod(0o600)
    for index,path in enumerate(manifests):
        if path.exists():shutil.copy2(path,backup/('manifest-'+str(index)+'.json'))
    previous=json.loads(manifests[0].read_text())
    result=subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python',str(stage/'apply.py'),str(stage)],capture_output=True,text=True,check=True)
    receipt=json.loads(result.stdout.strip().splitlines()[-1]);manifest=dict(previous)
    # No migration channel: publish 1.5.2 to both existing manifest fields.
    manifest['android']=receipt['releases']['production']
    manifest['androidProduction']=receipt['releases']['production']
    for path in manifests:
        pending=path.with_suffix('.next');pending.write_text(json.dumps(manifest,indent=2)+'\n')
        pending.chmod(0o644);os.chown(pending,owner.pw_uid,owner.pw_gid);os.replace(pending,path)
    receipt.update(backup=str(backup),manifest=manifest)
    (stage/'receipt.json').write_text(json.dumps(receipt));print(json.dumps(receipt),flush=True)
except BaseException as failure:
    if isinstance(failure,subprocess.CalledProcessError):print(failure.stderr,file=sys.stderr)
    if (backup/'database.sqlite3').exists():
        with sqlite3.connect(backup/'database.sqlite3') as source,sqlite3.connect(data/'portfolio.sqlite3') as target:source.backup(target)
    if (stage/'created.json').exists():
        for file_id in json.loads((stage/'created.json').read_text()):
            assert len(file_id)==32 and all(c in '0123456789abcdef' for c in file_id)
            (data/'uploads/project-files'/file_id).unlink(missing_ok=True)
    for index,path in enumerate(manifests):
        saved=backup/('manifest-'+str(index)+'.json')
        if saved.exists():shutil.copy2(saved,path)
    subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python','-c',"import sys;sys.path.insert(0,'/opt/kevlog/app');import cms;cms.rebuild()"],check=True)
    raise
finally:
    subprocess.run(['systemctl','start','kevlog'],check=True)
'''
        command = (
            "/opt/kevlog/venv/bin/python -c " + shlex.quote(wrapper)
            + " " + shlex.quote(stage)
        )
        receipt = json.loads(ssh_command(client, command, sudo=True, password=password))

        # Persist the receipt first, so later local-site errors can't lose the remote result.
        receipt_file = ROOT / "artifacts" / "published-release-1.5.2.json"
        receipt_file.parent.mkdir(parents=True, exist_ok=True)
        content = receipt.pop("content")
        write_json_atomic(receipt_file, receipt)
        update_local_site(receipt, content)
        print(json.dumps(receipt, indent=2))
        print("Published 1.5.2; website buttons and updater refreshed. Previous files retained.")
    finally:
        if stage:
            try:
                cleanup = (
                    "from pathlib import Path;import sys;"
                    "p=Path(sys.argv[1]);"
                    "assert p.parent==Path('/tmp') and p.name.startswith('kevlog-launcher-update-');"
                    "[(p/n).unlink(missing_ok=True) for n in "
                    "['Launcher-1.5.2.apk','apply.py','releases.json','created.json','receipt.json']];"
                    "p.rmdir()"
                )
                ssh_command(
                    client,
                    "/usr/bin/python3 -c " + shlex.quote(cleanup) + " " + shlex.quote(stage),
                    sudo=True, password=password,
                )
            except Exception as exc:
                print(f"WARNING: Could not clean up server staging folder: {exc}", file=sys.stderr)
        client.close()


if __name__ == "__main__":
    main()
