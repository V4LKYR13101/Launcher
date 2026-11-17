"""Publish the two Launcher binaries as the Kevlog service account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.2.0' and meta['build']==5
desktop=json.loads((stage/'companion.json').read_text())
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[])),'Release already published.'
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
created=[]
for filename,details,magic in [('Launcher.apk',meta,b'PK\x03\x04'),('LauncherCompanion.exe',desktop,b'MZ')]:
 source=stage/filename
 with source.open('rb') as data:assert data.read(len(magic))==magic
 assert source.stat().st_size==details['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==details['sha256']
 file_id=secrets.token_hex(16);created.append(file_id);(stage/'created.json').write_text(json.dumps(created))
 target=cms.PROJECT_FILES/file_id
 with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
 target.chmod(0o640);assert hashlib.file_digest(target.open('rb'),'sha256').hexdigest()==details['sha256']
 details['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[
 dict(filename='Launcher.apk',file_id=created[0],label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.2.0 · Android 7+ · {meta['bytes']/1048576:.1f} MB"),
 dict(filename='LauncherCompanion.exe',file_id=created[1],label='Download Windows companion',icon='desktop_windows',colour='#e4ff00',text_colour='#11120f',note=f"v1.2.0 · Windows x64 · {desktop['bytes']/1048576:.1f} MB")
]+[b for b in project.get('buttons',[]) if not b.get('filename','').lower().endswith('.apk') and b.get('filename')!='LauncherCompanion.exe']
now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-2-0',version='1.2.0',date='2026-11-13',status='published',kind='release',platforms='Android 7+ / Windows x64',title='Root workspace and Windows companion',summary='Icon packs, app dashboards, richer Root cards, saved notification messages and a local Windows companion.',body='<p>Launcher 1.2.0 adds icon packs and individual icon choices, tile palettes, per-app badge muting, hourly and weekly usage charts, and QR sharing.</p><p>Root now supports commands, multiple notes, independent timers, a weather graph, media controls, RSS feeds, web and YouTube results, and manually saved travel tickets. Hold a card header to pin, arrange or hide it. Drawer folding follows a drag on its heading.</p><p>The optional Windows companion shares notes and a selected folder over your own network. Pair by code and compare the fingerprint. Keep Launcher visible during the connection. File transfers are limited to 16 MB.</p><p>Message history saves messages supplied by notifications, with replies where the source app offers a reply action. It cannot retrieve earlier private chats. Inline web and YouTube results need your own provider API keys; browser search remains available.</p><p>Both release builds compiled successfully. Phone acceptance testing is being handled by the owner.</p>',created_at=now,updated_at=now))
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
cms.rebuild();client=cms.app.test_client()
for details in [meta,desktop]:
 with client.head(details['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==details['bytes']
for file_id in old:
 with client.head('/downloads/projects/launcher/'+file_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.2.0 APK and Windows companion. Replaced the previous live APK.')
print(json.dumps(dict(release=meta,companion=desktop,old_file_ids=old,new_file_id=created[0],created_file_ids=created)),flush=True)
