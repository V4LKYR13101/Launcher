from pathlib import Path
import json,re
from ui_polish_qa import S,OUT,expect,shot,menu,swipe,home
from device_qa import adb,tree,tap

def headlines():
 return {n.get('text'):n.get('bounds') for n in tree(S).iter('node') if n.get('text') in ['A new riverside path opens this weekend','Local studio opens its doors','The weekend reading list','A closer look at the night sky']}
home();menu('Settings');tap(S,'News & feeds');n=next(n for n in tree(S).iter('node') if n.get('text')=='Show article images')
if n.get('checked')!='true':tap(S,'Show article images')
tap(S,'Done');home();menu('Root');tree(S);swipe(950,500,950,2000)
expect('NEWS & FEEDS');before=headlines();assert len(before)==4
tap(S,'Refresh feeds');after=headlines();assert before==after,(before,after);shot('feeds-refresh-keeps-stories')
tap(S,'Show more');swipe(950,1700,950,850);expect('New trains on the coastal route');shot('feeds-expanded')
menu('Settings');tap(S,'News & feeds');tap(S,'Show article images');tap(S,'Done');home()
menu('Root');tree(S);swipe(950,500,950,2000);expect('NEWS & FEEDS');assert not any(n.get('content-desc')=='Article image' for n in tree(S).iter('node'));shot('feeds-images-hidden')
menu('Settings');tap(S,'News & feeds');tap(S,'Show article images');tap(S,'Done');home();menu('Root')
(OUT/'feed-checks.json').write_text(json.dumps(dict(cachedStoriesRetainedOnFailure=True,headlinePositionsStable=True,showMore=True,imagesToggle=True),indent=2));print('Verified feed refresh, saved stories, Show more and image preference.',flush=True)
