from pathlib import Path
import json,time,xml.etree.ElementTree as ET,hashlib,io
from PIL import Image,ImageDraw
from device_qa import adb
S='emulator-5554';assert S.startswith('emulator-')
out=Path('artifacts/ui-polish-1.3.11');out.mkdir(exist_ok=True)
raw=adb(S,'shell','cat','/data/user/0/com.launcher.home/shared_prefs/launcher-state.xml');(out/'before-feed-state.xml').write_text(raw,encoding='utf-8')
root=ET.fromstring(raw);node=next(n for n in root if n.get('name')=='state');state=json.loads(node.text)
state['RssSources']=[dict(Id='qa',Name='Sample news',Url='https://launcher-ui-check.invalid/feed',Enabled=True)]
state['RootOrder']=['feeds','resources','calendar','focus','timers','tickets'];state['RootHidden']=['weather','media','notes','calculator']
state['ResourcesVisible']=True;state['FeedArticleCount']=4;state['FeedImages']=True;state['Theme']='Dark';state['CollapsedCards']={}
node.text=json.dumps(state,ensure_ascii=False);file=out/'feed-state.xml';ET.ElementTree(root).write(file,encoding='utf-8',xml_declaration=True)
adb(S,'shell','mkdir','-p','/data/user/0/com.launcher.home/cache/feed-images')
now=int(time.time()*1000);items=[]
for i,(title,detail) in enumerate([('A new riverside path opens this weekend','The route connects the park and the old railway bridge.'),('Local studio opens its doors','Meet the makers behind the latest exhibition.'),('The weekend reading list','Books, long reads and a few things worth saving.'),('A closer look at the night sky','The best places to watch the meteor shower.'),('New trains on the coastal route','Extra departures start on Monday.'),('Community garden takes root','Neighbours share their first harvest.')]):
 image='https://launcher-ui-check.invalid/image'+str(i)+'.png' if i%2==0 else ''
 items.append(dict(Title=title,Detail=detail,Url='https://example.com/sample-'+str(i),PublishedAt=now-i*3600000,Source='Sample news',ImageUrl=image))
 if image:
  im=Image.new('RGB',(320,320),(36,57,56));draw=ImageDraw.Draw(im);draw.ellipse((80,-50,400,230),fill=(160,182,104));draw.polygon([(0,320),(180,100),(320,320)],fill=(93,132,120));buf=io.BytesIO();im.save(buf,format='PNG');temp=out/('feed-image-'+str(i)+'.png');temp.write_bytes(buf.getvalue());name=hashlib.sha256(image.encode()).hexdigest().upper();adb(S,'push',str(temp),'/data/user/0/com.launcher.home/cache/feed-images/'+name)
cache=out/'feeds.json';cache.write_text(json.dumps(dict(qa=dict(Url=state['RssSources'][0]['Url'],Links=items,UpdatedAt=now))),encoding='utf-8')
adb(S,'shell','am','force-stop','com.launcher.home');adb(S,'push',str(file),'/data/user/0/com.launcher.home/shared_prefs/launcher-state.xml');adb(S,'push',str(cache),'/data/user/0/com.launcher.home/cache/feeds.json');owner=adb(S,'shell','stat','-c','%u:%g','/data/user/0/com.launcher.home').strip();adb(S,'shell','chown',owner,'/data/user/0/com.launcher.home/cache/feeds.json');adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','--ez','open_root','true');print('Loaded isolated sample feed fixtures.',flush=True)
