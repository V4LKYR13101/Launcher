"""Read and operate UI without suspending the accessibility overlay under test."""
import xml.etree.ElementTree as ET,re,time
from device_qa import adb
S='emulator-5554'
assert S.startswith('emulator-')
def tree():
 result=adb(S,'shell','am','instrument','-w','com.launcher.overlayinspector/.Inspector')
 assert 'INSTRUMENTATION_RESULT: xml=' in result,result
 return ET.fromstring(result.split('INSTRUMENTATION_RESULT: xml=',1)[1].splitlines()[0].strip())
def nodes():return list(tree().iter('node'))
def find(label):
 matches=[n for n in nodes() if (n.get('text')==label or n.get('content-desc')==label) and n.get('visible')=='true']
 assert matches,'Missing: '+label
 return next((n for n in matches if n.get('clickable')=='true'),matches[0])
def center(n):
 x1,y1,x2,y2=map(int,re.findall(r'-?\d+',n.get('bounds')));return (x1+x2)//2,(y1+y2)//2
def tap(label):
 x,y=center(find(label));adb(S,'shell','input','tap',x,y)
def exists(label):return any((n.get('text')==label or n.get('content-desc')==label) and n.get('visible')=='true' for n in nodes())
def show(label):
 for i in range(8):
  allnodes=nodes();matches=[n for n in allnodes if n.get('text')==label or n.get('content-desc')==label]
  if any(n.get('visible')=='true' and 440<center(n)[1]<1750 for n in matches):return
  above=bool(matches and center(matches[0])[1]<440)
  adb(S,'shell','input','swipe',850,800 if above else 1980,850,1980 if above else 800,350)
 raise AssertionError('Not found after scrolling: '+label)
def labels():return [n.get('text') or n.get('content-desc') for n in nodes() if n.get('text') or n.get('content-desc')]

def handle(swipe=False):
 chunks=adb(S,'shell','dumpsys','window','windows').split('Window #')
 target=next(c for c in chunks if 'Launcher system panel' in c and 'NOT_FOCUSABLE' in c and 'NOT_TOUCHABLE' not in c)
 coords=re.search(r'frame=\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]',target)
 x1,y1,x2,y2=map(int,coords.groups());x,y=(x1+x2)//2,(y1+y2)//2
 if swipe:adb(S,'shell','input','swipe',x,y,x,y+200,300)
 else:adb(S,'shell','input','tap',x,y)
 time.sleep(.6)
