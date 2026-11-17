"""Replace the live Launcher APK, manifest and release notes with rollback."""
from pathlib import Path
import hashlib,json,shlex,paramiko
ROOT=Path(__file__).resolve().parents[1];APK=ROOT/'artifacts/Launcher.apk'
password=(ROOT.parent/'Kevin/server_password.txt').read_text(encoding='utf-8-sig').strip()
meta=dict(version='1.1.1',build=4,bytes=APK.stat().st_size,sha256=hashlib.sha256(APK.read_bytes()).hexdigest(),notes='https://kevlog.co.za/projects/launcher/#release-launcher-1-1-1')
client=paramiko.SSHClient();client.load_host_keys(str(Path.home()/'.ssh/known_hosts'));client.set_missing_host_key_policy(paramiko.RejectPolicy());client.connect('kevlog.co.za',username='kevin',password=password,timeout=20,look_for_keys=False,allow_agent=False)
def run(command,sudo=False):
 i,o,e=client.exec_command(('sudo -S -p "" -- ' if sudo else '')+command,timeout=180)
 if sudo:i.write(password+'\n');i.flush()
 i.channel.shutdown_write();out=o.read().decode();err=e.read().decode()
 if o.channel.recv_exit_status():raise RuntimeError(err or out)
 return out.strip()
stage=run('mktemp -d /tmp/kevlog-launcher-update-XXXXXXXX');run('chmod 755 '+shlex.quote(stage))
WRAPPER=r'''
import pathlib,sys,sqlite3,json,subprocess,shutil,os,pwd
stage=pathlib.Path(sys.argv[1]);assert stage.parent==pathlib.Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
data=pathlib.Path('/var/lib/kevlog');root=pathlib.Path('/opt/kevlog/app').resolve();backup=pathlib.Path('/var/www/kevlog/backups')/stage.name;backup.mkdir(mode=0o700)
owner=pwd.getpwnam('kevlog');manifests=[root/'source/updates/launcher.json',root/'dist/updates/launcher.json']
old=[];new=None;subprocess.run(['systemctl','stop','kevlog'],check=True)
try:
 with sqlite3.connect(data/'portfolio.sqlite3') as source,sqlite3.connect(backup/'database.sqlite3') as target:source.backup(target)
 os.chmod(backup/'database.sqlite3',0o600)
 with sqlite3.connect(data/'portfolio.sqlite3') as db:
  projects=json.loads(db.execute('SELECT value FROM config WHERE name=?',('projects.json',)).fetchone()[0]);project=next(p for p in projects if p['slug']=='launcher')
 old=[b['file_id'] for b in project.get('buttons',[]) if b.get('filename','').lower().endswith('.apk') and b.get('file_id')]
 for file_id in old:
  assert len(file_id)==32 and all(c in '0123456789abcdef' for c in file_id)
  shutil.copy2(data/'uploads/project-files'/file_id,backup/(file_id+'.apk'))
 for i,path in enumerate(manifests):
  if path.exists():shutil.copy2(path,backup/f'manifest-{i}.json')
 result=subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python',str(stage/'apply.py'),str(stage)],capture_output=True,text=True,check=True)
 receipt=json.loads(result.stdout.strip().splitlines()[-1]);new=receipt['new_file_id'];assert receipt['old_file_ids']==old
 for path in manifests:
  path.parent.mkdir(parents=True,exist_ok=True);pending=path.with_suffix('.next');pending.write_text(json.dumps({'android':receipt['release']},indent=2)+'\n');pending.chmod(0o644);os.chown(pending,owner.pw_uid,owner.pw_gid);os.replace(pending,path)
 # The old files are retained only in the private rollback backup, never in live downloads.
 for file_id in old:(data/'uploads/project-files'/file_id).unlink()
 receipt.update(backup=str(backup),old_apks_removed=len(old));print(json.dumps(receipt),flush=True)
except BaseException as error:
 if isinstance(error,subprocess.CalledProcessError):print(error.stderr,file=sys.stderr)
 with sqlite3.connect(backup/'database.sqlite3') as source,sqlite3.connect(data/'portfolio.sqlite3') as target:source.backup(target)
 for file_id in old:
  if not (data/'uploads/project-files'/file_id).exists():shutil.copy2(backup/(file_id+'.apk'),data/'uploads/project-files'/file_id);os.chown(data/'uploads/project-files'/file_id,owner.pw_uid,owner.pw_gid)
 if new:(data/'uploads/project-files'/new).unlink(missing_ok=True)
 for i,path in enumerate(manifests):
  if (backup/f'manifest-{i}.json').exists():shutil.copy2(backup/f'manifest-{i}.json',path)
 subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python','-c',"import sys;sys.path.insert(0,'/opt/kevlog/app');import cms;cms.rebuild()"],check=True)
 raise
finally:subprocess.run(['systemctl','start','kevlog'],check=True)
'''
try:
 with client.open_sftp() as s:
  s.put(str(APK),stage+'/Launcher.apk');s.put(str(ROOT/'tools/remote_update_release.py'),stage+'/apply.py')
  with s.open(stage+'/release.json','w') as f:f.write(json.dumps(meta))
 print('Uploaded Launcher 1.1.1.',flush=True)
 output=run('/opt/kevlog/venv/bin/python -c '+shlex.quote(WRAPPER)+' '+shlex.quote(stage),True)
 print(output,flush=True);receipt=json.loads(output)
 (ROOT/'artifacts/published-release-1.1.1.json').write_text(output+'\n',encoding='utf-8')
 source=ROOT.parent/'Kevin/site/source/updates/launcher.json';source.write_text(json.dumps({'android':receipt['release']},indent=2)+'\n',encoding='utf-8')
finally:
 with client.open_sftp() as s:
  for name in ['Launcher.apk','apply.py','release.json']:
   try:s.remove(stage+'/'+name)
   except FileNotFoundError:pass
  s.rmdir(stage)
 client.close()
