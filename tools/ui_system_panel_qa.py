"""Integration checks on emulator-5554 only. The inspector preserves accessibility services."""
from pathlib import Path
import json,time
from overlay_qa import *
OUT=Path('artifacts/system-panel-1.4.0');OUT.mkdir(exist_ok=True)
checks=[]
def check(value,label):
 assert value,label
 checks.append(label);print('Verified:',label,flush=True)
def shot(name):
 time.sleep(.3);(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
def click_node(n):
 x,y=center(n);adb(S,'shell','input','tap',x,y)
def card(label):
 root=tree();parents={c:n for n in root.iter() for c in n};n=next(n for n in root.iter('node') if n.get('text')==label);return parents[parents[n]]
def card_action(title,action):
 show(title);click_node(next(n for n in card(title).iter('node') if n.get('text')==action))
def main():
 if exists('Launcher system panel'):tap('Close')
 adb(S,'logcat','-c');adb(S,'shell','am','force-stop','com.launcher.uifixture');adb(S,'shell','am','start','-W','-n','com.google.android.deskclock/com.android.deskclock.DeskClock');adb(S,'shell','am','start','-W','-n','com.launcher.uifixture/.MainActivity','--ez','sample','true');time.sleep(.8);handle(True)
 check(exists('Launcher system panel'),'edge swipe opens over another app')
 if exists('Pause playback'):tap('Pause playback')
 check(exists('Resume playback'),'pause updates real media session')
 tap('Resume playback');check(exists('Pause playback'),'play updates real media session');tap('Pause playback');tap('Next track');time.sleep(1.1);check('0:00 / 3:22' in labels(),'next track resets playback position')
 shot('panel-dark');show('Sam');shot('notifications-dark')
 card_action('Alex','Reply');check(exists('Reply to Alex'),'inline reply editor opens')
 adb(S,'shell','input','text','Test%sreply');shot('reply-keyboard');tap('Send');show('Reply received');check(exists('Test reply'),'reply delivered through original app action')
 show('Sam');n=find('Sam');x,y=center(n);adb(S,'shell','input','swipe',150,y+50,960,y+50,350);time.sleep(.3);check('Sam' not in labels(),'horizontal swipe dismisses notification')
 show('Sample background task');n=find('Sample background task');x,y=center(n);adb(S,'shell','input','swipe',150,y+45,960,y+45,350);check('Sample background task' in labels(),'ongoing notification remains after swipe')
 show('Alex');tap('Alex');check(not exists('Launcher system panel'),'notification body closes panel');check(exists('START SAMPLE PLAYBACK AND MESSAGES'),'notification body opens source app')
 handle();tap('Evening light');check(not exists('Launcher system panel') and exists('START SAMPLE PLAYBACK AND MESSAGES'),'media body opens player')
 handle();n=find('Swipe up to close system panel');x,y=center(n);adb(S,'shell','input','swipe',x,y,x,y-180,250);check(not exists('Launcher system panel'),'upward handle dismisses panel')
 handle();adb(S,'shell','input','keyevent','4');check(not exists('Launcher system panel'),'Android Back dismisses panel')
 handle();tap('Android');check(not exists('Launcher system panel'),'native Android shade remains accessible');shot('native-shade');adb(S,'shell','cmd','statusbar','collapse');adb(S,'shell','am','start','-W','-n','com.launcher.uifixture/.MainActivity');time.sleep(.8)
 handle();adb(S,'shell','input','keyevent','223');time.sleep(.3);check('Launcher system panel' not in adb(S,'shell','dumpsys','window','windows'),'screen off removes overlay windows');adb(S,'shell','input','keyevent','224');adb(S,'shell','input','keyevent','82');adb(S,'shell','am','start','-W','-n','com.launcher.uifixture/.MainActivity');time.sleep(.8)
 handle();tap('Disable');time.sleep(.3);check('Launcher system panel' not in adb(S,'shell','dumpsys','window','windows'),'Disable removes every overlay window');shot('disabled-over-app')
 adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','--ez','open_system_panel_settings','true');check(find('Enable system panel').get('checked')=='false','master switch stays off on returning to Launcher');shot('settings-off')
 check('com.launcher.home' not in adb(S,'logcat','-d','-b','crash'),'no Launcher crash during native integration checks')
 (OUT/'interaction-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
if __name__=='__main__':
 try:main()
 except BaseException:
  shot('failure');(OUT/'failure.log').write_text(adb(S,'logcat','-d','-t','300'),encoding='utf-8');raise
 finally:(OUT/'checks-partial.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
