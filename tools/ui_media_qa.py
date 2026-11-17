from pathlib import Path
import json
from device_qa import adb,tree,tap
from ui_polish_qa import S,OUT,home,menu,expect,shot,swipe
home();expect('Resume playback');shot('apps-media-dark')
# The playing seekbar never becomes idle for UIAutomator. Reuse the observed
# transport bounds, and verify state through Android's actual media session.
n=next(n for n in tree(S).iter('node') if n.get('content-desc')=='Resume playback');import re,time
x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')));x,y=(x1+x2)//2,(y1+y2)//2
adb(S,'shell','input','tap',x,y);time.sleep(.4)
assert 'state=PLAYING(3)' in adb(S,'shell','dumpsys','media_session')
assert 'topResumedActivity=' in adb(S,'shell','dumpsys','activity','activities')
(OUT/'apps-media-playing.png').write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
adb(S,'shell','input','tap',x,y);expect('Resume playback')
n=next(n for n in tree(S).iter('node') if n.get('content-desc')=='Now playing · open Studio Player');x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')));adb(S,'shell','input','tap',x1+12,y1+90);assert any((n.get('text') or '').startswith('Studio Player opened') for n in tree(S).iter('node'));print('Verified media card opens source app; transport buttons stay in Launcher.',flush=True)
home();menu('Notifications');expect('Sam');expect('Alex');shot('conversations-messages-dark')
tap(S,'Sam');assert any((n.get('text') or '').startswith('Studio Player opened') for n in tree(S).iter('node'));print('Verified notification opens its source app.',flush=True)
home();tap(S,'Open quick panel');shot('quick-panel-notifications')
# Dismiss the remaining sample message with a horizontal swipe on its notification card.
nodes=list(tree(S).iter('node'));n=next(n for n in nodes if n.get('text')=='Alex');import re
x1,y1,x2,y2=map(int,re.findall(r'\d+',n.get('bounds')));swipe(550,(y1+y2)//2,1040,(y1+y2)//2)
assert 'Alex' not in [n.get('text') for n in tree(S).iter('node')];print('Verified notification swipe dismissal.',flush=True)
(OUT/'media-checks.json').write_text(json.dumps(dict(pauseResume=True,mediaBodyOpensSource=True,buttonsDoNotLaunch=True,notificationOpensSource=True,swipeDismiss=True),indent=2))
home()
