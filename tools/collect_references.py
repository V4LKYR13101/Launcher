"""Collect Ratio reference imagery; originals remain research-only assets."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from urllib.parse import urljoin
from io import BytesIO
import json, re, hashlib
import requests
from bs4 import BeautifulSoup
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / 'featureImages'
SOURCES = [
 ('designer', 'https://singyulam.com/blloc-ratio'),
 ('blloc-six', 'https://medium.com/blloc/announcing-ratio-6-46b76617c764'),
 ('yanko', 'https://www.yankodesign.com/2020/05/02/spending-more-time-on-screen-this-premium-andriod-launcher-makes-your-smartphone-less-addictive/'),
 ('xataka-tour', 'https://www.xatakandroid.com/aplicaciones-android/ratio-minimalismo-al-extremo-launcher-super-personalizable'),
 ('xataka-oled', 'https://www.xatakandroid.com/aplicaciones-android/este-genial-launcher-tiene-diseno-minimalista-ahorra-bateria-pantallas-oled'),
 ('tips', 'https://mrnoob.net/ratio-launcher-tips-and-tricks/'),
 ('hands-on', 'https://9to5google.com/2020/05/29/blloc-ratio-hands-on/'),
 ('six-review', 'https://9to5google.com/2022/03/11/the-best-android-launchers-to-use-with-your-smartphone-in-2022-video/'),
 ('minimal-phone', 'https://medium.com/tech-right-now/what-my-minimalist-phone-looks-like-the-android-version-13cc0e4605ee'),
 ('overview-review', 'https://medium.com/tech-right-now/ratio-a-new-minimalist-launcher-c228b240bd65'),
 ('malavida', 'https://www.malavida.com/en/soft/ratio/android/'),
 ('fairphone', 'https://forum.fairphone.com/t/minimalistic-android-launcher/60302'),
 ('nothing', 'https://nothing.community/d/11687-has-anyone-tried-ratio-launcher-on-nothing-phone'),
]
EXTRA = [
 ('settings-six','https://shakeuptech.com/wp-content/uploads/2023/06/ratio-Android-Launcher-1-461x1024.webp'),
 ('nothing-home','https://s3-nothing-prod.s3.eu-central-1.amazonaws.com/2024-06-21/1718962713-333803-screenshot-20240621-150741.png'),
 ('sun-drawers','https://forum.fairphone.com/uploads/default/original/3X/b/2/b221616e00dc8336b6313a250b5f49bf5a03720f.png'),
 ('conversation-filters','https://miro.medium.com/v2/resize:fit:1200/1*h9bwa2yRk0kwKQbuqJNSHQ.gif'),
 ('blloc-wallpaper','https://www.blloc.com/uploads/ratio_rationis_e429d0498f.jpg'),
 ('conversation-faq','https://blloc.com/api/uploads/220120_OG_thumbnail_0f363249e4.png'),
 ('yanko-drawers','https://www.yankodesign.com/images/design_news/2020/05/spending-more-time-on-screen-this-android-launcher-makes-your-smartphone-less-addictive/Ratio_by_Blloc_is_a_minimal_launcher_to_refine_your_smartphone_experience_09.jpg'),
]
session = requests.Session()
session.headers['User-Agent'] = 'Mozilla/5.0'
def page_images(source):
 name, page = source
 try:
  res = session.get(page, timeout=35); res.raise_for_status()
  soup = BeautifulSoup(res.text, 'html.parser')
  found = []
  for img in soup.find_all('img'):
   src = img.get('data-src') or img.get('data-original') or img.get('src') or ''
   if src.startswith('data:'): continue
   src = urljoin(page, src)
   alt = img.get('alt','')
   if name in ['designer','blloc-six','minimal-phone','overview-review']:
    if not any(t in src for t in ['squarespace-cdn', 'miro.medium']): continue
    if any(t in src for t in ['resize:fill:','resize:fill%3A','/100/','/64/']): continue
   elif name == 'fairphone':
    if '/uploads/' not in src: continue
   elif not re.search('ratio|blloc',src+' '+alt,re.I): continue
   if src not in [i[2] for i in found]: found.append((name,page,src,alt))
  # Medium sometimes emits only srcsets for article images.
  return found
 except Exception as e:
  return [('ERROR', page, '', str(e))]

items = []
for results in ThreadPoolExecutor(max_workers=8).map(page_images, SOURCES): items.extend(results)
errors = [x for x in items if x[0]=='ERROR']
items = [x for x in items if x[0]!='ERROR']
items += [(n,u,u,'Direct reference from image search') for n,u in EXTRA]
for i in range(1,9):
 items.append(('malavida-gallery','https://www.malavida.com/en/soft/ratio/android/',f'https://imag.malavida.com/mvimgbig/download-fs/ratio-36661-{i}.jpg','Screenshot gallery'))
seen=set()
def save(item):
 name,page,url,alt=item
 try:
  data=session.get(url,timeout=30); data.raise_for_status()
  im=Image.open(BytesIO(data.content))
  if im.width<180 or im.height<180: return None
  digest=hashlib.sha256(im.convert('RGB').tobytes()).hexdigest()
  # Filename hashes keep independent runs reproducible.
  filename=f'{name}-{hashlib.sha256(url.encode()).hexdigest()[:10]}.{ "gif" if im.format=="GIF" else "jpg"}'
  if im.format=='GIF': (ROOT/filename).write_bytes(data.content)
  else: im.convert('RGB').save(ROOT/filename,quality=95)
  return dict(file=filename,page=page,url=url,description=alt,width=im.width,height=im.height,sha256=digest)
 except Exception: return None

results=[r for r in ThreadPoolExecutor(max_workers=10).map(save,items) if r]
unique=[]
for r in results:
 if r['sha256'] in seen:
  (ROOT/r['file']).unlink(missing_ok=True)
 else: seen.add(r['sha256']); unique.append(r)
(ROOT/'sources.json').write_text(json.dumps(dict(images=unique,unavailable_pages=errors),indent=2),encoding='utf-8')
# Paginated contact sheets are readable at full resolution.
for start in range(0,len(unique),16):
 sheet=Image.new('RGB',(1600,2000),'#e6e6e6'); d=ImageDraw.Draw(sheet)
 for j,r in enumerate(unique[start:start+16]):
  x,y=j%4*400,j//4*500
  d.text((x+8,y+8),r['file'][:48],fill='black')
  im=Image.open(ROOT/r['file']).convert('RGB'); im.thumbnail((380,460))
  sheet.paste(im,(x+(400-im.width)//2,y+30))
 sheet.save(ROOT/f'reference-board-{start//16+1:02}.jpg',quality=94)
print(json.dumps(dict(candidate_images=len(items),downloaded=len(results),unique_images=len(unique),boards=(len(unique)+15)//16,unavailable_pages=len(errors))))
