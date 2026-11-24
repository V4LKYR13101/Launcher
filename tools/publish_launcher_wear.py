"""Publish the signed Launcher phone, companion and watch face APKs to Kevlog."""
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import re
import shlex
import sys
import time
from urllib.error import HTTPError
from urllib.request import urlopen

SITE = "https://kevlog.co.za"
ROOT = Path(__file__).resolve().parents[1]
FEED_PATH = "/updates/launcher.json"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def apply_on_server(stage):
    import os
    import pwd
    import sqlite3
    import subprocess
    import shutil

    if stage.parent != Path("/tmp") or not stage.name.startswith("kevlog-launcher-update-"):
        raise ValueError("Invalid staging path.")

    root = Path("/opt/kevlog/app")
    data = Path("/var/lib/kevlog")
    backup = Path("/var/www/kevlog/backups") / stage.name
    manifests = [root / directory / "updates/launcher.json" for directory in ("source", "dist")]
    owner = pwd.getpwnam("kevlog")
    previous = read_json(manifests[0])
    metadata = read_json(stage / "releases.json")
    phone = metadata["androidProduction"]
    watch = metadata["watchFace"]["release"]
    companion = metadata["wearCompanion"]
    previous_watch = previous.get("watchFace", {}).get("release", {})
    keep_phone = "file" not in phone
    keep_watch = "file" not in watch
    keep_companion = "file" not in companion
    bridge = previous["android"]
    bridge_file = stage / "legacy-bridge.apk"
    bridge_id = bridge["url"].removeprefix(SITE + "/downloads/projects/launcher/").strip("/")
    if bridge_file.exists() and (
        not re.fullmatch(r"[0-9a-f]{32}", bridge_id) or
        bridge_file.stat().st_size != bridge["bytes"] or digest(bridge_file) != bridge["sha256"].lower()
    ):
        raise ValueError("The legacy APK does not match the existing public feed.")
    if keep_phone and phone != previous.get("androidProduction"):
        raise ValueError("The requested phone app differs from the live release.")
    if not keep_phone and previous.get("androidProduction", {}).get("build", 0) >= phone["build"]:
        raise ValueError("Kevlog already serves this Launcher build or a later one.")
    if keep_watch and metadata["watchFace"] != previous.get("watchFace"):
        raise ValueError("The requested watch face differs from the live release.")
    if not keep_watch and previous_watch.get("build", 0) >= watch["build"]:
        raise ValueError("Kevlog already serves this watch face build or a later one.")
    if keep_companion and companion != previous.get("wearCompanion"):
        raise ValueError("The requested watch companion differs from the live release.")

    uploads = [] if keep_phone else [phone]
    if not keep_companion:
        uploads.append(companion)
    if not keep_watch:
        uploads.append(watch)
    for release in uploads:
        package = stage / release["file"]
        if package.stat().st_size != release["bytes"] or digest(package) != release["sha256"]:
            raise ValueError(f"Staged download failed verification: {package.name}")

    backup.mkdir(mode=0o700)
    created_ids = []
    subprocess.run(["systemctl", "stop", "kevlog"], check=True)
    try:
        database = data / "portfolio.sqlite3"
        with sqlite3.connect(database) as source, sqlite3.connect(backup / "database.sqlite3") as copy:
            source.backup(copy)
        (backup / "database.sqlite3").chmod(0o600)
        for index, path in enumerate(manifests):
            shutil.copy2(path, backup / f"manifest-{index}.json")

        os.environ["KEVLOG_DATA"] = str(data)
        sys.path.insert(0, str(root))
        import cms

        projects = cms.config("projects.json")
        project = next(project for project in projects if project["slug"] == "launcher")
        release_id = ("launcher-roll-" + watch["version"].replace(".", "-") if keep_phone else
                      "launcher-" + phone["version"].replace(".", "-"))
        if any(release.get("id") == release_id for release in project.get("releases", [])):
            raise ValueError("Kevlog already has this release entry.")

        buttons = []
        downloads = [] if keep_phone else [
            (phone, f"Download Launcher {phone['version']}", "Android 7+ · 64-bit"),
        ]
        if not keep_companion:
            downloads.append((companion, f"Launcher for Wear OS {companion['version']}", "Companion APK · Wear OS 5+"))
        if not keep_watch:
            downloads.append((watch, f"Launcher Roll {watch['version']}",
                              "On Wear OS 6+, install from Launcher once the watch companion is installed."))
        for release, label, note in downloads:
            name = release.pop("file")
            file_id = release["sha256"][:32]
            target = cms.PROJECT_FILES / file_id
            with (stage / name).open("rb") as source, target.open("xb") as destination:
                created_ids.append(file_id)
                shutil.copyfileobj(source, destination)
            os.chown(target, owner.pw_uid, owner.pw_gid)
            target.chmod(0o640)
            release["url"] = f"{SITE}/downloads/projects/launcher/{file_id}/"
            buttons.append(dict(filename=name, file_id=file_id, label=label, note=note,
                                icon="file_download", colour="#e4ff00", text_colour="#11120f"))

        if bridge_file.exists():
            target = cms.PROJECT_FILES / bridge_id
            if target.exists():
                if digest(target) != bridge["sha256"].lower():
                    raise ValueError("The existing legacy download has the wrong digest.")
            else:
                with bridge_file.open("rb") as source, target.open("xb") as destination:
                    created_ids.append(bridge_id)
                    shutil.copyfileobj(source, destination)
                os.chown(target, owner.pw_uid, owner.pw_gid)
                target.chmod(0o640)
            if not any(button.get("file_id") == bridge_id for button in project.get("buttons", [])):
                buttons.append(dict(filename=f"Launcher-{bridge['version']}.apk", file_id=bridge_id,
                                    label=f"Legacy signing migration · {bridge['version']}",
                                    note="First update for older Launcher installations.",
                                    icon="file_download", colour="#e4ff00", text_colour="#11120f"))

        for button in project.get("buttons", []):
            if not keep_phone and previous.get("androidProduction", {}).get("url", "").rstrip("/").endswith("/" + button.get("file_id", "")):
                button["label"] = f"Previous Launcher APK · {previous['androidProduction']['version']}"
            if not keep_companion and previous.get("wearCompanion", {}).get("url", "").rstrip("/").endswith("/" + button.get("file_id", "")):
                button["label"] = f"Previous Wear OS companion · {previous['wearCompanion']['version']}"
        project["buttons"] = buttons + project.get("buttons", [])
        active_ids = [release["url"].rstrip("/").rsplit("/", 1)[-1]
                      for release in (phone, companion, watch)]
        for button in project["buttons"]:
            if button.get("file_id") and button.get("filename", "").lower().endswith(".apk"):
                if button["file_id"] in active_ids:
                    button.pop("hidden", None)
                else:
                    button["hidden"] = True
        order = {file_id: index for index, file_id in enumerate(active_ids)}
        project["buttons"].sort(key=lambda button: order.get(button.get("file_id"), len(order)))
        published = watch if keep_phone else phone
        published["notes"] = f"{SITE}/projects/launcher/#release-{release_id}"
        now = datetime.now(timezone(timedelta(hours=2)))
        project.setdefault("releases", []).append(dict(
            id=release_id, version=f"Launcher Roll {watch['version']}" if keep_phone else phone["version"],
            date=now.date().isoformat(),
            status="published", kind="release", platforms="Android 7+ · Wear OS 6+ for face push",
            title="Larger centered time and weather" if keep_phone else
                "Wear OS connection fix" if keep_companion else
                "Wear OS setup and first install" if keep_watch else "Launcher Roll for Wear OS",
            summary=("Larger central digits, tighter spacing and a weather complication."
                     if keep_phone else "Corrects how the phone handles replies from the watch companion."
                     if keep_companion else "Install Launcher Roll from its own setup screen in Launcher settings."),
            body=("<p>Launcher Roll now centers the time, enlarges the active digits and staggers the two rows of neighboring numbers. "
                  "If the built-in weather feed is unavailable, open the watch face editor and select a source "
                  "for the Weather complication.</p>" if keep_phone else
                  "<p>This phone release fixes decoding of watch replies. The corrected install flow also requires "
                  "Launcher for Wear OS 1.0.3 on the watch.</p>" if keep_companion else
                 "<p>Install and open Launcher for Wear OS on the watch. On the paired Android phone, "
                 "open Launcher and go to <strong>Settings → Wear OS → Install Launcher Roll</strong>. "
                 "Approve the download and select Launcher Roll in the watch face picker. "
                 "The Wear OS settings screen has a separate action for checking installed face updates. "
                 "Phone-driven face installs and updates require Wear OS 6 or later.</p>"
                 "<p>This release is available for device testing.</p>"),
            created_at=now.isoformat(), updated_at=now.isoformat(),
        ))
        with cms.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            db.execute("UPDATE config SET value=? WHERE name=?",
                       (json.dumps(projects, ensure_ascii=False), "projects.json"))

        subprocess.run(["runuser", "-u", "kevlog", "--", "env", f"KEVLOG_DATA={data}",
                        "/opt/kevlog/venv/bin/python", "-c",
                        "import sys; sys.path.insert(0, '/opt/kevlog/app'); import cms; cms.rebuild()"], check=True)
        client = cms.app.test_client()
        releases = [phone, watch, companion, previous["androidProduction"]]
        if bridge_file.exists():
            releases.append(bridge)
        for release in releases:
            with client.head(release["url"].removeprefix(SITE)) as response:
                if response.status_code != 200 or response.content_length != release["bytes"]:
                    raise RuntimeError(f"Download check failed for build {release['build']}.")

        feed = dict(previous, **metadata)
        for path in manifests:
            pending = path.with_suffix(".next")
            write_json(pending, feed)
            os.chown(pending, owner.pw_uid, owner.pw_gid)
            pending.chmod(0o644)
            os.replace(pending, path)
        print(json.dumps(dict(feed=feed, project=project, backup=str(backup)), ensure_ascii=False), flush=True)
    except BaseException:
        if (backup / "database.sqlite3").exists():
            with sqlite3.connect(backup / "database.sqlite3") as source, sqlite3.connect(data / "portfolio.sqlite3") as copy:
                source.backup(copy)
        for file_id in created_ids:
            (data / "uploads/project-files" / file_id).unlink(missing_ok=True)
        for index, path in enumerate(manifests):
            saved = backup / f"manifest-{index}.json"
            if saved.exists():
                shutil.copy2(saved, path)
            path.with_suffix(".next").unlink(missing_ok=True)
        subprocess.run(["runuser", "-u", "kevlog", "--", "env", f"KEVLOG_DATA={data}",
                        "/opt/kevlog/venv/bin/python", "-c",
                        "import sys; sys.path.insert(0, '/opt/kevlog/app'); import cms; cms.rebuild()"], check=True)
        raise
    finally:
        subprocess.run(["systemctl", "start", "kevlog"], check=True)


def publish(phone_directory, watch_directory, legacy_bridge=None, face_only=False):
    import paramiko
    import xml.etree.ElementTree as et

    face_entry = watch_directory / "watch-face-feed-entry.json" if watch_directory else None
    has_face = face_entry and face_entry.exists()
    if face_only or not has_face:
        with urlopen(SITE + FEED_PATH + f"?t={time.time_ns()}", timeout=25) as response:
            live = json.load(response)
    if face_only:
        phone = live["androidProduction"]
        if not has_face:
            raise ValueError("The watch face package and feed entry are required.")
        artifacts = []
    else:
        phone = read_json(phone_directory / "release-info.json")
        phone["build"] = int(phone["build"])
        phone["sha256"] = phone["sha256"].lower()
        artifacts = [(phone_directory / phone["file"], phone)]
    if face_entry and face_entry.exists():
        face = read_json(face_entry)["watchFace"]
        face["release"]["file"] = f"Launcher-Roll-{face['release']['version']}.apk"
    else:
        face = live["watchFace"]
    if watch_directory and not face_only:
        settings = et.parse(ROOT / "src/Launcher.Wear/Launcher.Wear.csproj")
        wear_version = settings.findtext("PropertyGroup/ApplicationDisplayVersion")
        wear_build = int(settings.findtext("PropertyGroup/ApplicationVersion"))
        companion_path = watch_directory / f"Launcher-for-Wear-{wear_version}.apk"
        companion = dict(version=wear_version, build=wear_build, file=companion_path.name,
                         bytes=companion_path.stat().st_size, sha256=digest(companion_path))
        artifacts.append((companion_path, companion))
    else:
        companion = live["wearCompanion"]
    metadata = dict(androidProduction=phone, watchFace=face, wearCompanion=companion)
    if face_entry and face_entry.exists():
        artifacts.append((watch_directory / face["release"]["file"], face["release"]))
    for path, release in artifacts:
        if path.stat().st_size != release["bytes"] or digest(path) != release["sha256"]:
            raise ValueError(f"Release artifact failed verification: {path.name}")

    site = ROOT.parent / "Kevin"
    password = (site / "server_password.txt").read_text(encoding="utf-8-sig").strip()
    with paramiko.SSHClient() as client:
        client.load_host_keys(str(Path.home() / ".ssh/known_hosts"))
        client.set_missing_host_key_policy(paramiko.RejectPolicy())
        client.connect("kevlog.co.za", username="kevin", password=password, timeout=20,
                       look_for_keys=False, allow_agent=False)

        def run(command, sudo=False):
            prefix = 'sudo -S -p "" -- ' if sudo else ""
            incoming, outgoing, errors = client.exec_command(prefix + command, timeout=240)
            if sudo:
                incoming.write(password + "\n")
                incoming.flush()
            incoming.channel.shutdown_write()
            output = outgoing.read().decode()
            error = errors.read().decode()
            if outgoing.channel.recv_exit_status():
                raise RuntimeError(error or output)
            return output.strip()

        stage = run("mktemp -d /tmp/kevlog-launcher-update-XXXXXXXX")
        names = [path.name for path, _ in artifacts] + ["publish.py", "releases.json"]
        if legacy_bridge is not None:
            names.append("legacy-bridge.apk")
        try:
            with client.open_sftp() as sftp:
                for path, _ in artifacts:
                    sftp.put(str(path), f"{stage}/{path.name}")
                if legacy_bridge is not None:
                    sftp.put(str(legacy_bridge), f"{stage}/legacy-bridge.apk")
                sftp.put(__file__, f"{stage}/publish.py")
                with sftp.open(f"{stage}/releases.json", "w") as stream:
                    stream.write(json.dumps(metadata))
            receipt = json.loads(run(f"/opt/kevlog/venv/bin/python {shlex.quote(stage + '/publish.py')} --apply {shlex.quote(stage)}", True).splitlines()[-1])
        finally:
            with client.open_sftp() as sftp:
                for name in names:
                    try:
                        sftp.remove(f"{stage}/{name}")
                    except FileNotFoundError:
                        pass
                sftp.rmdir(stage)

    receipt_directory = watch_directory if face_only else phone_directory
    write_json(receipt_directory / "kevlog-publication.json", receipt)
    verify_publication(receipt_directory, legacy_bridge is not None)


def verify_publication(phone_directory, verify_bridge):
    receipt = read_json(phone_directory / "kevlog-publication.json")
    build = receipt["feed"]["androidProduction"]["build"]
    # Wait briefly for the web workers after the service restart.
    for attempt in range(5):
        try:
            with urlopen(SITE + FEED_PATH + f"?build={build}&t={time.time_ns()}", timeout=25) as response:
                live = json.load(response)
            break
        except HTTPError as error:
            if error.code not in (502, 503, 504) or attempt == 4:
                raise
            time.sleep(2)
    if live != receipt["feed"]:
        raise RuntimeError("The public update feed differs from the published feed.")
    releases = [live["androidProduction"], live["watchFace"]["release"], live["wearCompanion"]]
    if verify_bridge:
        releases.append(live["android"])
    for release in releases:
        with urlopen(release["url"], timeout=35) as response:
            sha256 = hashlib.sha256()
            total = 0
            while chunk := response.read(262144):
                sha256.update(chunk)
                total += len(chunk)
        if total != release["bytes"] or sha256.hexdigest() != release["sha256"].lower():
            raise RuntimeError("A public download differs from the signed release.")
        print(f"Verified {release['url']}", flush=True)
    local = ROOT.parent / "Kevin/site/source"
    write_json(local / FEED_PATH.lstrip("/"), live)
    projects_file = local / "projects.json"
    projects = read_json(projects_file)
    project = next(project for project in projects if project["slug"] == "launcher")
    project.update({key: receipt["project"][key] for key in ("buttons", "releases")})
    write_json(projects_file, projects)


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--apply":
        apply_on_server(Path(sys.argv[2]))
    elif len(sys.argv) in (3, 4) and sys.argv[1] == "--verify":
        verify_publication(Path(sys.argv[2]), len(sys.argv) == 4 and sys.argv[3] == "--legacy")
    elif len(sys.argv) == 3 and sys.argv[1] == "--phone-only":
        publish(Path(sys.argv[2]), None)
    elif len(sys.argv) == 3 and sys.argv[1] == "--face-only":
        publish(None, Path(sys.argv[2]), face_only=True)
    elif len(sys.argv) in (3, 4):
        publish(Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]) if len(sys.argv) == 4 else None)
    else:
        raise SystemExit("Usage: publish_launcher_wear.py PHONE_RELEASE_DIR WATCH_RELEASE_DIR [LEGACY_BRIDGE_APK]")
