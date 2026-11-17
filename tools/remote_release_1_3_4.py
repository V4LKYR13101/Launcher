"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.4' and meta['build']==14
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.4 · Android 7+ · {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.3, build 13.', 'Android release: 1.3.4, build 14.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-4',version='1.3.4',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='A better place to read your feeds',summary='Choose your story count, keep reading during refresh and show optional article images.',body='<p>Feed settings now lets you choose how many stories appear initially, from 1 to 50. Show more adds another batch. Stories use clearer headlines, accent-coloured publisher names and quieter dates.</p><p>Current stories stay visible while sources refresh. Successful results are saved in private cache storage and can appear immediately after Launcher reopens. A failed source keeps its saved stories with a short explanation. Refresh completion updates the feed content without rebuilding the widgets page.</p><p>Optional rounded thumbnails use images supplied in RSS or Atom feeds. Turn images off for a clean full-width text layout. Missing or failed images leave no empty space. Image downloads and decoding are bounded, with a reusable disk cache.</p><p>Release compilation and publication checks passed. No device or functional tests were run. The Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('install version 1.3.3','install version 1.3.4')
  detail='## A better reading layout in 1.3.4\n\nNews & feeds now lets you choose the initial number of stories, from 1 to 50, with Show more available below the list. Publisher names use your accent, headlines have a clearer hierarchy, and dates sit quietly below the summary.\n\nStories stay visible during refresh. Successful source results are saved in private cache storage, so reopening Launcher can show saved stories immediately. Failed sources keep their previous stories. Android can clear this disposable cache.\n\nImages are optional. Where a feed supplies an article image, Launcher can show a rounded thumbnail beside the headline. Turn images off in Feed settings for a full-width reading layout. Stories without an image use that same layout, with no empty image box. Thumbnails are cached and decoded at a small size; Launcher does not fetch every article page to search for artwork.\n\n'
  if detail not in post_body:post_body=post_body.replace('## Icons and light-sensor themes in 1.3.0\n\n','## Icons and light-sensor themes in 1.3.0\n\n'+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.4 feed layout, saved stories and optional images; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
