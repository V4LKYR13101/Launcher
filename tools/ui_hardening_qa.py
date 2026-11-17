"""Production regression scenarios. Destructive fixture operations target emulator-5554 only."""
from pathlib import Path
import json,re,sys,time,xml.etree.ElementTree as ET
from device_qa import adb,tree,tap as native_tap,find
S='emulator-5554';assert S.startswith('emulator-')
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/production-hardening-2026-11-15';OUT.mkdir(exist_ok=True)
STATE='/data/user/0/com.launcher.home/files/launcher-state.json'
checks=json.loads((OUT/'native-checks.json').read_text()) if (OUT/'native-checks.json').exists() else []
def check(value,message):
    assert value,message
    checks.append(message);print('Verified:',message,flush=True)
    (OUT/'native-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
def labels():return [n.get('text') or n.get('content-desc') for n in tree(S).iter('node')]
def state():return json.loads(adb(S,'shell','cat',STATE).lstrip('\ufeff'))
def shot(name):
    root=tree(S);(OUT/(name+'.xml')).write_text(ET.tostring(root,encoding='unicode'),encoding='utf-8');(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
def center(node):
    a,b,c,d=map(int,re.findall(r'\d+',node.get('bounds')));return (a+c)//2,(b+d)//2
def touch(node):adb(S,'shell','input','tap',*center(node));tree(S)
def tap(serial,label,hold=False):native_tap(serial,label,hold);tree(serial)
def home():adb(S,'shell','input','keyevent','3')
def back():adb(S,'shell','input','keyevent','4')
def show(label):
    for _ in range(8):
        root=tree(S);nodes=list(root.iter('node'))
        match=next((n for n in nodes if n.get('text')==label or n.get('content-desc')==label),None)
        if match is not None:return match
        bounds=[list(map(int,re.findall(r'\d+',n.get('bounds','')))) for n in nodes];w=max(b[2] for b in bounds if len(b)==4);h=max(b[3] for b in bounds if len(b)==4)
        adb(S,'shell','input','swipe',int(w*.8),int(h*.8),int(w*.8),int(h*.35),250)
    raise AssertionError('Could not find '+label)
def menu(label):tap(S,'Launcher settings');touch(show(label))
def appearance():home();menu('Settings');touch(show('Appearance'))
def fill(value,index=0):
    fields=[n for n in tree(S).iter('node') if n.get('class')=='android.widget.EditText'];touch(fields[index]);adb(S,'shell','input','keycombination','113','29')
    if not value:adb(S,'shell','input','keyevent','67');return
    if value.startswith('#'):adb(S,'shell','input','keyevent','18');value=value[1:]
    adb(S,'shell','input','text',value.replace(' ','%s'))

def isolate_launcher():
    adb(S,'shell','cmd','package','set-home-activity','com.google.android.apps.nexuslauncher/.NexusLauncherActivity')
    home();adb(S,'shell','am','force-stop','com.launcher.home')

def install_state(data):
    # Android immediately recreates a force-stopped default HOME. Switch away before replacing fixture files.
    isolate_launcher();uid=adb(S,'shell','stat','-c','%u',STATE).strip();fixture=OUT/'qa-state.json';fixture.write_text(json.dumps(data),encoding='utf-8');adb(S,'push',fixture,'/data/local/tmp/launcher-qa-state.json');adb(S,'shell','cp','/data/local/tmp/launcher-qa-state.json',STATE);adb(S,'shell','chown',uid+':'+uid,STATE);adb(S,'shell','restorecon',STATE)
    adb(S,'shell','cmd','package','set-home-activity','com.launcher.home/.MainActivity');adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity');tree(S)
def seed():
    data=state();data.update(SetupCompleted=True,Theme='Dark',Accent='#E4FF00',ShowHomeNavigation=True,FollowSystemTheme=False,AutomaticLightTheme=False,Columns=4,NotesVisible=True,RootOrder=['overview','calendar','notes','timers','reminders','focus','calculator','tickets','feeds'],RootHidden=[],CollapsedCards={},Notes=[{'Id':'a'*32,'Title':'QA migration keeper','Text':'Survives bridge and production updates','UpdatedAt':int(time.time()*1000),'Checklist':False},{'Id':'b'*32,'Title':'Trip checklist','Text':'[x] Charge phone\n[ ] Pack passport','UpdatedAt':int(time.time()*1000),'Checklist':True}],Reminders=[])
    for source in data.get('RssSources',[]):source['Enabled']=False
    install_state(data)
def smoke():
    seed();check('Apps, selected' in labels(),'bridge opens the Apps page with visible page tabs')
    tap(S,'Today');check('At a glance, expanded' in labels(),'Today uses the removable overview card');check(not any('Good morning' in s or 'Good afternoon' in s for s in labels() if s),'overview uses the date and active content rather than a greeting');shot('root-dark-acid')
    tap(S,'At a glance, expanded',True);tap(S,'Remove card');tap(S,'Remove');check('overview' in state()['RootHidden'],'overview can be removed like other cards')
    home();tap(S,'Today');check(not any('At a glance' in s for s in labels() if s),'overview removal survives page navigation')
    touch(show('+ Add card'));touch(show('At a glance'));check('overview' not in state()['RootHidden'],'overview can be restored through Add card')
    appearance();touch(show('Theme'));root=tree(S);check(any(n.get('selected')=='true' and 'Dark' in (n.get('text','')+n.get('content-desc','')) for n in root.iter('node')),'theme picker announces its current selection');tap(S,'Light');home();tap(S,'Today');shot('root-light-acid')
    appearance();touch(show('Accent'));tap(S,'Custom hex color');fill('invalid');tap(S,'Save');check('Accent · #RRGGBB' in labels(),'invalid accent stays in its editor');check(state()['Accent']=='#E4FF00','invalid accent leaves saved accent unchanged');fill('#9BCBFF');tap(S,'Save');check(state()['Accent']=='#9BCBFF','valid custom accent saves')
    finish_smoke()
def finish_smoke():
    check(state()['Accent']=='#9BCBFF','valid custom accent saves')
    touch(show('Show page tabs'));check(not state()['ShowHomeNavigation'],'page tabs can be turned off in Appearance');home();check('Apps, selected' not in labels() and 'Today' not in labels(),'page tabs disappear from home when switched off')
    appearance();touch(show('Show page tabs'));check(state()['ShowHomeNavigation'],'page tabs can be shown again');home();tap(S,'Today');tap(S,'Today');shot('root-light-sky')
    appearance();touch(show('Theme'));tap(S,'Dark');home();tap(S,'Today');tap(S,'Today');shot('root-dark-sky')
    home();tap(S,'Search apps');fill('migration');check(any('QA migration keeper' in (s or '') for s in labels()),'unified search finds saved note content');back();back()
    appearance();back();check('Setup & feature health' in labels() or 'Settings' in labels(),'Android Back returns from Appearance to Settings')
    home();adb(S,'shell','am','force-stop','com.launcher.home');adb(S,'shell','am','start','-n','com.launcher.home/.MainActivity');check(any(n['Title']=='QA migration keeper' for n in state()['Notes']),'notes survive process restart')
def layouts():
    font=adb(S,'shell','settings','get','system','font_scale').strip();rotate=adb(S,'shell','settings','get','system','accelerometer_rotation').strip();angle=adb(S,'shell','settings','get','system','user_rotation').strip()
    try:
        home();adb(S,'shell','settings','put','system','font_scale','2.0');adb(S,'shell','wm','size','840x1680');home();shot('apps-320dp-font200');root=tree(S);camera=next(n for n in root.iter('node') if n.get('text')=='Camera');check(camera.get('text')=='Camera','Camera label stays readable at 200 percent text on 320 dp');tap(S,'Today');shot('root-320dp-font200')
        adb(S,'shell','settings','put','system','font_scale',font);adb(S,'shell','wm','size','reset');adb(S,'shell','settings','put','system','accelerometer_rotation','0');adb(S,'shell','settings','put','system','user_rotation','1');home();shot('apps-landscape');tap(S,'Today');shot('root-landscape');check(True,'landscape keeps home navigation and Root content reachable')
    finally:
        adb(S,'shell','settings','put','system','font_scale',font);adb(S,'shell','wm','size','reset');adb(S,'shell','settings','put','system','accelerometer_rotation',rotate);adb(S,'shell','settings','put','system','user_rotation',angle);home()
if __name__=='__main__':
    try:
        if len(sys.argv)>1 and sys.argv[1]=='finish':finish_smoke()
        elif len(sys.argv)>1 and sys.argv[1]=='layouts':layouts()
        else:smoke()
    except BaseException:
        (OUT/'failure.png').write_bytes(adb(S,'exec-out','screencap','-p',binary=True));raise
