"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.10' and meta['build']==20
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.10 Â· Android 7+ Â· {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.9, build 19.', 'Android release: 1.3.10, build 20.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-10',version='1.3.10',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='A clearer, more consistent interface',summary='Grouped Settings, accent-aware forms and layouts that accommodate larger text.',body='<p>Settings is grouped into Home screen, Cards and conversations, Connections and access, and Updates and backup. Disabled controls have distinct colours, and input focus, selection and cursors use the chosen accent.</p><p>Shared actions, drawer headings, tile labels, weather rows, quick controls, media metadata and icon-picker labels make more room for larger text. Feed thumbnails adapt to narrower screens. Root actions use equal widths where they share a row.</p><p>Root, Apps and Conversations remember their scroll positions when switching pages or returning from search. Existing navigation gestures are unchanged.</p><p>Release compilation and package checks passed. No device or functional tests were run. Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('current downloads are Android 1.3.9','current downloads are Android 1.3.10')
  post_body=post_body.replace('The optional Phone resources card has battery, memory and storage charts, with recent readings collected while the card is open.','The optional Phone resources card shows current battery, memory and storage figures as donut charts. Tap a reading for the exact values.')
  detail="Typing narrows the full app list. You don't have to scroll or load another batch before an app can appear in the results. Launcher keeps the same app catalogue for the drawers and search, updating it when apps change.\n\n"
  marker='The other drawers can stay closed.\n\n'
  if detail not in post_body:post_body=post_body.replace(marker,marker+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.10 interface refinements; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
