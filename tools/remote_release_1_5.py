"""Publish the migration pair, preserving every currently served download."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil,re
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
metadata=json.loads((stage/'releases.json').read_text());assert metadata['bridge']['build']==26 and metadata['production']['build']==27
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')=='1.5.1' for r in project.get('releases',[])), 'Release 1.5.1 is already published'
old_ids=[button['file_id'] for button in project.get('buttons',[]) if button.get('file_id')]
created=[];releases={}
for kind,filename in [('bridge','Launcher-bridge-1.5.0.apk'),('production','Launcher-1.5.1.apk')]:
    release=dict(metadata[kind]);source=stage/filename
    assert source.stat().st_size==release['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest().upper()==release['sha256'].upper()
    file_id=secrets.token_hex(16);created.append(file_id);(stage/'created.json').write_text(json.dumps(created))
    target=cms.PROJECT_FILES/file_id
    with source.open('rb') as incoming,target.open('xb') as outgoing:shutil.copyfileobj(incoming,outgoing)
    target.chmod(0o640);release['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
    release['notes']='https://kevlog.co.za/projects/launcher/#release-launcher-1-5-1';releases[kind]=release
buttons=[]
for kind,filename,label,note in [
    ('production','Launcher-1.5.1.apk','Download Launcher 1.5.1','New install or second update · Android 7+ · 64-bit'),
    ('bridge','Launcher-bridge-1.5.0.apk','Update existing Launcher · step 1','Install 1.5.0 first when updating an older installation, then install 1.5.1.')]:
    release=releases[kind];buttons.append(dict(filename=filename,file_id=release['url'].rstrip('/').split('/')[-1],label=label,icon='file_download',colour='#e4ff00',text_colour='#11120f',note=note))
for button in project.get('buttons',[]):
    item=dict(button)
    if item.get('filename','').lower().endswith('.apk'):item['label']='Previous Android APK · 1.4.1'
    buttons.append(item)
project['buttons']=buttons
project['notes']=[re.sub(r'Android release: [^.]+\.[^.]+\.[^,]+, build \d+\.', 'Android release: 1.5.1, build 27.',note) for note in project.get('notes',[])]
project['notes']=[note for note in project['notes'] if not note.startswith('Signing migration:')]
project['notes'].append('Signing migration: existing installations update to 1.5.0 first, then 1.5.1. Keep Launcher installed to preserve its data. Fresh installations can use 1.5.1 directly.')
now=datetime.now(timezone.utc).isoformat()
body='<p>A more expressive Today page uses your theme and accent, with layered surfaces and motion that respects device capabilities. The overview and other Today cards can be hidden, removed and rearranged alongside Android widgets. Choose individual page-tab buttons in Appearance, or hide the entire tab bar. Today now has a calendar icon.</p><p>New native tools include calendar agenda, checklists, reminders with date and time pickers, shared text and link capture, app-shortcut pinning, work-profile handling and a separate Private Space container. Search includes saved notes and other local content. Reading adds saved stories and read/unread controls. Layout editing includes drag controls, arrows, favorites ordering and Undo.</p><p>This release also hardens state recovery, note saving, protected notification previews and independent timer/reminder alerts. It passed 100 core behavior checks and warning-free Release compilation. It is published for device testing while further native and OEM checks continue.</p><p>Existing installations need the 1.5.0 migration update before 1.5.1 through the in-app updater. Android 9 and newer then use the dedicated production certificate with verified signing lineage. Android 7–8 retains the compatible original signer. Both APKs contain only 64-bit ARM and x86 builds. The Windows companion download is retained.</p>'
project.setdefault('releases',[]).append(dict(id='launcher-1-5-1',version='1.5.1',date='2026-11-15',status='published',kind='release',platforms='Android 7+ · 64-bit',title='A more expressive home, with native tools',summary='Themed depth, configurable page tabs, richer Today cards and hardened storage and alerts. Published for device testing.',body=body,created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
    db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
    post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
    if post:
        post_body=re.sub(r'current downloads are Android \d+\.\d+\.\d+','current downloads are Android 1.5.1',post['body'])
        db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
for release in releases.values():
    with client.head(release['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==release['bytes']
for file_id in old_ids:
    with client.head('/downloads/projects/launcher/'+file_id+'/') as response:assert response.status_code==200
cms.audit('Published Launcher 1.5.1 and its 1.5.0 signing migration bridge; retained previous download URLs and the Windows companion.')
print(json.dumps(dict(releases=releases,old_file_ids=old_ids,created_file_ids=created,content=dict(project=project,postBody=post_body))),flush=True)
