from pathlib import Path
from PIL import Image
import json,hashlib
ROOT=Path(__file__).resolve().parents[1];HERE=ROOT/'artifacts/showcase'
artwork=json.loads((HERE/'artwork.json').read_text())
gallery=[]
for i,item in enumerate(artwork):
    gallery.append(dict(src='/media/'+item['file'],caption=item['caption'],alt=item['title'],
        note=item['kind']+' Sample weather, notes and notifications.',width=1920,height=1080,heroEnabled=i==0))
for path in (HERE/'media').glob('launcher-screen-*.webp'):
    with Image.open(path) as im:w,h=im.size
    label=path.stem.removeprefix('launcher-screen-').removesuffix('-v1').replace('-',' ').capitalize()
    gallery.append(dict(src='/media/'+path.name,caption=label,alt='Actual Launcher '+label.lower()+' screen',
        note='Actual Android app capture with sample content.',width=w,height=h,heroEnabled=False))
project=dict(slug='launcher',title='Launcher',category='A customisable Android home screen',dates='2026',
    organisation='Personal project',role='Design & development',collaborators='',
    description='Launcher puts apps into named drawers, with monochrome icons, a weather strip and a compact media player. A separate page holds notes, focus sessions and Android widgets. Choose the colours, layout and notification sources that work for you.',
    stack='C# · .NET 11 · Native Android views',url='/projects/launcher/#screenshots',linkLabel='Explore Launcher',
    cover='/media/launcher-cover-v1.webp',banner='/media/launcher-cover-v1.webp',socialImage='/media/launcher-cover-v1.webp',coverType='landscape',
    problem='An Android home screen should make everyday apps easy to find without filling the screen with competing colours and controls.',
    approach='Group apps into editable drawers, use their real icons in monochrome, and give weather, media, cards and notifications their own places. Test the gestures and native integrations on a Galaxy A26.',
    result='A working Android launcher with adjustable themes and layouts, native widget hosting, filtered notifications, playback controls and updates from Settings.',
    notes=[
        'Android 7 or later. Download the APK, approve installation and choose Launcher as the default Home app in Android settings.',
        'Custom drawer names and symbols, app assignment, pinned apps, hidden apps, tile sizing and three themes with custom accent colours.',
        'Weather uses Open-Meteo with an editable city and manual or scheduled foreground refresh. Root also supports existing Android widgets.',
        'The media card uses active Android media sessions. Controls depend on the playing app. Tap the card body to open that app.',
        'Notification access enables Conversations, notification count badges and media access. Notification sources are selectable. Usage access is optional for time totals.',
        'The quick panel is part of Launcher. Android controls the system status bar and notification shade across other apps.',
        'Settings, notes and layout remain on the device. JSON layout backup is available. Widget bindings remain local to the device.',
        'Settings includes App updates: check Kevlog, download a verified APK and open Android’s installer. Layout and settings are retained.',
        'Gallery screens use sample content. Device frames are mockups. The studio cover is a generated product scene based on an app capture.'
    ],buttons=[dict(filename='Launcher.apk',label='Download Android APK',icon='file_download',colour='#e4ff00',text_colour='#11120f',note='v1.0.1 · Android 7+ · 18 MB')],
    releases=[dict(id='launcher-1-0-1',version='1.0.1',date='2026-11-13',status='published',kind='release',platforms='Android 7+',title='Launcher for Android',summary='Custom drawers, cards, notifications and app updates.',body='<p>Named drawers and geometric symbols, monochrome app icons, adjustable themes and accents, weather, media controls, Android widgets and notification filters.</p><p>Check, download and install updates from Settings. Downloads are verified before Android opens its installer.</p>',created_at='2026-11-13T13:00:00+00:00',updated_at='2026-11-13T13:00:00+00:00')])
body=(HERE/'article.md').read_text(encoding='utf-8')
post=dict(slug='building-launcher-for-android',title='Launcher, a home screen with less noise',excerpt='Named app drawers, monochrome icons, useful cards and a few details that took more work than expected. Building a native Android launcher in C#.',body=body,cover=project['cover'],reading_minutes=max(1,round(len(body.split())/220)),published_at='2026-11-13T13:00:00+00:00',section='personal',tags=['Android','C#','.NET','Launcher'])
apk=ROOT/'artifacts/Launcher.apk'
release=dict(version='1.0.1',build=2,bytes=apk.stat().st_size,sha256=hashlib.sha256(apk.read_bytes()).hexdigest(),notes='https://kevlog.co.za/projects/launcher/#release-launcher-1-0-1')
payload=dict(project=project,gallery=gallery,post=post,release=release)
encoded=json.dumps(payload,ensure_ascii=False,indent=2)
assert '\u2014' not in encoded,'Em dash in public copy'
(HERE/'content.json').write_text(encoded+'\n',encoding='utf-8')
print(f'Prepared project, article, release and {len(gallery)} gallery items.')
