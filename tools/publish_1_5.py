"""Publish the tested signing pair without removing the migration or prior downloads."""
from pathlib import Path
import hashlib,json,shlex,paramiko,re
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/release-1.5'
# Freeze the exact compiled source and APK identities before upload.
identity=json.loads((OUT/'source-sha256.json').read_text(encoding='utf-8-sig'))
expected={entry['path']:entry['sha256'].upper() for entry in identity}
actual={path.relative_to(ROOT).as_posix():hashlib.file_digest(path.open('rb'),'sha256').hexdigest().upper() for path in (ROOT/'src').rglob('*') if path.is_file() and not {'bin','obj'}.intersection(path.relative_to(ROOT).parts)}
assert actual==expected,'Source differs from the compiled release snapshot'
metadata={}
for kind,name in [('bridge','Launcher-bridge-1.5.0.apk'),('production','Launcher-1.5.1.apk')]:
    release=json.loads((OUT/(name+'.json')).read_text(encoding='utf-8-sig'));apk=OUT/name
    assert apk.stat().st_size==release['bytes'] and hashlib.file_digest(apk.open('rb'),'sha256').hexdigest().upper()==release['sha256'].upper()
    metadata[kind]={key:release[key] for key in ['version','build','bytes','sha256']}
password=(ROOT.parent/'Kevin/server_password.txt').read_text(encoding='utf-8-sig').strip()
client=paramiko.SSHClient();client.load_host_keys(str(Path.home()/'.ssh/known_hosts'));client.set_missing_host_key_policy(paramiko.RejectPolicy());client.connect('kevlog.co.za',username='kevin',password=password,timeout=20,look_for_keys=False,allow_agent=False)
def run(command,sudo=False):
    incoming,outgoing,error=client.exec_command(('sudo -S -p "" -- ' if sudo else '')+command,timeout=240)
    if sudo:incoming.write(password+'\n');incoming.flush()
    incoming.channel.shutdown_write();out=outgoing.read().decode();err=error.read().decode()
    if outgoing.channel.recv_exit_status():raise RuntimeError(err or out)
    return out.strip()
stage=run('mktemp -d /tmp/kevlog-launcher-update-XXXXXXXX');run('chmod 755 '+shlex.quote(stage))
wrapper=r'''
from pathlib import Path
import os,sys,json,sqlite3,shutil,subprocess,pwd
stage=Path(sys.argv[1]);assert stage.parent==Path('/tmp') and stage.name.startswith('kevlog-launcher-update-')
root=Path('/opt/kevlog/app');data=Path('/var/lib/kevlog');backup=Path('/var/www/kevlog/backups')/stage.name;backup.mkdir(mode=0o700);owner=pwd.getpwnam('kevlog');manifests=[root/'source/updates/launcher.json',root/'dist/updates/launcher.json']
subprocess.run(['systemctl','stop','kevlog'],check=True)
try:
    with sqlite3.connect(data/'portfolio.sqlite3') as source,sqlite3.connect(backup/'database.sqlite3') as target:source.backup(target)
    (backup/'database.sqlite3').chmod(0o600)
    for index,path in enumerate(manifests):
        if path.exists():shutil.copy2(path,backup/('manifest-'+str(index)+'.json'))
    previous=json.loads(manifests[0].read_text())
    result=subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python',str(stage/'apply.py'),str(stage)],capture_output=True,text=True,check=True)
    receipt=json.loads(result.stdout.strip().splitlines()[-1]);manifest=dict(previous);manifest['android']=receipt['releases']['bridge'];manifest['androidProduction']=receipt['releases']['production']
    for path in manifests:
        pending=path.with_suffix('.next');pending.write_text(json.dumps(manifest,indent=2)+'\n');pending.chmod(0o644);os.chown(pending,owner.pw_uid,owner.pw_gid);os.replace(pending,path)
    receipt.update(backup=str(backup),manifest=manifest);(stage/'receipt.json').write_text(json.dumps(receipt));print(json.dumps(receipt),flush=True)
except BaseException as error:
    if isinstance(error,subprocess.CalledProcessError):print(error.stderr,file=sys.stderr)
    if (backup/'database.sqlite3').exists():
        with sqlite3.connect(backup/'database.sqlite3') as source,sqlite3.connect(data/'portfolio.sqlite3') as target:source.backup(target)
    if (stage/'created.json').exists():
        for file_id in json.loads((stage/'created.json').read_text()):
            assert len(file_id)==32 and all(character in '0123456789abcdef' for character in file_id)
            (data/'uploads/project-files'/file_id).unlink(missing_ok=True)
    for index,path in enumerate(manifests):
        saved=backup/('manifest-'+str(index)+'.json')
        if saved.exists():shutil.copy2(saved,path)
    subprocess.run(['runuser','-u','kevlog','--','env','KEVLOG_DATA=/var/lib/kevlog','/opt/kevlog/venv/bin/python','-c',"import sys;sys.path.insert(0,'/opt/kevlog/app');import cms;cms.rebuild()"],check=True)
    raise
finally:subprocess.run(['systemctl','start','kevlog'],check=True)
'''
try:
    with client.open_sftp() as sftp:
        for name in ['Launcher-bridge-1.5.0.apk','Launcher-1.5.1.apk']:sftp.put(str(OUT/name),stage+'/'+name)
        sftp.put(str(ROOT/'tools/remote_release_1_5.py'),stage+'/apply.py')
        with sftp.open(stage+'/releases.json','w') as stream:stream.write(json.dumps(metadata))
    print('Uploaded both signed APKs; publishing the paired update feed.',flush=True)
    run('chown kevlog:kevlog '+shlex.quote(stage),True)
    receipt=json.loads(run('/opt/kevlog/venv/bin/python -c '+shlex.quote(wrapper)+' '+shlex.quote(stage),True));content=receipt.pop('content')
    (ROOT/'artifacts/published-release-1.5.1.json').write_text(json.dumps(receipt,indent=2)+'\n')
    local=ROOT.parent/'Kevin/site/source';projects_path=local/'projects.json';projects=json.loads(projects_path.read_text(encoding='utf-8-sig'))
    for project in projects:
        if project['slug']=='launcher':
            for key in ['buttons','notes','releases']:project[key]=content['project'][key]
    projects_path.write_text(json.dumps(projects,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(local/'updates/launcher.json').write_text(json.dumps(receipt['manifest'],indent=2)+'\n')
    posts_path=local/'blog-seed.json';posts=json.loads(posts_path.read_text(encoding='utf-8-sig'))
    for post in posts:
        if post['slug']=='building-launcher-for-android':
            for key in ['body','body_markdown']:
                if key in post:post[key]=re.sub(r'current downloads are Android \d+\.\d+\.\d+','current downloads are Android 1.5.1',post[key])
    posts_path.write_text(json.dumps(posts,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(receipt),flush=True)
finally:
    run('/usr/bin/python3 -c '+shlex.quote("from pathlib import Path;import sys;p=Path(sys.argv[1]);assert p.parent==Path('/tmp') and p.name.startswith('kevlog-launcher-update-');[(p/n).unlink(missing_ok=True) for n in ['Launcher-bridge-1.5.0.apk','Launcher-1.5.1.apk','apply.py','releases.json','created.json','receipt.json']];p.rmdir()")+' '+shlex.quote(stage),True)
    client.close()
