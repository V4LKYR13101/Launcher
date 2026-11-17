"""Publish the Android haptic update as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.13' and meta['build']==23
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.13 \u00b7 Android 7+ \u00b7 {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.12, build 22.', 'Android release: 1.3.13, build 23.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-13',version='1.3.13',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='Touch feedback you can choose',summary='Off, Subtle and Crisp haptics for Launcher controls and committed gestures.',body='<p>Settings now includes Haptic feedback with Off, Subtle and Crisp choices, selected-state indicators and sample effects. The preference is saved with your layout.</p><p>Taps, holds, switches, page changes, drawer gestures, sheet dismissals and notification swipes use Android haptic feedback. Ordinary scrolling and background refreshes stay quiet. Repeated feedback from the same action is limited, and a moving finger cancels the blank-area long press.</p><p>Off disables Launcher interaction feedback. Android navigation, the keyboard, authentication and widget providers keep their own settings. Effects follow Android touch-vibration policy and the hardware controls their strength. No vibration permission or continuous vibration service is added.</p><p>Release compilation and 61 core checks passed. Native emulator checks verify event routing and preference behaviour; the physical feel of the motor must be judged on the phone. No physical phone was used. The Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('current downloads are Android 1.3.12','current downloads are Android 1.3.13')
  post_body=post_body.replace('The optional Phone resources card has battery, memory and storage charts, with recent readings collected while the card is open.','The optional Phone resources card shows current battery, memory and storage figures as donut charts. Tap a reading for the exact values.')
  detail="Typing narrows the full app list. You don't have to scroll or load another batch before an app can appear in the results. Launcher keeps the same app catalogue for the drawers and search, updating it when apps change.\n\n"
  marker='The other drawers can stay closed.\n\n'
  if detail not in post_body:post_body=post_body.replace(marker,marker+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.13 haptic feedback and emulator-checked settings; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
