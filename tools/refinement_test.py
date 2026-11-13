"""Focused Android UI regression checks; isolated LauncherQA emulator only."""
from pathlib import Path
from PIL import Image
import json,re,time,shlex
from device_qa import adb,tree,tap,find,OUT

SERIAL='emulator-5570'
checks=[]
def visible(label):return any(n.get('text')==label or n.get('content-desc')==label for n in tree(SERIAL).iter('node'))
def check(value,name):
 assert value,name
 checks.append(name)
 print('PASS',name,flush=True)
def home():
 adb(SERIAL,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME','--ez','qa_keep_awake','true')
def shot(name):(OUT/(name+'.png')).write_bytes(adb(SERIAL,'exec-out','screencap','-p',binary=True))
def bounds(node):return list(map(int,re.findall(r'\d+',node.get('bounds'))))

home();time.sleep(2)
for permission in ['android.permission.BLUETOOTH_CONNECT','android.permission.ACCESS_COARSE_LOCATION','android.permission.ACCESS_FINE_LOCATION']:
 adb(SERIAL,'shell','pm','grant','com.launcher.home',permission)
home()
for a,b in [(700,550),(1000,800),(2300,2100)]:
 tap(SERIAL,'Open quick panel')
 adb(SERIAL,'shell','input','swipe',540,a,540,b,180)
 check(visible('Tap to search'),f'Quick panel closes on short upward drag at y={a}')

tap(SERIAL,'Launcher settings')
node=find(SERIAL,'Drag down to close sheet');x1,y1,x2,y2=bounds(node)
height=int(adb(SERIAL,'shell','wm','size').strip().split()[-1].split('x')[1])
shot('qa-bottom-sheet-final')
check(y1>height*.15,'Sheet handle is below top status area')
adb(SERIAL,'shell','input','swipe',(x1+x2)//2,(y1+y2)//2,(x1+x2)//2,(y1+y2)//2+500,350)
check(visible('Tap to search'),'Dragging bottom-sheet handle dismisses sheet')

# Only synthetic shell notifications on the isolated emulator are dismissed.
adb(SERIAL,'shell','cmd','notification','allow_listener','com.launcher.home/crc64b7906a7f52eca6eb.ConversationService')
for name,direction in [('QA swipe left',-1),('QA swipe right',1)]:
 adb(SERIAL,'shell','cmd notification post -t '+shlex.quote(name)+' '+shlex.quote('launcher-test-'+str(direction))+' '+shlex.quote('Synthetic notification for gesture verification'))
 time.sleep(.5);tap(SERIAL,'Open quick panel')
 check(visible(name),'Quick panel receives '+name)
 node=find(SERIAL,name);x1,y1,x2,y2=bounds(node);y=(y1+y2)//2
 adb(SERIAL,'shell','input','swipe',540,y,540+direction*470,y,350)
 check(not visible(name),'Notification dismisses '+('left' if direction<0 else 'right'))
 adb(SERIAL,'shell','input','keyevent',4)

tap(SERIAL,'Launcher settings');tap(SERIAL,'Settings');tap(SERIAL,'Root');check(visible('Focus card'),'Focus card has a visibility toggle')
tap(SERIAL,'Focus card');tap(SERIAL,'Back');tap(SERIAL,'Back');tap(SERIAL,'Root')
check(not visible('Focus'),'Focus card can be hidden')
shot('qa-root-focus-hidden')
home();tap(SERIAL,'Open quick panel');shot('qa-accent-final')
check(visible('Wi-Fi'),'Quick panel still opens after card preference change')

target=Path(__file__).resolve().parents[1]/'artifacts'/'refinement-test.json'
target.write_text(json.dumps(dict(passed=len(checks),checks=checks),indent=2),encoding='utf-8')
print(f'PASS: {len(checks)} refinement checks. {target}')
