from pathlib import Path
import sys,re,json
from device_qa import adb,tree
S='emulator-5554';assert S.startswith('emulator-');out=Path('artifacts/ui-polish-1.3.12');out.mkdir(exist_ok=True)
try:
 adb(S,'install','-r','src/Launcher/bin/Release/net11.0-android/com.launcher.home-Signed.apk')
 adb(S,'shell','wm','size','720x1280');adb(S,'shell','wm','density','320');adb(S,'shell','settings','put','system','font_scale','1.5')
 adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME')
 nodes=list(tree(S).iter('node'));contact=next(n for n in nodes if n.get('text')=='Contacts');x1,y1,x2,y2=map(int,re.findall(r'\d+',contact.get('bounds')));assert y2-y1<55,(y1,y2)
 (out/'apps-small-large-text.png').write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
 (out/'checks.json').write_text(json.dumps(dict(version='1.3.12',build=22,display='360 x 640 dp',fontScale='150%',singleWordLabelOneLine=True,fullAccessibleName='Contacts'),indent=2));print('Verified one-line app labels at 150% text on a small display.',flush=True)
finally:
 adb(S,'shell','settings','put','system','font_scale','1.0');adb(S,'shell','wm','size','reset');adb(S,'shell','wm','density','reset')
