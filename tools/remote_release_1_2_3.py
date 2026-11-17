"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.2.3' and meta['build']==8
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.2.3 · Android 7+ · {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.2.2, build 7.', 'Android release: 1.2.3, build 8.') for n in project.get('notes',[])]
project['notes'].append('Version 1.2.3 refines shared controls, sheets, drawer typography, app dashboards and notification cards. News & feeds combines optional RSS sources in publication-time order. Settings includes feed management and an optional drawer-heading drag gesture.')
now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-2-3',version='1.2.3',date='2026-11-13',status='published',kind='release',platforms='Android 7+',title='A calmer interface and one news feed',summary='Shared interface refinement, reliable source-app opening and a single chronological RSS card.',body='<p>Buttons, sheet headings, choice lists, settings rows and notification cards now use consistent spacing and touch feedback. Add card uses an icon grid. Drawer headings are smaller, dark surfaces are quieter, and wallpaper dimming is adjustable. Blank app icon masks fall back to native artwork or initials. Locked tiles use a scaled fingerprint shape rather than a spiral.</p><p>The app dashboard has Today and Last 7 days tabs and a compact QR preview. Tile appearance options show visual previews. Conversations and the quick panel share their notification card layout. Tapping a notification opens its source app, with a native notification-action fallback where needed and the existing biometric gate.</p><p>News & feeds combines enabled RSS/Atom sources in one Root card, newest first. Google News South Africa starts enabled; MyBroadband, BBC World, The Verge and Ars Technica are optional presets. Add, edit, remove or switch sources in Settings or Add card. RSS needs no API key. The reader follows HTTPS redirects, recognises published feed links and shows clearer errors.</p><p>Settings, Drawers includes Drag headings to fold. It defaults off so normal scrolling is uninterrupted; tap-to-fold remains available.</p><p>Release compilation and publication checks passed. The preset feed endpoints were inspected outside the app. Device acceptance testing remains with the owner. The Windows companion stays on 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('install version 1.2.2','install version 1.2.3')
  detail='## A quieter interface in 1.2.3\n\nThe next pass brings consistent spacing to buttons, sheets, settings, the card picker and notification cards. Add card now has icons and short descriptions. Drawer headings are smaller, the dashboard switches between Today and Last 7 days, and tile appearance choices have previews. Dark surfaces are quieter, and wallpaper dimming is adjustable. Locked apps use a fingerprint shape instead of the earlier spiral. Notification taps open the source app while keeping the Launcher biometric check.\n\nNews & feeds now combines enabled sources in one card, sorted by publication time. Google News South Africa is enabled initially. MyBroadband, BBC World, The Verge and Ars Technica are optional presets, with custom RSS and Atom sources supported too. Add and manage them from Settings or Add card. No API key is needed for feeds.\n\nThe drawer-heading fold gesture can be enabled or disabled in Settings, Drawers. It defaults off; tapping to fold still works.\n\n'
  if '## A quieter interface in 1.2.3' not in post_body:post_body=post_body.replace('## A cleaner layout in 1.2.1',detail+'## A cleaner layout in 1.2.1')
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.2.3 interface, notification and combined RSS update; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
