"""Server-side Launcher 1.5.2 publisher. Invoked by publish_release_1_5_2.py.

Runs as kevlog under the existing site Python environment. It uploads only
Launcher-1.5.2.apk, preserves every existing download, and leaves the legacy
1.5.0 migration bridge untouched.
"""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import secrets
import shutil
import sys
import re

os.environ["KEVLOG_DATA"] = "/var/lib/kevlog"
sys.path.insert(0, "/opt/kevlog/app")
import cms

stage = Path(sys.argv[1]).resolve()
assert stage.parent == Path("/tmp") and stage.name.startswith("kevlog-launcher-update-")

metadata = json.loads((stage / "releases.json").read_text(encoding="utf-8"))
assert metadata["production"]["version"] == "1.5.2"
assert metadata["production"]["build"] == 28

projects = cms.config("projects.json")
project = next(p for p in projects if p["slug"] == "launcher")
if any(str(r.get("version")) == "1.5.2" for r in project.get("releases", [])):
    raise RuntimeError("Launcher 1.5.2 is already published; refusing to duplicate it")

old_buttons = [dict(button) for button in project.get("buttons", [])]
old_ids = sorted({button["file_id"] for button in old_buttons if button.get("file_id")})
old_names = {button.get("filename") for button in old_buttons}
if "Launcher-bridge-1.5.0.apk" not in old_names or "Launcher-1.5.1.apk" not in old_names:
    raise RuntimeError("Existing migration/production buttons missing; refusing unsafe publication")

release = dict(metadata["production"])
source = stage / "Launcher-1.5.2.apk"
with source.open("rb") as file:
    actual_hash = hashlib.file_digest(file, "sha256").hexdigest().upper()
if source.stat().st_size != release["bytes"] or actual_hash != release["sha256"].upper():
    raise RuntimeError("Uploaded APK differs from verified local release")

# Record the ID before creating the file, so the outer rollback can clean it up.
file_id = secrets.token_hex(16)
(stage / "created.json").write_text(json.dumps([file_id]), encoding="utf-8")
with source.open("rb") as incoming, (cms.PROJECT_FILES / file_id).open("xb") as outgoing:
    shutil.copyfileobj(incoming, outgoing)
(cms.PROJECT_FILES / file_id).chmod(0o640)

release["url"] = f"https://kevlog.co.za/downloads/projects/launcher/{file_id}/"
release["notes"] = "https://kevlog.co.za/projects/launcher/#release-launcher-1-5-2"

# Insert latest as first download, retaining ALL original buttons and file IDs.
# Do not relabel/remove the bridge: older installations need that step.
previous_buttons = []
for original in old_buttons:
    button = dict(original)
    if button.get("filename") == "Launcher-1.5.1.apk":
        button["label"] = "Previous Android APK · 1.5.1"
        button["note"] = "Earlier production release. New updates use Launcher 1.5.2."
    elif button.get("filename") == "Launcher-bridge-1.5.0.apk":
        button["label"] = "Update existing Launcher · step 1"
        button["note"] = "For installations before the signing migration: install 1.5.0, then 1.5.2."
    previous_buttons.append(button)

latest_button = {
    "filename": "Launcher-1.5.2.apk",
    "file_id": file_id,
    "label": "Download Launcher 1.5.2",
    "icon": "file_download",
    "colour": "#e4ff00",
    "text_colour": "#11120f",
    "note": "Latest release · Android 7+ · 64-bit. Existing older installations may require the 1.5.0 signing bridge first.",
}
project["buttons"] = [latest_button, *previous_buttons]

notes = []
for original in project.get("notes", []):
    if original.startswith("Signing migration:"):
        continue
    updated = re.sub(
        r"Android release: \d+\.\d+\.\d+, build \d+\.",
        "Android release: 1.5.2, build 28.",
        original,
    )
    notes.append(updated)
notes.append(
    "Signing migration: pre-migration installations update to 1.5.0 first, "
    "then 1.5.2. Keep Launcher installed to preserve its data. "
    "Users already on 1.5.1 can update directly to 1.5.2."
)
project["notes"] = notes

now = datetime.now(timezone.utc).isoformat()
project.setdefault("releases", []).append({
    "id": "launcher-1-5-2",
    "version": "1.5.2",
    "date": datetime.now(timezone.utc).date().isoformat(),
    "status": "published",
    "kind": "release",
    "platforms": "Android 7+ · 64-bit",
    "title": "Google Discover integration",
    "summary": "Optional Google Discover integration with native service support and RSS preferences retained.",
    "body": (
        "<p>Launcher 1.5.2 adds an optional Google Discover feed provider and retains "
        "existing RSS sources, saved stories and personal settings.</p>"
        "<p>Google's native feed overlay depends on the installed Google app and "
        "device compatibility. Some devices may use the Google app fallback instead "
        "of an embedded feed.</p>"
        "<p>Existing installations that have not completed the signing migration "
        "should install the 1.5.0 bridge first, then 1.5.2. Installations already "
        "running 1.5.1 can update directly. Prior APK downloads remain available.</p>"
    ),
    "created_at": now,
    "updated_at": now,
})

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
            "UPDATE posts SET body=?,updated_at=? WHERE id=?",
            (post_body, now, post["id"]),
        )

cms.rebuild()
client = cms.app.test_client()
for url, expected_size in [(release["url"], release["bytes"])]:
    with client.head(url.removeprefix("https://kevlog.co.za")) as response:
        if response.status_code != 200 or response.content_length != expected_size:
            raise RuntimeError(f"New download failed HEAD verification: {url}")
for old_id in old_ids:
    with client.head(f"/downloads/projects/launcher/{old_id}/") as response:
        if response.status_code != 200:
            raise RuntimeError(f"Previous download no longer accessible: {old_id}")

cms.audit(
    "Published Launcher 1.5.2, preserved the 1.5.0 migration bridge, "
    "1.5.1 and all prior download URLs."
)
print(json.dumps({
    "releases": {"production": release},
    "old_file_ids": old_ids,
    "created_file_ids": [file_id],
    "content": {"project": project, "postBody": post_body},
}), flush=True)
