"""Prepare, publish and mirror the scoped Launcher content refresh."""
from pathlib import Path
import json,shlex,paramiko,hashlib,ftfy,shutil
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'artifacts/showcase-1.4.1'
password=(ROOT.parent/'Kevin/server_password.txt').read_text(encoding='utf-8-sig').strip()
client=paramiko.SSHClient();client.load_host_keys(str(Path.home()/'.ssh/known_hosts'));client.set_missing_host_key_policy(paramiko.RejectPolicy());client.connect('kevlog.co.za',username='kevin',password=password,timeout=20,look_for_keys=False,allow_agent=False)
def run(command,sudo=False):
 i,o,e=client.exec_command(('sudo -S -p "" -- ' if sudo else '')+command,timeout=240)
 if sudo:i.write(password+'\n');i.flush()
 i.channel.shutdown_write();out=o.read().decode();err=e.read().decode()
 if o.channel.recv_exit_status():raise RuntimeError(err or out)
 return out.strip()
read="""import sys,json;sys.path.insert(0,'/opt/kevlog/app');import cms
with cms.connect() as db:post=dict(db.execute('SELECT * FROM posts WHERE slug=?',('building-launcher-for-android',)).fetchone())
print(json.dumps(dict(project=next(p for p in cms.config('projects.json') if p['slug']=='launcher'),gallery=cms.config('galleries.json')['launcher'],post=post)))"""
snapshot=json.loads(run('runuser -u kevlog -- env KEVLOG_DATA=/var/lib/kevlog /opt/kevlog/venv/bin/python -c '+shlex.quote(read),True));(ART/'before-content.json').write_text(json.dumps(snapshot,ensure_ascii=False,indent=2),encoding='utf-8')
def repair(value):
 if isinstance(value,str):return ftfy.fix_text(value).replace('\u2014',' - ')
 if isinstance(value,list):return [repair(v) for v in value]
 if isinstance(value,dict):return {k:repair(v) for k,v in value.items()}
 return value
project=repair(snapshot['project']);assert project['buttons'][0]['note'].startswith('v1.4.1')
new=json.loads((ART/'gallery.json').read_text());new_paths={g['src'] for g in new}
gallery=[repair(g) for g in snapshot['gallery'] if g['src'] not in new_paths]
older=[g for g in gallery if g['src']=='/media/launcher-136-resources.webp'];gallery=[g for g in gallery if g not in older]
for g in older:g['caption']='Archive 1.3.6: the previous resource history chart';g['heroEnabled']=False
insert=next((i for i,g in enumerate(gallery) if g['src']=='/media/launcher-138-layers.webp'),4)+1
gallery[insert:insert]=new
archive=next((i for i,g in enumerate(gallery) if g.get('caption','').startswith('Archive')),len(gallery));gallery[archive:archive]=older
assert gallery[0]['src']=='/media/launcher-interface-overview.webp' and gallery[0]['heroEnabled']
post=dict(title=snapshot['post']['title'],excerpt=snapshot['post']['excerpt'],body=repair(snapshot['post']['body']))
image='![Launcher tile icon size controls.](/media/launcher-141-icon-size.webp)\n\n*Actual Android emulator captures in a mockup frame.*\n\n'
marker='## Installing it'
addition="Tile icon size is separate from tile height. In Appearance, a slider lets you make app icons smaller or larger, with a preview before you save. The default stays at 100 percent. Icons fit the space available inside each tile so labels and usage times stay clear.\n\n"
if image not in post['body']:
 assert marker in post['body']
 post['body']=post['body'].replace(marker,addition+image+marker)
(ART/'gallery.json').write_text(json.dumps(gallery,ensure_ascii=False,indent=2),encoding='utf-8');(ART/'article.md').write_text(post['body'],encoding='utf-8')

media={p.name:hashlib.file_digest(p.open('rb'),'sha256').hexdigest() for p in (ART/'media').glob('*.webp')}
payload=dict(project=project,gallery=gallery,post=post,media=media,expectedButtons=snapshot['project']['buttons'],expectedGallery=snapshot['gallery'],expectedBody=snapshot['post']['body'])
assert '\u2014' not in json.dumps(dict(project=project,gallery=gallery,post=post),ensure_ascii=False)
(ART/'content.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
stage=run('mktemp -d /tmp/kevlog-launcher-content-XXXXXXXX');run('chmod 755 '+shlex.quote(stage))
wrapper=r'''
from pathlib import Path
import sqlite3,subprocess,sys,json,shutil,os
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-content-')
data=Path('/var/lib/kevlog');backup=Path('/var/www/kevlog/backups')/stage.name;backup.mkdir(mode=0o700)
subprocess.run(['systemctl','stop','kevlog'],check=True)
try:
 with sqlite3.connect(data/'portfolio.sqlite3') as source,sqlite3.connect(backup/'database.sqlite3') as target:source.backup(target)
 os.chmod(backup/'database.sqlite3',0o600)
 result=subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python',str(stage/'apply.py'),str(stage)],capture_output=True,text=True,check=True)
 receipt=json.loads(result.stdout.strip().splitlines()[-1]);receipt['backup']=str(backup);print(json.dumps(receipt),flush=True)
except BaseException as error:
 if isinstance(error,subprocess.CalledProcessError):print(error.stderr,file=sys.stderr)
 with sqlite3.connect(backup/'database.sqlite3') as source,sqlite3.connect(data/'portfolio.sqlite3') as target:source.backup(target)
 subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python','-c',"import sys;sys.path.insert(0,'/opt/kevlog/app');import cms;cms.rebuild()"],check=True)
 raise
finally:subprocess.run(['systemctl','start','kevlog'],check=True)
'''
files={name:ART/'media'/name for name in media};files.update({'content.json':ART/'content.json','apply.py':ROOT/'tools/remote_refresh_content_1_4_1.py'})
try:
 with client.open_sftp() as s:
  for name,path in files.items():s.put(str(path),stage+'/'+name)
 receipt=json.loads(run('/opt/kevlog/venv/bin/python -c '+shlex.quote(wrapper)+' '+shlex.quote(stage),True))
 (ART/'published.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8');print(json.dumps(receipt),flush=True)
 site=ROOT.parent/'Kevin/site'
 for filename,update in [('projects.json',project),('galleries.json',gallery),('blog-seed.json',post)]:
  path=site/'source'/filename;data=json.loads(path.read_text(encoding='utf-8-sig'))
  if filename=='projects.json':data=[project if p['slug']=='launcher' else p for p in data]
  elif filename=='galleries.json':data['launcher']=gallery
  else:
   data=[{**p,**post,'body_markdown':post['body'],'cover':project['cover']} if p['slug']=='building-launcher-for-android' else p for p in data]
  path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 mirror=site/'launcher-showcase-1.4.1';mirror.mkdir(exist_ok=True)
 for name in ['content.json','article.md','gallery.json','published.json']:shutil.copy2(ART/name,mirror/name)
 images=ROOT/'featureImages/releases/1.4.1';images.mkdir(parents=True,exist_ok=True)
 for source in (ART/'media').glob('*.webp'):shutil.copy2(source,images/source.name)
 (images/'INDEX.md').write_text('# Launcher interface presentation\n\nTile icon size captures from the Android emulator. Device frames present the actual live preview at 60 and 150 percent. These are not physical phone captures. The interface overview remains the main project cover.\n\n'+''.join('- ['+p.name+']('+p.name+')\n' for p in sorted(images.glob('*.webp'))),encoding='utf-8')
finally:
 with client.open_sftp() as s:
  for name in files:
   try:s.remove(stage+'/'+name)
   except FileNotFoundError:pass
  s.rmdir(stage)
 client.close()
