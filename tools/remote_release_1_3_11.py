"""Apply the Android-only layout and usage correction as the Kevlog account."""
from pathlib import Path
from datetime import datetime,timezone
import os,sys,json,hashlib,secrets,shutil
os.environ['KEVLOG_DATA']='/var/lib/kevlog';sys.path.insert(0,'/opt/kevlog/app');import cms
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
meta=json.loads((stage/'release.json').read_text());assert meta['version']=='1.3.11' and meta['build']==21
source=stage/'Launcher.apk';assert source.stat().st_size==meta['bytes'] and hashlib.file_digest(source.open('rb'),'sha256').hexdigest()==meta['sha256']
projects=cms.config('projects.json');project=next(p for p in projects if p['slug']=='launcher')
assert not any(r.get('version')==meta['version'] for r in project.get('releases',[]))
old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
assert old and all(len(x)==32 and all(c in '0123456789abcdef' for c in x) for x in old)
assert not any(b.get('file_id') in old for p in projects if p['slug']!='launcher' for b in p.get('buttons',[]))
file_id=secrets.token_hex(16);(stage/'created.json').write_text(json.dumps([file_id]));target=cms.PROJECT_FILES/file_id
with source.open('rb') as data,target.open('xb') as out:shutil.copyfileobj(data,out)
target.chmod(0o640);meta['url']='https://kevlog.co.za/downloads/projects/launcher/'+file_id+'/'
project['buttons']=[dict(filename='Launcher.apk',file_id=file_id,label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note=f"v1.3.11 Ã‚Â· Android 7+ Ã‚Â· {meta['bytes']/1048576:.1f} MB")]+[b for b in project['buttons'] if not b.get('filename','').lower().endswith('.apk')]
project['notes']=[n.replace('Android release: 1.3.10, build 20.', 'Android release: 1.3.11, build 21.') for n in project.get('notes',[])]

now=datetime.now(timezone.utc).isoformat()
project.setdefault('releases',[]).append(dict(id='launcher-1-3-11',version='1.3.11',date='2026-11-14',status='published',kind='release',platforms='Android 7+',title='Finishing the details',summary='Balanced app icons, calmer card layouts, clearer conversations and more stable refreshes.',body='<p>App icons are sized from their visible artwork, rather than transparent margins. Drawer headings sit closer to the tiles, with touch feedback and matching fold timing.</p><p>Root cards share header and divider spacing. Focus choices are outlined. Secondary actions use a quieter colour while primary actions and important readings keep the accent.</p><p>Conversations adds date separators, tighter spacing for consecutive notifications from the same app and smaller reply controls. System notifications show the application name.</p><p>Sheets have consistent footers, keyboard clearance and a sensible maximum width on larger screens. Journey and reply forms keep their main actions in the footer. Duplicate settings titles are removed. The media seek handle has room at either end of its track.</p><p>Refreshes wait until touch and scrolling settle. Root and drawer anchors preserve the reading position; unchanged feed rows keep their loaded thumbnails.</p><p>Release compilation and Android emulator checks passed, including themes, larger text, page swipes, drawer folding, media controls, notification opening and dismissal, and feed preferences. No physical phone was used. Windows companion remains 1.2.0.</p>',created_at=now,updated_at=now))
post_body=None
with cms.connect() as db:
 db.execute('BEGIN IMMEDIATE');db.execute('UPDATE config SET value=? WHERE name=?',(json.dumps(projects,ensure_ascii=False),'projects.json'))
 post=db.execute('SELECT id,body FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone()
 if post:
  post_body=post['body'].replace('current downloads are Android 1.3.10','current downloads are Android 1.3.11')
  post_body=post_body.replace('The optional Phone resources card has battery, memory and storage charts, with recent readings collected while the card is open.','The optional Phone resources card shows current battery, memory and storage figures as donut charts. Tap a reading for the exact values.')
  detail="Typing narrows the full app list. You don't have to scroll or load another batch before an app can appear in the results. Launcher keeps the same app catalogue for the drawers and search, updating it when apps change.\n\n"
  marker='The other drawers can stay closed.\n\n'
  if detail not in post_body:post_body=post_body.replace(marker,marker+detail)
  db.execute('UPDATE posts SET body=?,updated_at=? WHERE id=?',(post_body,now,post['id']))
cms.rebuild();client=cms.app.test_client()
with client.head(meta['url'].removeprefix('https://kevlog.co.za')) as response:assert response.status_code==200 and response.content_length==meta['bytes']
for old_id in old:
 with client.head('/downloads/projects/launcher/'+old_id+'/') as response:assert response.status_code==404
cms.audit('Published Launcher 1.3.11 visual refinements and emulator-checked interactions; removed the previous live APK.')
print(json.dumps(dict(release=meta,old_file_ids=old,new_file_id=file_id,created_file_ids=[file_id],content=dict(project=project,postBody=post_body))),flush=True)
