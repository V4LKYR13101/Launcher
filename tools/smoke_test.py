"""End-to-end smoke test on a dedicated emulator, never a personal phone."""
from pathlib import Path
import json, sys, re, time
from device_qa import adb, tree, tap, OUT

serial=sys.argv[1]
if serial!='emulator-5570':raise SystemExit('This test only runs against the dedicated LauncherQA emulator on port 5570.')
results=[]
def expect(label):
 nodes=list(tree(serial).iter('node'))
 assert any(n.get('text')==label or n.get('content-desc')==label for n in nodes),label
 results.append(label)
def back():adb(serial,'shell','input','keyevent',4)
def screenshot(name):
 (OUT/(name+'.png')).write_bytes(adb(serial,'exec-out','screencap','-p',binary=True))
def click(label):tap(serial,label)
def note_input():
 return next(n for n in tree(serial).iter('node') if n.get('class')=='android.widget.EditText' and n.get('text')!='')

adb(serial,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity','--ez','qa_keep_awake','true')
time.sleep(3)
expect('Essentials');screenshot('qa-classic')
click('Essentials, expanded');expect('Essentials, collapsed')
click('Essentials, collapsed');expect('Essentials, expanded')
click('Tap to search');expect('All apps')
adb(serial,'shell','input','text','Clock');expect('Results');expect('Clock');screenshot('qa-search')
back() # dismiss keyboard first
click('Close search');expect('Essentials')
click('Root');expect('Today');expect('Focus');expect('Notes');screenshot('qa-root')
click('25 min');expect('End focus')
click('Tiles');expect('Ⅱ  Focus is on · essentials & pinned apps')
click('Ⅱ  Focus is on · essentials & pinned apps');expect('Essentials')
click('Open quick panel');expect('Quick panel');expect('Wi-Fi');expect('Notifications');screenshot('qa-quick-panel')
adb(serial,'shell','input','swipe',530,2000,530,1500,350);expect('Essentials')
click('Launcher settings');expect('Launcher');
adb(serial,'shell','input','swipe',540,700,540,1800,450);expect('Essentials')
click('Launcher settings');click('Settings');expect('Appearance')
click('Appearance');click('Studio');expect('Columns');screenshot('qa-appearance')
click('Back');click('Back');expect('Essentials')
click('Launcher settings');click('Settings');click('Drawers');click('+  New drawer')
adb(serial,'shell','input','text','TestDrawer');screenshot('qa-new-drawer');click('Create drawer');expect('TestDrawer');click('Cancel')
expect('TestDrawer');back();expect('Essentials')
click('Conversations');expect('Conversations');expect('Set up conversations');screenshot('qa-conversations-empty')
back();expect('Essentials')
# Process restart verifies persistence without touching private app storage.
adb(serial,'shell','am','force-stop','com.launcher.home')
adb(serial,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity')
expect('Essentials')
click('Launcher settings');click('Settings');click('Appearance');expect('5')
back();back();expect('Essentials')
report=Path(__file__).resolve().parents[1]/'artifacts'/'smoke-test.json'
report.write_text(json.dumps({'device':serial,'passed':len(results),'checks':results},indent=2),encoding='utf-8')
print(f'PASS: {len(results)} device UI checks. {report}')
