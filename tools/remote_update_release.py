"""Apply a scoped Launcher update as the Kevlog service account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());source=stage/'Launcher.apk'
assert meta['version']=='1.1.1' and meta['build']==4
assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[])),'Release already exists; refusing duplicate publication.'
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
new=secrets.token_hex(16);target=cms.PROJECT_FILES/new
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640)
assert hashlib.file_digest(target.open('rb'),'sha256').hexdigest()==meta['sha256']
download=f'/downloads/projects/launcher/{new}/';meta['url']='https://kevlog.co.za'+download
project['buttons']=[dict(filename='Launcher.apk',file_id=new,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note='v1.1.0 Ã‚Â· Android 7+ Ã‚Â· '+f"{meta['bytes']/1048576:.1f} MB")]+[b for b in project.get('buttons',[]) if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n for n in project.get('notes',[]) if not n.startswith('Gallery screens use sample content.')]
project['notes'] += [
 'Version 1.1.0 adds biometric launch locks with an accent fingerprint pattern, larger app icons, flatter Root cards, outlined Focus tiles and animated drawer folding.',
 'Hold an app tile to choose Biometric lock or Open app in grayscale. Locks guard launches and editing through Launcher; they do not replace Android app security.',
 'Per-app grayscale affects the running appÃ¢â‚¬â„¢s display. It needs the optional Launcher app grayscale accessibility service and a one-time WRITE_SECURE_SETTINGS grant from a computer. Settings includes the setup steps. Colour correction affects the whole display while the chosen app is in front.',
 'Wallpaper handling keeps the system wallpaper on a stable window layer while scrolling and refreshing content.',
 'The gallery was captured with version 1.0.1 and sample content. It predates the 1.1.0 visual changes.'
]
project['notes']=list(dict.fromkeys(project['notes']))
now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-1-1',version='1.1.1',date='2026-11-13',status='published',kind='release',platforms='Android 7+',title='Fingerprint scaling and wallpaper stability',summary='Full-tile fingerprint ridges and stable wallpaper rendering, with a newer build for in-app updating.',body='<p>Fingerprint ridges fill standard, wide and tall tiles without crossing. Wallpaper rendering keeps a stable transparent window surface during scrolling. Malformed punctuation in Settings and widget options is corrected. This release uses build 4 so development installations on build 3 can update through Settings.</p><ul><li>Biometric launch locks with an accent fingerprint background that fills each tile.</li><li>Per-app display grayscale settings with an optional foreground service and one-time display permission.</li><li>Larger icons, flatter Root cards and outlined Focus/Sun tiles.</li><li>Animated drawer folding and stable system wallpaper handling.</li><li>A standard Visual Studio solution for deploying directly to a phone.</li></ul><p>Built successfully. Device acceptance testing is being handled by the owner.</p>',created_at=now,updated_at=now))
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE')
 db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  body=post['body'].replace('Focus mode keeps Essentials and pinned apps visible. It helps narrow the home screen, but it does not block another app or silence the phone.','Focus mode gives the app tiles a quiet outlined appearance. Apps stay available; it does not block another app or silence the phone.')
  section='''## Added in 1.1.0

Apps can now require authentication when opened through Launcher. Their tiles carry a fingerprint pattern in the selected accent, drawn across the full tile. Hold a tile to turn Biometric lock on or off. Android still controls other ways of opening an app.

The same editor includes Open app in grayscale. This setting changes the display while that app is in front, separately from icon colour. It needs the optional accessibility service and a one-time display permission from a computer. Settings explains both steps. When you leave the app, Launcher restores the previous colour-correction setting.

Root has a flatter layout, app icons have more room, Focus uses outlined tiles and drawers fold with an animation. Wallpaper handling also keeps its window layer stable during scrolling. The gallery below was captured before this visual update.

'''
  if '## Added in 1.1.0' not in body:body=body.replace('## Install Launcher',section+'## Install Launcher')
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=? AND body=?',(body,now,post['id'],post['body']))
cms.rebuild()
client=cms.app.test_client()
with client.head(download) as response:assert response.status_code==200 and response.content_length==meta['bytes']
response=client.get('/projects/launcher/');assert response.status_code==200 and download.encode() in response.data
for file_id in old:
 with client.head(f'/downloads/projects/launcher/{file_id}/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.1.1 and replaced its Android APK download.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=new,project='/projects/launcher/')),flush=True)
