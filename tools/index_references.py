from pathlib import Path
import ast, json
from PIL import Image

root=Path(__file__).resolve().parents[1]
folder=root/'featureImages'
manifest=json.loads((folder/'sources.json').read_text(encoding='utf-8'))
known={r['file']:r for r in manifest['images']}
script=ast.parse((root/'tools/research_images.py').read_text(encoding='utf-8'))
initial={}
for node in script.body:
 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='urls' for t in node.targets):
  initial={name+'.jpg':url for name,url in ast.literal_eval(node.value)}
images=[]
for file in sorted(folder.iterdir()):
 if file.suffix.lower() not in ['.jpg','.jpeg','.png','.webp','.gif']:continue
 if file.name.startswith('reference-board-') or file.name=='contact-sheet.jpg':continue
 image=Image.open(file)
 record=known.get(file.name,dict(file=file.name,url=initial.get(file.name,''),page='',description='Initial online reference' if file.name in initial else 'User-provided reference'))
 record.update(width=image.width,height=image.height)
 images.append(record)
manifest['images']=images
(folder/'sources.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
lines=['# Visual reference index','',f'{len(images)} reference image files, plus contact sheets. Originals are retained at their downloaded resolution. These files are research material and are not bundled in the Android app.','', '| File | Dimensions | Source |','| --- | --- | --- |']
for r in images:
 source=f'[source]({r.get("page") or r.get("url")})' if r.get('page') or r.get('url') else 'User-provided'
 lines.append(f'| [{r["file"]}]({r["file"]}) | {r["width"]} × {r["height"]} | {source} |')
(folder/'REFERENCE-INDEX.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'Indexed {len(images)} reference image files.')
