"""Native UI checks on the isolated emulator. Never targets a physical phone."""
from pathlib import Path
import json,time,sys,re
from device_qa import adb,tree,tap
S='emulator-5554';assert S.startswith('emulator-')
OUT=Path(__file__).resolve().parents[1]/'artifacts/ui-polish-1.3.11';OUT.mkdir(parents=True,exist_ok=True)
checks=[]
def labels():
 return [(n.get('text') or n.get('content-desc')) for n in tree(S).iter('node')]
def expect(label):
 assert label in labels(),label
 checks.append(label);print('Verified:',label,flush=True)
def shot(name):
 tree(S);(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True));print('Captured:',name,flush=True)
def home():
 adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME');expect('Search apps')
def menu(label):
 tap(S,'Launcher settings')
 for attempt in range(4):
  if label in labels():tap(S,label);return
  sizes=re.findall(r'(\d+)x(\d+)',adb(S,'shell','wm','size'));width,height=map(int,sizes[-1]);swipe(width//2,int(height*.8),width//2,int(height*.4))
 raise AssertionError('Menu item: '+label)
def swipe(x,y,endx,endy):adb(S,'shell','input','swipe',x,y,endx,endy,350)
def main():
 home();shot('apps-dark')
 tap(S,'Essentials, expanded');expect('Essentials, collapsed');tap(S,'Essentials, collapsed');expect('Camera, hold for app actions')
 swipe(900,365,130,365);expect('Conversations');shot('conversations-dark')
 home();swipe(160,365,920,365);expect('TODAY');shot('root-dark')
 swipe(920,1800,920,600);shot('root-tools-dark')
 menu('Settings');expect('HOME SCREEN');expect('CARDS AND CONVERSATIONS');shot('settings-dark')
 tap(S,'Appearance');tap(S,'Theme');tap(S,'Light');expect('Appearance');shot('appearance-light')
 home();shot('apps-light')
 tap(S,'Camera, hold for app actions',True);shot('app-actions-light');adb(S,'shell','input','keyevent',4)
 menu('Add drawer');expect('Create drawer');shot('drawer-form-keyboard-light')
 adb(S,'shell','input','keyevent',4);shot('drawer-form-light')
 tap(S,'Cancel');expect('Search apps')
 menu('Settings');tap(S,'Appearance');tap(S,'Theme');tap(S,'Dark');home()
 tap(S,'Open quick panel');expect('Notifications');shot('quick-panel-dark')
 swipe(550,400,550,160);expect('Search apps')
 adb(S,'shell','settings','put','system','font_scale','1.5');home();shot('apps-large-text')
 menu('Settings');shot('settings-large-text')
 adb(S,'shell','settings','put','system','font_scale','1.0');home()
 (OUT/'checks.json').write_text(json.dumps(checks,indent=2));print('Completed',len(checks),'native UI assertions.',flush=True)
if __name__=='__main__':
 try:main()
 except BaseException:
  shot('failure');raise
 finally:adb(S,'shell','settings','put','system','font_scale','1.0')
