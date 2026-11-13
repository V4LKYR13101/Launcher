"""Small adb UI helper. Every operation explicitly targets one device serial."""
import argparse, subprocess, xml.etree.ElementTree as ET, re, json, sys
from pathlib import Path

ADB = Path('C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe')
sys.stdout.reconfigure(encoding='utf-8')
OUT = Path(__file__).resolve().parents[1] / 'artifacts' / 'screenshots'
OUT.mkdir(parents=True, exist_ok=True)

def adb(serial, *args, binary=False,include_stderr=False):
    result = subprocess.run([str(ADB), '-s', serial, *map(str,args)], capture_output=True, check=True,timeout=45)
    data=result.stdout+(result.stderr if include_stderr else b'')
    return data if binary else data.decode('utf-8', errors='replace')

def tree(serial):
    adb(serial,'shell','rm','-f','/sdcard/launcher-ui.xml')
    result=adb(serial, 'shell', 'uiautomator', 'dump', '/sdcard/launcher-ui.xml',include_stderr=True)
    if 'dumped' not in result:
        raise RuntimeError('Android could not obtain an idle hierarchy; refusing to read a stale snapshot: '+result.strip())
    return ET.fromstring(adb(serial, 'shell', 'cat', '/sdcard/launcher-ui.xml'))

def find(serial, label):
    nodes = list(tree(serial).iter('node'))
    matches = [n for n in nodes if n.get('text')==label or n.get('content-desc')==label]
    if not matches:
        raise ValueError(f'No visible UI element: {label}')
    return matches[0]

def tap(serial, label, hold=False):
    node=find(serial,label)
    x1,y1,x2,y2=map(int,re.findall(r'\d+',node.get('bounds')))
    x,y=(x1+x2)//2,(y1+y2)//2
    if hold: adb(serial,'shell','input','swipe',x,y,x,y,700)
    else: adb(serial,'shell','input','tap',x,y)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('serial');p.add_argument('action',choices=['screen','tree','tap','hold','start','back']);p.add_argument('value',nargs='?',default='launcher')
    a=p.parse_args()
    if a.action=='screen':
        target=OUT/(a.value+'.png');target.write_bytes(adb(a.serial,'exec-out','screencap','-p',binary=True));print(target)
    elif a.action=='tree':
        for n in tree(a.serial).iter('node'):
            if n.get('text') or n.get('content-desc'):
                print(json.dumps({k:n.get(k) for k in ['text','content-desc','bounds','clickable']},ensure_ascii=False))
    elif a.action in ['tap','hold']: tap(a.serial,a.value,a.action=='hold')
    elif a.action=='start': print(adb(a.serial,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity'))
    elif a.action=='back': adb(a.serial,'shell','input','keyevent','4')
