"""Publish Launcher binaries first, with database and updater rollback."""
from pathlib import Path
import ast,hashlib,json,shlex,paramiko
ROOT=Path(__file__).resolve().parents[1]
apk=ROOT/'artifacts/Launcher.apk';exe=ROOT/'artifacts/LauncherCompanion.exe'
def details(p):return dict(bytes=p.stat().st_size,sha256=hashlib.file_digest(p.open('rb'),'sha256').hexdigest())
meta=dict(version='1.2.0',build=5,**details(apk),notes='https://kevlog.co.za/projects/launcher/#release-launcher-1-2-0');desktop=dict(version='1.2.0',**details(exe))
# Reuse the established service stop, backup, atomic manifest and rollback flow.
tree=ast.parse((ROOT/'tools/publish_release.py').read_text(encoding='utf-8-sig'))
wrapper=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='WRAPPER' for t in n.targets))
wrapper=wrapper.replace("if new:(data/'uploads/project-files'/new).unlink(missing_ok=True)","""if (stage/'created.json').exists():
  for created in json.loads((stage/'created.json').read_text()):
   assert len(created)==32 and all(c in '0123456789abcdef' for c in created)
   (data/'uploads/project-files'/created).unlink(missing_ok=True)""")
password=(ROOT.parent/'Kevin/server_password.txt').read_text(encoding='utf-8-sig').strip()
client=paramiko.SSHClient();client.load_host_keys(str(Path.home()/'.ssh/known_hosts'));client.set_missing_host_key_policy(paramiko.RejectPolicy());client.connect('kevlog.co.za',username='kevin',password=password,timeout=20,look_for_keys=False,allow_agent=False)
def run(command,sudo=False):
 i,o,e=client.exec_command(('sudo -S -p "" -- ' if sudo else '')+command,timeout=240)
 if sudo:i.write(password+'\n');i.flush()
 i.channel.shutdown_write();out=o.read().decode();err=e.read().decode()
 if o.channel.recv_exit_status():raise RuntimeError(err or out)
 return out.strip()
stage=run('mktemp -d /tmp/kevlog-launcher-update-XXXXXXXX');run('chmod 755 '+shlex.quote(stage))
try:
 with client.open_sftp() as s:
  for path,name in [(apk,'Launcher.apk'),(exe,'LauncherCompanion.exe'),(ROOT/'tools/remote_release_1_2.py','apply.py')]:s.put(str(path),stage+'/'+name)
  for name,value in [('release.json',meta),('companion.json',desktop)]:
   with s.open(stage+'/'+name,'w') as f:f.write(json.dumps(value))
 print('Uploaded Launcher APK and Windows companion.',flush=True)
 # kevlog can write its list of created files for rollback without a world-writable stage.
 run('chown kevlog:kevlog '+shlex.quote(stage),True)
 output=run('/opt/kevlog/venv/bin/python -c '+shlex.quote(wrapper)+' '+shlex.quote(stage),True);receipt=json.loads(output)
 print(output,flush=True);(ROOT/'artifacts/published-release-1.2.0.json').write_text(output+'\n',encoding='utf-8')
 (ROOT.parent/'Kevin/site/source/updates/launcher.json').write_text(json.dumps({'android':receipt['release']},indent=2)+'\n',encoding='utf-8')
finally:
 # The folder is owned by the service account after publication; root removes only
 # the explicitly named staging files inside this validated mktemp directory.
 run("/usr/bin/python3 -c "+shlex.quote("from pathlib import Path;import sys;p=Path(sys.argv[1]);assert p.parent==Path('/tmp') and p.name.startswith('kevlog-launcher-update-');[(p/n).unlink(missing_ok=True) for n in ['Launcher.apk','LauncherCompanion.exe','apply.py','release.json','companion.json','created.json']];p.rmdir()")+' '+shlex.quote(stage),True)
 client.close()
