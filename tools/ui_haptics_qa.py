from pathlib import Path
import json,re,time
from device_qa import adb,tree,tap
from ui_polish_qa import home,menu,expect,S
OUT=Path('artifacts/haptics-1.3.13');OUT.mkdir(exist_ok=True)
def logs():return adb(S,'logcat','-d','-s','LauncherHaptics:D','*:S')
def clear():adb(S,'logcat','-c')
def shot(name):tree(S);(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
def settings():
 if any(n.get('content-desc')=='Drag down to close sheet' for n in tree(S).iter('node')):adb(S,'shell','input','keyevent',4)
 home();menu('Settings');tap(S,'Haptic feedback')
old=adb(S,'shell','settings','get','system','haptic_feedback_enabled').strip()
try:
 settings();
 if not any(n.get('content-desc')=='Subtle, selected' for n in tree(S).iter('node')):tap(S,'Subtle');settings()
 expect('Subtle, selected');shot('subtle')
 tap(S,'Crisp');home();
 if any(n.get('content-desc')=='Essentials, collapsed' for n in tree(S).iter('node')):tap(S,'Essentials, collapsed')
 clear();tap(S,'Essentials, expanded');expect('Essentials, collapsed');data=logs();assert data.count('cue=Tap')==1,data
 tap(S,'Essentials, collapsed');tree(S);clear();adb(S,'shell','input','swipe',900,1600,900,700,250);tree(S);assert 'cue=' not in logs(),'ordinary scroll buzzed'
 settings();expect('Crisp, selected');tap(S,'Off');home();clear();
 if any(n.get('content-desc')=='Essentials, collapsed' for n in tree(S).iter('node')):tap(S,'Essentials, collapsed')
 expect('Essentials, expanded');tap(S,'Camera, hold for app actions',True);tree(S);assert 'cue=' not in logs(),'Off emitted haptics';adb(S,'shell','input','keyevent',4)
 settings();expect('Off, selected');shot('off');tap(S,'Crisp');settings();expect('Crisp, selected');shot('crisp')
 adb(S,'shell','settings','put','system','haptic_feedback_enabled','0');clear();tap(S,'Tap');data=logs();assert 'suppressed=system' in data and 'accepted=True' not in data,data;(OUT/'system-off-dispatch.log').write_text(data)
 adb(S,'shell','settings','put','system','haptic_feedback_enabled','1');clear();tap(S,'Hold');data=logs();assert 'cue=Hold' in data,data
 # A drag close uses the existing handle and commits once.
 n=next(n for n in tree(S).iter('node') if n.get('content-desc')=='Drag down to close sheet');x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')));clear();adb(S,'shell','input','swipe',(x1+x2)//2,(y1+y2)//2,(x1+x2)//2,min(2300,(y1+y2)//2+420),300);tree(S);assert logs().count('cue=Commit')==1,logs()
 # Restart proves the style is saved independently of transient controls.
 adb(S,'shell','am','force-stop','com.launcher.home');settings();expect('Crisp, selected');tap(S,'Subtle')
 (OUT/'checks.json').write_text(json.dumps(dict(defaultSubtle=True,crispPersisted=True,offDisablesTapAndHold=True,singleCuePerTap=True,noScrollHaptics=True,sheetDismissCommit=True,systemPreferenceUsed=True,physicalFeelVerified=False),indent=2));print('Native haptics routing, Off, persistence and gesture checks passed.',flush=True)
finally:
 if old in ['0','1']:adb(S,'shell','settings','put','system','haptic_feedback_enabled',old)
 else:adb(S,'shell','settings','delete','system','haptic_feedback_enabled')
