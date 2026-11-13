from pathlib import Path
import json,subprocess,xml.etree.ElementTree as ET,time
from device_qa import adb,tree

S='emulator-5570'
ADB='C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe'
folder=Path(__file__).resolve().parents[1]/'artifacts/showcase/screenshots'
def capture(theme,accent,name):
    xml=ET.fromstring(adb(S,'shell','run-as','com.launcher.home','cat','shared_prefs/launcher-state.xml'))
    node=xml.find("string[@name='state']");state=json.loads(node.text)
    state.update(Theme=theme,Accent=accent,ShowMediaCard=True)
    for drawer in state['Drawers']:drawer['Expanded']=drawer['Id'] in ['essentials','messaging']
    node.text=json.dumps(state)
    adb(S,'shell','am','force-stop','com.launcher.home')
    subprocess.run([ADB,'-s',S,'shell','run-as','com.launcher.home','tee','shared_prefs/launcher-state.xml'],
        input=ET.tostring(xml,encoding='utf-8'),capture_output=True,check=True)
    adb(S,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity');time.sleep(8);tree(S)
    (folder/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True));print('Captured',name,flush=True)
capture('Light','#375C48','light')
capture('Dark','#9FE7C1','mint')
capture('Dark','#E4FF00','home')
