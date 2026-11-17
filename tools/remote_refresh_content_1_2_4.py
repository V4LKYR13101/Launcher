"""Refresh only the Launcher project, gallery and existing article."""
from pathlib import Path
import os,sys,json,hashlib,shutil
from datetime import datetime,timezone
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-content-')
p=json.loads((stage/'content.json').read_text(encoding='utf-8'));project=p['project'];assert project['slug']=='launcher'
projects=cms.config('projects.json');current=next(x for x in projects if x['slug']=='launcher');assert current['buttons']==p['expectedButtons'],'Downloads changed during content preparation.'
galleries=cms.config('galleries.json');assert galleries.get('launcher')==p['expectedGallery'],'Gallery changed during content preparation.'
for name,digest in p['media'].items():
 assert Path(name).name==name and name.startswith('launcher-124-') and name.endswith('.webp')
 source=stage/name;assert hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==digest
 target=cms.UPLOADS/name
 if target.exists():assert hashlib.file_digest(target.open('rb'),'sha256').hexdigest()==digest
 else:
  with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
  target.chmod(0o644)
projects=[project if x['slug']=='launcher' else x for x in projects];galleries['launcher']=p['gallery'];now=datetime.now(timezone.utc).isoformat()
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE')
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone();assert post and post['body']==p['expectedBody'],'Article changed during content preparation.'
 for name,value in [('projects.json',projects),('galleries.json',galleries)]:db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(value,ensure_ascii=False),name))
 article=p['post'];db.execute('UPDATE posts SET title=?,excerpt=?,body=?,format=?,cover=?,reading_minutes=?,updated_at=? WHERE id=?',(article['title'],article['excerpt'],article['body'],'markdown',project['cover'],max(1,round(len(article['body'].split())/220)),now,post['id']))
cms.rebuild();client=cms.app.test_client()
for url,marker in [('/projects/launcher/','launcher-124-picker.webp'),('/blog/building-launcher-for-android/','A quieter interface in 1.2.3')]:
 response=client.get(url);assert response.status_code==200 and marker.encode() in response.data
for image in p['gallery']:
 with client.head(image['src']) as response:assert response.status_code==200,image['src']
cms.audit('Updated Launcher 1.2.4 copy, release presentation, three design previews, archived screenshots and blog article.')
print(json.dumps(dict(status='published',project='/projects/launcher/',article='/blog/building-launcher-for-android/',galleryItems=len(p['gallery']),newMockups=len(p['media']))),flush=True)
