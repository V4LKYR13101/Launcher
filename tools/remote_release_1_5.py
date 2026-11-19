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
