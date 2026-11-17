"""Native icon sizing checks, explicitly restricted to the isolated emulator."""
from pathlib import Path
import json,re
from device_qa import adb,tree,tap,find
from ui_polish_qa import home,menu
S='emulator-5554';assert S.startswith('emulator-')
OUT=Path('artifacts/icon-size-1.4.1');OUT.mkdir(exist_ok=True)
checks=[]
def labels():return [n.get('text') or n.get('content-desc') for n in tree(S).iter('node')]
def check(condition,message):
 assert condition,message
 checks.append(message);print('Verified:',message,flush=True)
def shot(name):
 tree(S);(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
def show(label):
 for i in range(5):
  if label in labels():return
  adb(S,'shell','input','swipe',900,1900,900,800,300)
 raise AssertionError(label)
def appearance():
 home();menu('Settings');tap(S,'Appearance');show('Tile icon size')
def size_dialog():show('Tile icon size');tap(S,'Tile icon size')
def scale(high):
 n=next(n for n in tree(S).iter('node') if n.get('class')=='android.widget.SeekBar');x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')))
 adb(S,'shell','input','tap',x2-2 if high else x1+2,(y1+y2)//2)
 check(('150%' if high else '60%') in labels(),'preview shows '+('150%' if high else '60%'))
def camera():return next(n for n in tree(S).iter('node') if n.get('content-desc','').startswith('Camera,')).get('bounds')
def main():
 adb(S,'logcat','-c');appearance();size_dialog();tap(S,'Reset to default');check('100% · default' in labels(),'reset preview returns to default');tap(S,'Save');home();baseline=camera();shot('apps-100')
 appearance();size_dialog();scale(False);shot('size-60');tap(S,'Save');check('60%' in labels(),'small size saved in Appearance');home();check(camera()==baseline,'small icons keep tile bounds');shot('apps-60')
 appearance();size_dialog();scale(True);shot('size-150');tap(S,'Save');check('150%' in labels(),'large size saved in Appearance');home();check(camera()==baseline,'large icons keep tile bounds');shot('apps-150')
 appearance();size_dialog();scale(False);tap(S,'Cancel');check('150%' in labels(),'Cancel keeps saved size')
 adb(S,'shell','am','force-stop','com.launcher.home');appearance();check('150%' in labels(),'icon size survives app restart')
 tap(S,'Columns');tap(S,'6');home();shot('apps-six-columns');appearance();tap(S,'Columns');tap(S,'4');show('Tile height');tap(S,'Tile height');tap(S,'Compact · 88');home();shot('apps-compact')
 appearance();tap(S,'Tile height');tap(S,'Classic · 112');size_dialog();tap(S,'Reset to default');tap(S,'Save');check('Default · 100%' in labels(),'default reset saves correctly')
 check('com.launcher.home' not in adb(S,'logcat','-d','-b','crash'),'no Launcher crash during icon-size checks')
 (OUT/'checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
if __name__=='__main__':
 try:main()
 except BaseException:shot('failure');raise
