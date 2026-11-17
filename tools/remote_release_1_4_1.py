"""Publish the Android haptic update as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.4.1' and meta['build']==25
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.4.1 \u00b7 Android 7+ \u00b7 {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.4.0, build 24.', 'Android release: 1.4.1, build 25.') for n in project.get('notes',[])]

if not any(n.startswith('Optional system panel:') for n in project.get('notes',[])):project.setdefault('notes',[]).append('Optional system panel: notifications, media, volume and brightness across apps, with a separate master switch and accessibility setup. Samsung SystemUI is unchanged.')
now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-4-1',version='1.4.1',date='2026-11-15',status='published',kind='release',platforms='Android 7+',title='Choose your tile icon size',summary='A live preview and size slider for app icons inside tiles.',body='<p>Appearance now includes Tile icon size. Adjust the slider from 60 to 150 percent, preview the result, save it or reset to the default 100 percent. Cancel leaves your current setting in place.</p><p>The setting applies to standard and expanded app tiles. Icons fit their available space so labels and usage times stay clear, while tile dimensions, grid spacing and notification badges keep their existing placement.</p><p>Existing layouts keep the default icon size. Your choice is included in layout backups. Release compilation, 68 core checks and native emulator checks passed. The Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('current downloads are Android 1.4.0','current downloads are Android 1.4.1')
  post_body=post_body.replace('The optional Phone resources card has battery, memory and storage charts, with recent readings collected while the card is open.','The optional Phone resources card shows current battery, memory and storage figures as donut charts. Tap a reading for the exact values.')
  detail="Typing narrows the full app list. You don't have to scroll or load another batch before an app can appear in the results. Launcher keeps the same app catalogue for the drawers and search, updating it when apps change.\n\n"
  marker='The other drawers can stay closed.\n\n'
  if detail not in post_body:post_body=post_body.replace(marker,marker+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.4.1 tile icon sizing and emulator-checked settings; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
