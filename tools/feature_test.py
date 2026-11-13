"""Live provider/media/filter/battery checks on the dedicated emulator."""
import json,re,time,shlex
from pathlib import Path
from device_qa import adb,tree,tap,find,OUT

s='emulator-5570';checks=[]
def nodes():return list(tree(s).iter('node'))
def has(label):return any((n.get('text') or '').startswith(label) or n.get('content-desc')==label for n in nodes())
def check(condition,label):
 assert condition,label
 checks.append(label);print('PASS',label,flush=True)
def await_label(label,timeout=25):
 deadline=time.monotonic()+timeout
 while time.monotonic()<deadline:
  if has(label):return True
  time.sleep(1)
 return False
def click(label):tap(s,label)
def home():adb(s,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME','--ez','qa_keep_awake','true')
def shot(name):(OUT/(name+'.png')).write_bytes(adb(s,'exec-out','screencap','-p',binary=True))
def box(node):return list(map(int,re.findall(r'\d+',node.get('bounds'))))
def post(tag,title):adb(s,'shell','cmd notification post -t '+shlex.quote(title)+' '+shlex.quote(tag)+' '+shlex.quote('Synthetic scroll and swipe verification'))

home()
# Pause the synthetic fixture before hierarchy inspection: a continuously updating
# progress widget can prevent Android's UIAutomator idle barrier from completing.
adb(s,'shell','input','tap',540,600)
time.sleep(.5)
check(await_label('QA sample track') or await_label('QA second track'),'Active Android media session appears below weather')
check(await_label('Resume playback'),'Pause command reaches media session')
click('Resume playback');time.sleep(.3)
check('state=PLAYING(3)' in adb(s,'shell','dumpsys','media_session'),'Resume command reaches media session')
adb(s,'shell','input','tap',540,600);time.sleep(.3)
click('Next track');check(await_label('QA second track'),'Next command updates actual session metadata')
click('Previous track');check(await_label('QA sample track'),'Previous command updates metadata')
shot('qa-live-media-card')

click('Search apps');adb(s,'shell','input','text','Launcher%sQA')
check(await_label('3 unread notifications'),'Real fixture notification produces a three-count app badge')
click('Close search');click('Launcher settings');click('Notifications');check(await_label('QA unread notification'),'Conversations includes non-message notifications')
click('Filter notification apps');click('Filter apps');adb(s,'shell','input','text','Launcher%sQA');click('Launcher QA');click('Apply filters')
check(not has('QA unread notification'),'Conversation source exclusion filters that app')
home()

# Give the feed enough items to require scrolling, then verify vertical and horizontal gestures separately.
for i in range(1,7):post('launcher-qa-scroll-'+str(i),'QA item '+str(i))
click('Open quick panel');check(await_label('QA item 6'),'Quick panel receives synthetic notifications')
feeds=[n for n in nodes() if n.get('class')=='android.widget.ScrollView']
x1,y1,x2,y2=box(feeds[0])
for _ in range(2):adb(s,'shell','input','swipe',(x1+x2)//2,y2-35,(x1+x2)//2,y1+35,400)
check(has('Quick panel'),'Vertical notification scrolling does not close quick panel')
check(has('QA item 1') or has('QA item 2'),'Older notifications become reachable by scrolling')
for tag,label,direction in [('launcher-qa-scroll-1','QA item 1',-1),('launcher-qa-scroll-2','QA item 2',1)]:
 if not has(label):
  adb(s,'shell','input','swipe',540,y1+50,540,y2-50,350)
 node=find(s,label);bx1,by1,bx2,by2=box(node);cy=(by1+by2)//2
 adb(s,'shell','input','swipe',540,cy,540+direction*470,cy,180)
 time.sleep(.4)
 check(tag not in adb(s,'shell','cmd','notification','list'),'Notification dismisses '+('left' if direction<0 else 'right'))
shot('qa-notification-feed-final');click('Close quick panel');check(has('Search apps'),'Bottom handle closes panel after feed scroll')

click('Launcher settings');click('Settings');click('Weather');click('Location');adb(s,'shell','input','text','Cape%sTown');click('Search')
check(await_label('Cape Town, Western Cape, South Africa'),'Weather geocoding returns real city matches')
click('Cape Town, Western Cape, South Africa')
check(await_label('Updated ',timeout=25),'Weather fetch returns live provider data')
home();shot('qa-weather-media-final')

adb(s,'shell','cmd','battery','set','level','45');adb(s,'shell','cmd','battery','set','status','2')
check(await_label('45%'),'Status row reflects live battery level changes')
shot('qa-battery-45-charging');adb(s,'shell','cmd','battery','reset')
target=Path(__file__).resolve().parents[1]/'artifacts'/'feature-test.json'
target.write_text(json.dumps(dict(passed=len(checks),checks=checks),indent=2),encoding='utf-8')
print(f'PASS: {len(checks)} feature checks. {target}')
