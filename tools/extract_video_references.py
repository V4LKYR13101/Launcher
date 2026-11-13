from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import subprocess,json
ROOT=Path(__file__).resolve().parents[1]
VIDEO=ROOT/'artifacts/research-video/VVnmrUmSvqo.mp4'
OUT=ROOT/'featureImages/video-VVnmrUmSvqo';OUT.mkdir(exist_ok=True)
if not (OUT/'frame-0341.jpg').exists():
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-i',str(VIDEO),'-vf','fps=1/2,scale=1280:-2','-q:v','2',str(OUT/'frame-%04d.jpg')],check=True)
frames=sorted(OUT.glob('frame-*.jpg'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
for start in range(0,len(frames),20):
    batch=frames[start:start+20];sheet=Image.new('RGB',(1920,1016),'#101010');draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(batch):
        x=(i%5)*384;y=(i//5)*254
        with Image.open(path) as im:im.thumbnail((384,216));sheet.paste(im,(x,y))
        second=(start+i)*2;draw.text((x+10,y+221),f'{second//60:02}:{second%60:02}  {path.name}',font=font,fill='white')
    sheet.save(OUT/f'board-{start//20+1:02}.jpg',quality=92)
details={'drawers':108,'usage-indicators':119,'app-usage-editor':136,'locked-highlighted-tiles':144,'app-icon-picker':153,
    'per-app-controls':159,'app-monochrome':169,'biometric-lock':209,'drawer-editing':225,'root-command-input':249,
    'root-weather':262,'root-player':275,'timer-command':286,'root-notes':291,'extra-cards':302,'root-pin':312,
    'focus-sun-menu':346,'desktop-files':435,'desktop-notes':443,'late-home':614}
for name,second in details.items():
    target=OUT/f'{second:03d}s-{name}.jpg'
    if not target.exists():
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-ss',str(second),'-i',str(VIDEO),'-frames:v','1','-q:v','2',str(target)],check=True)
metadata=dict(source='https://www.youtube.com/watch?v=VVnmrUmSvqo',creator='TechAltar',published='2020-04-04',
    title='Ratio hands-on: the ultimate Android launcher',durationSeconds=681,frameIntervalSeconds=2,
    notes='Contact-sheet times are nominal two-second samples. Named detail frames are extracted at the exact requested time. Research references only, not Launcher marketing assets.',
    frames=[dict(file=p.name,nominalSeconds=i*2) for i,p in enumerate(frames)],details=details)
(OUT/'sources.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print(f'Extracted {len(frames)} timeline frames, {len(details)} exact-time details and {(len(frames)+19)//20} contact sheets to {OUT}')
