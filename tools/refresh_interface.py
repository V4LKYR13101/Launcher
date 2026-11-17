"""Prepare, publish and mirror the scoped Launcher content refresh."""
from pathlib import Path
import json,shlex,paramiko,hashlib,ftfy,shutil
ROOT=Path(__file__).resolve().parents[1];ART=ROOT/'artifacts/kevlog-interface-refresh'
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
project=repair(snapshot['project']);project.update(json.loads((ART/'project-copy.json').read_text(encoding='utf-8')))
assert project['buttons']==snapshot['project']['buttons']
order=json.loads((ART/'gallery-order.json').read_text());new=order['new'];new_paths={g['src'] for g in new};priority={name:i for i,name in enumerate(order['priority'])}
retired={'/media/launcher-interface-home.webp','/media/launcher-interface-apps.webp'}
old=[repair(g) for g in snapshot['gallery'] if g['src'] not in new_paths and g['src'] not in retired]
def rank(pair):
 i,g=pair;name=g['src'].rsplit('/',1)[-1]
 if name.startswith('launcher-screen-'):return 3000+i
 if g.get('caption','').startswith('Archive'):return 2000+i
 if name=='launcher-130-icons.webp':return 900
 return priority.get(name,200+i)
gallery=new+[g for _,g in sorted(enumerate(old),key=rank)]
for i,g in enumerate(gallery):g['heroEnabled']=i==0
captions={'launcher-121-root.webp':'Root: notes, timers and everyday cards','launcher-121-messages.webp':'Conversations: notifications filtered by app','launcher-138-layers.webp':'Optional movement across the interface','launcher-136-resources.webp':'Root: phone resource charts','launcher-135-feeds.webp':'Root: news with a cleaner reading layout','launcher-130-icons.webp':'Icon options: Material, Phosphor and Bootstrap','launcher-130-light.webp':'Appearance: optional light-sensor themes'}
for g in gallery:
 if g['src'].rsplit('/',1)[-1] in captions:g['caption']=captions[g['src'].rsplit('/',1)[-1]]
post=json.loads((ART/'post-copy.json').read_text());post['body']=(ART/'article.md').read_text(encoding='utf-8')
(ART/'gallery.json').write_text(json.dumps(gallery,ensure_ascii=False,indent=2),encoding='utf-8')
assert len(gallery)==len({g['src'] for g in gallery})
assert project['cover']==project['banner']==project['socialImage']==gallery[0]['src']=='/media/launcher-interface-overview.webp'
assert not any('icon' in post[k].lower() for k in ['title','excerpt'])
assert post['body'].index('## Appearance and movement')>post['body'].index('## Conversations')

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
files={name:ART/'media'/name for name in media};files.update({'content.json':ART/'content.json','apply.py':ROOT/'tools/remote_refresh_interface.py'})
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
 mirror=site/'launcher-interface-refresh';mirror.mkdir(exist_ok=True)
 for name in ['content.json','article.md','gallery.json','published.json']:shutil.copy2(ART/name,mirror/name)
 images=ROOT/'featureImages/kevlog-interface-refresh';images.mkdir(parents=True,exist_ok=True)
 for source in (ART/'media').glob('*.webp'):shutil.copy2(source,images/source.name)
 (images/'INDEX.md').write_text('# Launcher interface presentation\n\nInterface-led project and article covers. Labelled illustrations with sample content, not device screenshots.\n\n'+''.join('- ['+p.name+']('+p.name+')\n' for p in sorted(images.glob('*.webp'))),encoding='utf-8')
finally:
 with client.open_sftp() as s:
  for name in files:
   try:s.remove(stage+'/'+name)
   except FileNotFoundError:pass
  s.rmdir(stage)
 client.close()
