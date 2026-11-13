"""Render reproducible product boards around unmodified app captures."""
from pathlib import Path
import json,html
from PIL import Image
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'artifacts/showcase'
OUT=BASE/'mockups';OUT.mkdir(exist_ok=True)
MEDIA=BASE/'media';MEDIA.mkdir(exist_ok=True)
font=(ROOT/'src/Launcher/Assets/Outfit-Regular.ttf').as_uri()
bold=(ROOT/'src/Launcher/Assets/Outfit-Bold.ttf').as_uri()
boards=[
 ('cover','Launcher.','A home screen\nwith less noise.','Apps, weather and what is playing.\nJust where you need them.',[],True),
 ('home','Your everyday apps.','Grouped your way.','Named drawers. Real app icons.\nThe essentials within reach.',['home','drawers'],False),
 ('themes','Make it yours.','A little colour.\nA lot of choice.','Light and dark themes, custom accents\nand app icons that keep their identity.',['light','mint'],False),
 ('icons','A symbol for each space.','Small details.\nYour choices.','Choose a geometric icon for every drawer.\nRename it. Fill it. Put it where it belongs.',['drawer-icons'],False),
 ('root','A place to pause.','Notes. Focus.\nA clearer day.','Collapsible cards and Android widgets.\nKeep the pieces you actually use.',['root'],False),
 ('conversations','Choose what comes through.','Your notifications,\nin one place.','Small source icons and app filters\nmake a busy feed easier to read.',['conversations','notification-filters'],False),
 ('panel','The quick things.','A panel\nwithin reach.','Connections, brightness and notifications.\nPull down to open. Swipe up to close.',['quick-panel'],False),
 ('weather','Before you head out.','Your city.\nYour forecast.','Weather in your chosen accent.\nSet the city and refresh interval yourself.',['weather-settings','home'],False),
 ('appearance','A home that fits.','Set the pace.\nShape the layout.','Adjust columns, tiles, headings and icons.\nChoose the status row position too.',['appearance'],False),
 ('search','Find it. Open it.','Search at\nyour fingertips.','A lower search bar for installed apps.\nType a name and get on with your day.',['search'],False),
]
manifest=[]
css=f'''@font-face{{font-family:Outfit;src:url('{font}')}}@font-face{{font-family:Outfit;src:url('{bold}');font-weight:700}}
*{{box-sizing:border-box}}body{{margin:0;background:#11120f;color:#f4f4ee;font-family:Outfit}}main{{width:1920px;height:1080px;position:relative;overflow:hidden;background:radial-gradient(ellipse at 88% 30%,#30341c 0,transparent 58%),linear-gradient(135deg,#10110f,#191c17)}}
.copy{{position:absolute;left:110px;top:170px;z-index:3;max-width:850px}}.eyebrow{{font-size:24px;color:#e4ff00;letter-spacing:3px;margin:0 0 45px}}h1{{font-size:100px;line-height:1.03;letter-spacing:-4px;margin:0 0 40px;white-space:pre-line}}p{{font-size:29px;line-height:1.5;color:#afb2a6;white-space:pre-line}}.foot{{position:absolute;left:110px;bottom:55px;font-size:19px;color:#93978b;letter-spacing:.5px;z-index:4}}.brand{{position:absolute;right:95px;bottom:55px;color:#e4ff00;font-size:23px;z-index:4}}
.phone{{position:absolute;top:85px;right:185px;width:390px;padding:11px;background:linear-gradient(120deg,#707469,#20231f 9%,#080908 55%,#4d5147);border:1px solid #777c6b;border-radius:51px;box-shadow:0 45px 65px #0009,0 0 0 2px #080a08;transform:rotate(3deg)}}.phone:before{{content:'';position:absolute;right:-5px;top:160px;width:4px;height:65px;border-radius:5px;background:#777c70}}.phone img{{display:block;width:100%;height:auto;border-radius:40px}}.duo .phone{{width:328px;top:150px;right:92px;transform:rotate(5deg)}}.duo .phone:nth-of-type(2){{right:470px;top:100px;transform:rotate(-5deg)}}.hero{{background-position:right center;background-size:auto 100%;background-repeat:no-repeat}}.hero .copy{{top:270px}}.hero h1{{font-size:115px}}'''
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,channel='chrome',args=['--allow-file-access-from-files'])
    page=browser.new_page(viewport={'width':1920,'height':1080},device_scale_factor=1)
    for index,(name,eyebrow,title,description,screens,hero) in enumerate(boards,1):
        phones=''.join(f'<div class="phone"><img src="{(BASE/"screenshots"/(screen+".png")).as_uri()}"></div>' for screen in screens)
        style=f' style="background-image:url({(OUT/"studio-hero.png").as_uri()})"' if hero else ''
        classes=('hero' if hero else '')+(' duo' if len(screens)==2 else '')
        markup=f'<html><head><meta charset="utf-8"><style>{css}</style></head><body><main class="{classes}"{style}><div class="copy"><div class="eyebrow">{html.escape(eyebrow)}</div><h1>{html.escape(title)}</h1><p>{html.escape(description)}</p></div>{phones}<div class="foot">ANDROID · C# / .NET &nbsp;&nbsp; | &nbsp;&nbsp; Sample content</div><div class="brand">Launcher &nbsp; {index:02}</div></main></body></html>'
        source=OUT/(name+'.html');source.write_text(markup,encoding='utf-8');page.goto(source.as_uri());page.evaluate('document.fonts.ready');page.wait_for_timeout(250)
        target=OUT/(name+'.png');page.screenshot(path=str(target));Image.open(target).convert('RGB').save(MEDIA/('launcher-'+name+'-v1.webp'),'WEBP',quality=92,method=6)
        manifest.append(dict(name=name,caption=eyebrow,title=title.replace('\n',' '),file='launcher-'+name+'-v1.webp',kind='Generated product scene using an app capture.' if hero else 'Device mockup using actual app captures.'))
    browser.close()
for source in (BASE/'screenshots').glob('*.png'):
    if source.stem=='updates':continue # This capture is the deliberately older update-test installation.
    Image.open(source).convert('RGB').save(MEDIA/('launcher-screen-'+source.stem+'-v1.webp'),'WEBP',quality=91,method=6)
(BASE/'artwork.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(f'Rendered {len(boards)} product boards and copied actual screenshots to {MEDIA}')

