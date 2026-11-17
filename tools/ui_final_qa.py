from pathlib import Path
import json,re,time
from device_qa import adb,tree,tap
from ui_polish_qa import S,OUT,home,menu,expect,shot,swipe
assert S.startswith('emulator-')

def find_scroll(label):
 for attempt in range(4):
  if any(n.get('text')==label for n in tree(S).iter('node')):return
  swipe(950,1800,950,750)
 raise AssertionError(label)
try:
 adb(S,'shell','am','force-stop','com.launcher.uifixture');adb(S,'shell','am','start','-W','-n','com.launcher.uifixture/.MainActivity','--ez','sample','true')
 adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME')
 # Same native transport position inspected in the earlier paused hierarchy.
 time.sleep(.6);adb(S,'shell','input','tap',540,594);expect('Resume playback');shot('final-apps-dark')
 menu('Notifications');expect('Alex');shot('final-conversations-dark')
 tap(S,'Reply');expect('Send reply');shot('final-reply-form');tap(S,'Cancel')
 menu('Root');expect('PHONE RESOURCES');shot('final-root-dark')
 find_scroll('+ Add journey');tap(S,'+ Add journey');expect('Save journey');shot('final-journey-form')
 n=next(n for n in tree(S).iter('node') if n.get('class')=='android.widget.EditText');x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')));adb(S,'shell','input','tap',(x1+x2)//2,(y1+y2)//2);adb(S,'shell','input','text','UI%stest');expect('Save journey');shot('final-journey-keyboard');tap(S,'Save journey');expect('Type a command')
 menu('Settings');shot('final-settings-dark');tap(S,'Appearance');assert sum(n.get('text')=='Appearance' for n in tree(S).iter('node'))==1;shot('final-appearance-dark')
 tap(S,'Theme');tap(S,'Light');home();shot('final-apps-light')
 menu('Settings');adb(S,'shell','wm','size','720x1280');adb(S,'shell','wm','density','320');tree(S);shot('final-settings-small')
 tap(S,'Appearance');tap(S,'Theme');shot('final-theme-sheet-small');tap(S,'Dark');home();shot('final-apps-small')
 adb(S,'shell','settings','put','system','font_scale','1.5');home();shot('final-apps-small-large-text')
 adb(S,'shell','settings','put','system','font_scale','1.0');home();menu('Settings');tap(S,'Appearance')
 adb(S,'shell','wm','size','1600x2560');adb(S,'shell','wm','density','240');tree(S);tap(S,'Theme');nodes=list(tree(S).iter('node'));heading=next(n for n in nodes if n.get('text')=='Theme');x1,y1,x2,y2=map(int,re.findall(r'\d+',heading.get('bounds')));assert x1>100 and x2-x1<=960;shot('final-theme-sheet-wide');adb(S,'shell','input','keyevent',4)
 (OUT/'final-checks.json').write_text(json.dumps(dict(finalPackageVersion='1.3.11',replyFooter=True,journeySave=True,singleSettingsTitle=True,smallDisplay='360 x 640 dp',largeText='150%',wideSheetMaxWidth='640 dp'),indent=2));print('Final package checks completed.',flush=True)
finally:
 adb(S,'shell','settings','put','system','font_scale','1.0');adb(S,'shell','wm','size','reset');adb(S,'shell','wm','density','reset')
