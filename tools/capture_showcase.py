"""Capture actual Launcher UI with sample data on the isolated emulator only."""
from pathlib import Path
import time,json,re
from device_qa import adb,tree,tap

S='emulator-5570'
OUT=Path(__file__).resolve().parents[1]/'artifacts/showcase/screenshots'
OUT.mkdir(parents=True,exist_ok=True)
def click(label,hold=False):tap(S,label,hold);time.sleep(.3)
def shot(name):
    tree(S) # Ensure the current UI has settled before taking the actual capture.
    (OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True));print('Captured',name,flush=True)
def home():
    adb(S,'shell','am','start','-n','com.launcher.home/com.launcher.home.MainActivity','-a','android.intent.action.MAIN','-c','android.intent.category.HOME');time.sleep(.4)
def menu(label):click('Launcher settings');click(label)
def main():
    home();shot('home')
    click('Essentials, expanded');click('Messaging, expanded');shot('drawers')
    click('Essentials, collapsed',True);click('Drawer icon');shot('drawer-icons');adb(S,'shell','input','keyevent','4')
    home();menu('Root');shot('root')
    home();menu('Notifications');shot('conversations')
    click('Filter notification apps');shot('notification-filters');adb(S,'shell','input','keyevent','4')
    home();click('Open quick panel');shot('quick-panel');click('Close quick panel')
    menu('Settings');click('App updates');shot('updates')
    home();menu('Settings');click('Weather');shot('weather-settings')
    home();menu('Settings');click('Appearance');shot('appearance')
    home();click('Search apps');adb(S,'shell','input','text','Chrome');time.sleep(.3);shot('search');adb(S,'shell','input','keyevent','4');adb(S,'shell','input','keyevent','4')
    home()

if __name__ == "__main__":
    main()
