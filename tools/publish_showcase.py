"""Publish only the new Launcher project, article, APK and media to Kevlog."""
from pathlib import Path
import json,shlex,paramiko
ROOT=Path(__file__).resolve().parents[1];HERE=ROOT/'artifacts/showcase'
password=(ROOT.parent/'Kevin/server_password.txt').read_text(encoding='utf-8-sig').strip()
payload=json.loads((HERE/'content.json').read_text(encoding='utf-8'))
files={p.name:p for p in (HERE/'media').glob('*.webp')}
files.update({'Launcher.apk':ROOT/'artifacts/Launcher.apk','content.json':HERE/'content.json','publish.py':ROOT/'tools/remote_publish_showcase.py'})
client=paramiko.SSHClient();client.load_host_keys(str(Path.home()/'.ssh/known_hosts'));client.set_missing_host_key_policy(paramiko.RejectPolicy())
client.connect('kevlog.co.za',username='kevin',password=password,timeout=20,look_for_keys=False,allow_agent=False)
def run(command,sudo=False):
    i,o,e=client.exec_command(('sudo -S -p "" -- ' if sudo else '')+command,timeout=180)
    if sudo:i.write(password+'\n');i.flush()
    i.channel.shutdown_write();out=o.read().decode();err=e.read().decode()
    if o.channel.recv_exit_status():raise RuntimeError(err or out)
    return out.strip()
stage=run('mktemp -d /tmp/kevlog-launcher-XXXXXXXX');run('chmod 755 '+shlex.quote(stage))
try:
    with client.open_sftp() as sftp:
        for name,path in files.items():sftp.put(str(path),stage+'/'+name)
    print('Uploaded reviewed Launcher content and APK.',flush=True)
    result=run('/opt/kevlog/venv/bin/python '+shlex.quote(stage+'/publish.py')+' '+shlex.quote(stage),True)
    print(result,flush=True);(HERE/'published.json').write_text(result+'\n',encoding='utf-8')
    published=json.loads(result)
    site=ROOT.parent/'Kevin/site';dest=site/'launcher-showcase';dest.mkdir(exist_ok=True)
    for name in ['content.json','article.md','artwork.json','published.json']:(dest/name).write_bytes((HERE/name).read_bytes())
    (site/'source/updates/launcher.json').write_text(json.dumps({'android':published['release']},indent=2)+'\n',encoding='utf-8')
    for name,key in [('projects.json','project'),('galleries.json','gallery'),('blog-seed.json','post')]:
        path=site/'source'/name;data=json.loads(path.read_text(encoding='utf-8'))
        if key=='project':
            payload[key]['buttons'][0]['file_id']=published['release']['url'].rstrip('/').split('/')[-1]
            data=[payload[key],*data]
        elif key=='gallery':data['launcher']=payload[key]
        else:data=[{**payload[key],'body_markdown':payload[key]['body']},*data]
        path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
finally:
    with client.open_sftp() as sftp:
        for name in files:
            try:sftp.remove(stage+'/'+name)
            except FileNotFoundError:pass
        sftp.rmdir(stage)
    client.close()
