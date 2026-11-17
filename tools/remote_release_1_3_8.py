"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.8' and meta['build']==18
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.8 · Android 7+ · {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.7, build 17.', 'Android release: 1.3.8, build 18.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-8',version='1.3.8',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='Layers throughout the everyday interface',summary='Root card reveals, page perspective, staggered drawers and optional phone tilt, with one master switch.',body='<p>Layering now reaches everyday navigation. Root cards reveal their content beneath steady headings, drawer apps appear with a small stagger, and page changes add perspective and content parallax. Status and search stay fixed.</p><p>The app actions panel is smaller and anchored to the tile edge. Inline usage and compact Open, Edit and Details actions replace the large dashboard-style block.</p><p>Tilt depth is optional and off initially. On supported phones it uses a rotation sensor for bounded visual movement. It freezes during touch and pauses beneath dialogs, during other layer animations and outside the home pages. The existing swipe thresholds, scrolling and gesture routing are retained; no new navigation gesture is added.</p><p>Layered interface is the master switch. Off disables all new layer effects and tilt, returning to flat, immediate page/card changes and the standard app editor. Android animation settings still apply.</p><p>Release compilation and publication checks passed. Source review confirmed preserved gesture recognition. No device or functional tests were run; physical feel and sensor behaviour remain for owner testing. The Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('install version 1.3.7','install version 1.3.8')
  detail='## Everyday layers in 1.3.8\n\nThe layered treatment now reaches Root cards, app drawers and page changes. Cards reveal their content below a steady heading, drawer apps appear with a small stagger, and recognised page swipes finish with perspective and content parallax. The status row and search stay fixed.\n\nApp actions are smaller and anchored to the tile edge. Inline usage and compact Open, Edit and Details actions replace the larger dashboard treatment.\n\nTilt depth is a separate option in Appearance, off initially. It uses a supported rotation sensor for gentle movement between content and heading planes. It calibrates at activation and rotation, freezes during touch and stops beneath dialogs or when Launcher is backgrounded. It does not add a navigation gesture or change the existing swipe thresholds.\n\nLayered interface is the master switch. Turn it off to disable app expansion, perspective, card and drawer reveals, scroll masking and tilt together. Page and card changes then become immediate. Android animation settings are also respected. Physical feel and sensor behaviour remain for owner testing; no device or functional tests were run.\n\n'
  if detail not in post_body:post_body=post_body.replace('## Icons and light-sensor themes in 1.3.0\n\n','## Icons and light-sensor themes in 1.3.0\n\n'+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.8 everyday layers and optional sensor tilt; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
