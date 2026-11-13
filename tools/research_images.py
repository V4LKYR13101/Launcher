from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import requests
from PIL import Image, ImageOps, ImageDraw

root = Path(__file__).resolve().parents[1] / 'docs' / 'research'
root.mkdir(parents=True, exist_ok=True)
urls = [
('play-01', 'https://play-lh.googleusercontent.com/UjiSSDhTenCAwGRVRedV9qD9H8eGR3BovHqt-udxzHbYSej5fkwAUW8ugl326SszrEKzQgDV6yd6nt7ACPKIpw=w1000'),
('play-02', 'https://play-lh.googleusercontent.com/VbK62wbfaYVx4oaWViaI55PXpNyVV2CK7Dtdm_fMjgdUvxMuUhN_lxTwFEBZNIUeJoxQY5qKALPRTseDuiY4kA=w1000'),
('play-03', 'https://play-lh.googleusercontent.com/CGp59KWDjBJjo48Rm3J7jdPHRdTposW8xiNhEZXNYB7Xw6Uu5GaNxYgUMxXBrbyhDqQi47Fmxx6AxgomnGI9Fw=w1000'),
('play-04', 'https://play-lh.googleusercontent.com/zETShi7tUYe6Xb3x1BxgJCO6kghURMmK_Fzto8T_h-axQDmSlFCGxMG3wPZNB4y3KnEQgO0myy_RkjueMPCaHq0=w1000'),
('play-05', 'https://play-lh.googleusercontent.com/UBe8gTfzduqZ_0uUU-qkMoKwgsVCWjFVjq7TsBFgFQZ4zpBOI3Fg1PkYEbBdB7md0_5MEW2qcJ1DqyYdxA5DoA=w1000'),
('play-06', 'https://play-lh.googleusercontent.com/SpzMeu6xZEW7CbRNSAAb5rD7ZrXpwQWTHjCez0cHQTbarSAGGTEuRsLWebUrMmAcuoDCrTNweIjXx9qnbmCkvw=w1000'),
('play-07', 'https://play-lh.googleusercontent.com/RwoQ8aPD9an9ZbAe3er0IccUgAzQiCBhDuvpLGbVkjGyKMDLLjTAaKVJB-8U-nf5YPZS980C6GkSL8T3np0WJw=w1000'),
('play-08', 'https://play-lh.googleusercontent.com/uQ0TSzem_zYk3ZvUSFQjrPNorWWZQ9LOs4SGZMmNGDbtuNWV5qHMLCeJShQo3ZPxv4xt2sUBLNSsD5EsoDdBOA=w1000'),
('drawers', 'https://cdn.blessthisstuff.com/imagens/stuff/blloc-ratio-launcher-4.jpg'),
('overview', 'https://www.blessthisstuff.com/imagens/listagem/2020/grande/grande_blloc-ratio-launcher.jpg'),
('guide', 'https://i.blogs.es/dc32dd/ratio-launcher-1/650_1200.jpg'),
('tree', 'https://i.blogs.es/a88539/ratio-launcher-3/650_1200.jpg'),
('review', 'https://hd2.tudocdn.net/946255?h=494&w=824'),
('root', 'https://miro.medium.com/v2/resize:fit:1358/1*Nc4Ym4xcES9VQm_cNRsq1w.png'),
]
def fetch(item):
    name, url = item
    try:
        res = requests.get(url, timeout=30); res.raise_for_status()
        im = Image.open(BytesIO(res.content)).convert('RGB')
        im.save(root / (name + '.jpg'))
        return name, im
    except Exception as e:
        print(name, type(e).__name__)
        return name, None
results = list(ThreadPoolExecutor(max_workers=8).map(fetch, urls))
sheet = Image.new('RGB', (1600, 480 * 4), '#eeeeee')
d = ImageDraw.Draw(sheet)
for i, (name, im) in enumerate(results):
    x, y = (i % 4) * 400, (i // 4) * 480
    d.text((x+12, y+8), name, fill='black')
    if im:
        im.thumbnail((380, 440))
        sheet.paste(im, (x+(400-im.width)//2, y+30))
sheet.save(root / 'contact-sheet.jpg')
print(f'Inspected sources: {sum(im is not None for _, im in results)}/{len(urls)}')
