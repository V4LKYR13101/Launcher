"""Run only in the validated temporary Kevlog upload directory."""
import sys,os,pathlib,json,sqlite3,subprocess,shutil,hashlib,secrets
from datetime import datetime,timezone
stage=pathlib.Path(sys.argv[1]);assert stage.parent==pathlib.Path('/tmp') and stage.name.startswith('kevlog-launcher-')
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
p=json.loads((stage/'content.json').read_text());assert p['project']['slug']=='launcher' and p['post']['slug']=='building-launcher-for-android'
src=stage/'Launcher.apk';meta=p['release'];assert src.stat().st_size==meta['bytes'] and hashlib.file_digest(src.open('rb'),'sha256').hexdigest()==meta['sha256']
assert src.open('rb').read(4)==b'PK\x03\x04'
backup=pathlib.Path('/var/www/kevlog/backups')/stage.name;backup.mkdir(mode=0o700)
created=[];owner=cms.UPLOADS.stat();subprocess.run(['systemctl','stop','kevlog'],check=True)
try:
    with sqlite3.connect(cms.DB) as source,sqlite3.connect(backup/'database.sqlite3') as target:source.backup(target)
    os.chmod(backup/'database.sqlite3',0o600)
    projects=cms.config('projects.json');galleries=cms.config('galleries.json')
    assert not any(x['slug']=='launcher' for x in projects) and 'launcher' not in galleries,'Launcher already exists; refusing duplicate import.'
    with cms.connect() as db:assert not db.execute('SELECT 1 FROM posts WHERE slug=?',(p['post']['slug'],)).fetchone()
    file_id=secrets.token_hex(16);download=f'/downloads/projects/launcher/{file_id}/';meta['url']='https://kevlog.co.za'+download
    p['project']['buttons'][0]['file_id']=file_id
    files=[(src,cms.PROJECT_FILES/file_id)]
    for image in p['gallery']:
        name=pathlib.Path(image['src']).name;assert image['src']=='/media/'+name and name.startswith('launcher-') and name.endswith('.webp')
        files.append((stage/name,cms.UPLOADS/name))
    for source,target in files:
        with source.open('rb') as data,target.open('xb') as out:created.append(target);shutil.copyfileobj(data,out)
        os.chown(target,owner.st_uid,owner.st_gid);target.chmod(0o640 if target.parent==cms.PROJECT_FILES else 0o644)
        assert hashlib.file_digest(source.open('rb'),'sha256').digest()==hashlib.file_digest(target.open('rb'),'sha256').digest()
    post=p['post']
    with cms.connect() as db:
        db.execute('BEGIN IMMEDIATE')
        for name,value in [('projects.json',[p['project'],*projects]),('galleries.json',{**galleries,'launcher':p['gallery']})]:
            db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(value,ensure_ascii=False),name))
        db.execute('INSERT INTO posts(title,slug,excerpt,body,format,cover,published_at,status,reading_minutes,updated_at,section,tags) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)',
            (post['title'],post['slug'],post['excerpt'],post['body'],'markdown',post['cover'],post['published_at'],'published',post['reading_minutes'],datetime.now(timezone.utc).isoformat(),post['section'],json.dumps(post['tags'])))
    cms.rebuild()
    # Imports run under sudo for the service/backup operation. Generated render
    # directories must remain writable by the service account on its next boot.
    for folder in [cms.DATA/'render-next',cms.RENDERED]:
        for target in [folder,*folder.rglob('*')]:
            if target.stat().st_uid==0:os.chown(target,owner.st_uid,owner.st_gid)
    for folder in [cms.ROOT/'source/updates',cms.ROOT/'dist/updates']:
        folder.mkdir(parents=True,exist_ok=True);target=folder/'launcher.json'
        with target.open('x') as file:created.append(target);file.write(json.dumps({'android':meta},indent=2)+'\n')
        target.chmod(0o644)
    client=cms.app.test_client()
    for route,marker in [('/projects/launcher/',download),('/blog/'+post['slug']+'/',post['title'])]:
        response=client.get(route);assert response.status_code==200 and marker.encode() in response.data,route
    for image in p['gallery']:
        with client.head(image['src']) as response:assert response.status_code==200,image['src']
    with client.head(download) as response:assert response.status_code==200 and response.content_length==meta['bytes']
    cms.audit('Published Launcher 1.0.1, Android APK, update manifest, article and product gallery.')
    print(json.dumps(dict(status='published',project='/projects/launcher/',article='/blog/'+post['slug']+'/',galleryItems=len(p['gallery']),release=meta,backup=str(backup))),flush=True)
except BaseException:
    if (backup/'database.sqlite3').exists():
        with sqlite3.connect(backup/'database.sqlite3') as source,sqlite3.connect(cms.DB) as target:source.backup(target)
    for target in created:target.unlink(missing_ok=True)
    cms.rebuild();raise
finally:subprocess.run(['systemctl','start','kevlog'],check=True)
