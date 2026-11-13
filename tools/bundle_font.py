from pathlib import Path
from io import BytesIO
import requests
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parents[1] / 'src' / 'Launcher' / 'Assets'
root.mkdir(parents=True, exist_ok=True)
url = 'https://raw.githubusercontent.com/google/fonts/main/ofl/outfit/Outfit%5Bwght%5D.ttf'
r = requests.get(url, timeout=45); r.raise_for_status()
for name, weight in [('Regular', 400), ('Bold', 700)]:
    font = TTFont(BytesIO(r.content))
    static = instantiateVariableFont(font, {'wght': weight}, inplace=True)
    static.save(root / f'Outfit-{name}.ttf')
license = requests.get('https://raw.githubusercontent.com/google/fonts/main/ofl/outfit/OFL.txt', timeout=30)
license.raise_for_status()
(root / 'Outfit-OFL.txt').write_text(license.text, encoding='utf-8')
print('Bundled Outfit Regular and Bold, SIL Open Font License.')
