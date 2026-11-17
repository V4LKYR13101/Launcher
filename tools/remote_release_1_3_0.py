"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.0' and meta['build']==10
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.0 · Android 7+ · {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.2.4, build 9.', 'Android release: 1.3.0, build 10.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-0',version='1.3.0',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='Built-in icon libraries and light-sensor themes',summary='Material, Phosphor and Bootstrap icons, optional automatic themes and a cleaner weather card.',body='<p>Google Material, Phosphor and Bootstrap icon libraries are bundled for offline selection. Material offers Outlined, Rounded, Filled and Sharp; Phosphor offers Light, Thin, Regular, Bold and Fill. Bootstrap includes outline, filled and brand icons. Search by name, choose an icon for an app or drawer, or select a global library. Unmatched apps retain their own artwork. Compatible installed icon packs remain available, with broader pack discovery and drawable browsing.</p><p>Settings includes Automatic theme. It uses only the ambient-light sensor, with separate Light and Dark lux thresholds and a configurable delay. It defaults off and runs only while Launcher is visible. The default thresholds are 1000 lux for Light and 300 lux for Dark, with a five-second delay. There is no camera permission or camera access. Choosing a theme manually turns automatic switching off.</p><p>The Root weather card no longer shows Refresh or City and units actions. Those controls remain in Weather settings.</p><p>The Android release compiled successfully. Device acceptance testing remains with the owner. The Windows companion remains version 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('install version 1.2.4','install version 1.3.0')
  detail="""## Icons and light-sensor themes in 1.3.0\n\nLauncher now includes Google Material, Phosphor and Bootstrap icon libraries. They work offline, with name search and choices for individual apps and drawer symbols. A global library can supply recognised app matches; other apps keep their own artwork. Compatible installed icon packs remain available. Material includes Outlined, Rounded, Filled and Sharp styles. Phosphor includes Light, Thin, Regular, Bold and Fill weights. Bootstrap includes outline, filled and brand icons.\n\nAutomatic theme is optional in Settings. The phone's ambient-light sensor can switch Launcher to Light above one lux threshold and back to Dark below another. A configurable delay prevents rapid changes. It defaults off, runs only while Launcher is visible and uses no camera. The initial settings are 1000 lux for Light, 300 lux for Dark and a five-second delay. Thresholds can be changed, and manually choosing a theme disables automatic switching.\n\nThe Root weather card also drops the Refresh and City and units actions. Location, units and manual refresh remain in Weather settings.\n\n"""
  if '## Icons and light-sensor themes in 1.3.0' not in post_body:post_body=post_body.replace('## A quieter interface in 1.2.3',detail+'## A quieter interface in 1.2.3')
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.0 bundled icon libraries and ambient-light themes; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
