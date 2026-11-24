"""Archive superseded APK downloads for the Launcher project on Kevlog."""
import argparse
import json
from pathlib import Path
import shlex

from publish_launcher_wear import ROOT, read_json, write_json


SERVER = r'''
from contextlib import closing
import fcntl
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import sys
import tempfile

root = Path("/opt/kevlog/app")
data = Path("/var/lib/kevlog")
uploads = data / "uploads/project-files"
database = data / "portfolio.sqlite3"
project_key = "projects.json"
download_path = "/downloads/projects/launcher/"
download_url = "https://kevlog.co.za" + download_path

def rebuild():
    subprocess.run(["runuser", "-u", "kevlog", "--", "env", f"KEVLOG_DATA={data}",
                    "/opt/kevlog/venv/bin/python", "-c",
                    "import sys; sys.path.insert(0, '/opt/kevlog/app'); import cms; cms.rebuild()"],
                   check=True, stdout=sys.stderr)

with Path("/run/lock/kevlog-launcher-release.lock").open("a") as lock:
    fcntl.flock(lock, fcntl.LOCK_EX)
    protected = set()
    for directory in ("source", "dist"):
        feed = json.loads((root / directory / "updates/launcher.json").read_text())
        for release in (feed["android"], feed["androidProduction"], feed["wearCompanion"], feed["watchFace"]["release"]):
            match = re.fullmatch(re.escape(download_url) + r"([0-9a-f]{32})/", release["url"])
            if not match:
                raise ValueError("An update feed download does not match Kevlog's Launcher route.")
            protected.add(match.group(1))

    with closing(sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)) as db:
        original = db.execute("SELECT value FROM config WHERE name=?", (project_key,)).fetchone()[0]
    projects = json.loads(original)
    launcher = next(project for project in projects if project["slug"] == "launcher")
    buttons = launcher["buttons"]
    keep = protected | {button.get("file_id") for button in buttons if not button.get("hidden")}
    keep.update(button.get("file_id") for project in projects if project["slug"] != "launcher"
                for button in project.get("buttons", []))
    candidates = {button["file_id"]: button["filename"] for button in buttons
                  if re.fullmatch(r"[0-9a-f]{32}", button.get("file_id") or "")
                  and button["file_id"] not in keep
                  and button.get("filename", "").lower().endswith(".apk")}
    receipt = {"removed": candidates, "protected": len(protected)}

    if sys.argv[1] == "apply" and candidates:
        backup = Path(tempfile.mkdtemp(prefix="launcher-apk-archive-", dir="/var/www/kevlog/backups"))
        (backup / "projects.json").write_text(original, encoding="utf-8")
        files_to_move = []
        for file_id in candidates:
            source = uploads / file_id
            if source.is_symlink() or (source.exists() and not source.is_file()):
                raise ValueError(f"Invalid APK storage path: {source}")
            if source.is_file():
                files_to_move.append(file_id)
        launcher["buttons"] = [button for button in buttons if button.get("file_id") not in candidates]
        updated = json.dumps(projects, ensure_ascii=False)
        moved = []
        try:
            with closing(sqlite3.connect(database)) as db, db:
                changed = db.execute("UPDATE config SET value=? WHERE name=? AND value=?",
                                     (updated, project_key, original)).rowcount
                if changed != 1:
                    raise RuntimeError("The project changed during cleanup. Run the script again.")
            for file_id in files_to_move:
                os.replace(uploads / file_id, backup / file_id)
                moved.append(file_id)
            rebuild()
        except BaseException:
            for file_id in moved:
                os.replace(backup / file_id, uploads / file_id)
            with closing(sqlite3.connect(database)) as db, db:
                changed = db.execute("UPDATE config SET value=? WHERE name=? AND value=?",
                                     (original, project_key, updated)).rowcount
            if changed:
                rebuild()
            raise
        receipt["backup"] = str(backup)
    print(json.dumps(receipt))
'''


def main():
    import paramiko

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="Remove archived APKs from live downloads")
    args = parser.parse_args()

    site = ROOT.parent / "Kevin"
    password = (site / "server_password.txt").read_text(encoding="utf-8-sig").strip()
    with paramiko.SSHClient() as client:
        client.load_host_keys(str(Path.home() / ".ssh/known_hosts"))
        client.set_missing_host_key_policy(paramiko.RejectPolicy())
        client.connect("kevlog.co.za", username="kevin", password=password, timeout=20,
                       look_for_keys=False, allow_agent=False)
        mode = "apply" if args.apply else "preview"
        command = f'sudo -S -p "" -- /opt/kevlog/venv/bin/python -c {shlex.quote(SERVER)} {mode}'
        incoming, outgoing, errors = client.exec_command(command, timeout=240)
        incoming.write(password + "\n")
        incoming.flush()
        incoming.channel.shutdown_write()
        output = outgoing.read().decode()
        error = errors.read().decode()
        if outgoing.channel.recv_exit_status():
            raise RuntimeError(error or output)
        receipt = json.loads(output)

    if not receipt["removed"]:
        print("No archived Launcher APKs found.")
        return
    print("Removed from public downloads:" if args.apply else "Would remove from public downloads:")
    for filename in receipt["removed"].values():
        print(f"  {filename}")
    print(f"Protected update feed downloads: {receipt['protected']}")
    if args.apply:
        print(f"Private server backup: {receipt['backup']}")
        projects_path = site / "site/source/projects.json"
        projects = read_json(projects_path)
        launcher = next(project for project in projects if project["slug"] == "launcher")
        launcher["buttons"] = [button for button in launcher["buttons"]
                               if button.get("file_id") not in receipt["removed"]]
        write_json(projects_path, projects)


if __name__ == "__main__":
    main()
