"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.6' and meta['build']==16
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.6 · Android 7+ · {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.5, build 15.', 'Android release: 1.3.6, build 16.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-6',version='1.3.6',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='Phone resources and smoother pages',summary='Accent donut charts for battery, memory and storage, plus animated page changes.',body='<p>Add the Phone resources card from Root to see battery remaining, memory used and internal storage used as three accent-coloured donut charts. Tap a donut for exact readings. The card also shows battery state and temperature when available.</p><p>Switch the graph between memory and battery. It records real readings every ten seconds while the expanded card is on screen, with up to fifteen minutes of session history. Gaps remain when readings pause. No extra permissions or background service are needed.</p><p>Root, Apps and Conversations now slide between pages with a restrained fade. The status row and bottom bar stay fixed. New touches finish an active transition immediately. Appearance has a Page transitions switch, and Android animation settings are respected.</p><p>Release compilation and publication checks passed. No device or functional tests were run. The Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('install version 1.3.5','install version 1.3.6')
  detail='## Phone resources and motion in 1.3.6\n\nRoot has a new optional Phone resources card. Add it through Add card to see battery remaining, memory used and internal data-volume storage used as three donut charts in your chosen accent. Light mode uses the darker accent shade for contrast. Tap a chart for exact totals and an explanation of the reading. Battery temperature and charging state appear where available.\n\nThe graph switches between memory and battery, using real samples every ten seconds while the expanded card is on screen. It retains up to fifteen minutes of session readings and leaves gaps while sampling is paused. It needs no extra permissions or background service. Android can reclaim cached RAM; a high used percentage alone is not a warning. Storage totals describe the internal data volume, rather than advertised device capacity.\n\nRoot, Apps and Conversations now have a short directional slide with a subtle fade. The old page remains visible during the handover, while the status row and bottom bar stay fixed. A new touch finishes the active transition, and the effect can be disabled under Appearance. Android animation settings still apply.\n\n'
  if detail not in post_body:post_body=post_body.replace('## Icons and light-sensor themes in 1.3.0\n\n','## Icons and light-sensor themes in 1.3.0\n\n'+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.6 resource charts and page transitions; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
