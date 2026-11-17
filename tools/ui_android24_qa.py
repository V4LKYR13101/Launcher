"""Oldest supported 64-bit Android qualification, against a fresh dedicated emulator."""
from pathlib import Path
import json,time,sys,re
from device_qa import adb,tree,tap
S='emulator-5556';ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/production-hardening-2026-11-15';STATE='/data/user/0/com.launcher.home/files/launcher-state.json'
def data():return json.loads(adb(S,'shell','cat',STATE).lstrip('\ufeff'))
def check(value,message):
    assert value,message
    path=OUT/'android24-checks.json';items=json.loads(path.read_text()) if path.exists() else [];items.append(message);path.write_text(json.dumps(items,indent=2));print('Verified:',message,flush=True)
def screenshot(name):
    tree(S);(OUT/(name+'.png')).write_bytes(adb(S,'exec-out','screencap','-p',binary=True))
def stage():
    check(adb(S,'shell','getprop','ro.build.version.sdk').strip()=='24','runtime is Android 7 / API 24')
    check(adb(S,'shell','getprop','ro.product.cpu.abi').strip()=='x86_64','Android 7 is running the 64-bit launcher ABI')
    adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity','-a','android.intent.action.SEND','-t','text/plain','--es','android.intent.extra.TEXT','https://example.org/android7-check','--es','android.intent.extra.SUBJECT','"Android 7 keeper"');tap(S,'Save note');tree(S)
    check(any(note['Title']=='Android 7 keeper' for note in data()['Notes']),'native share capture works on Android 7');(OUT/'android24-before-upgrade.json').write_text(json.dumps(data()));screenshot('android24-before-upgrade')
def upgrade():
    before=json.loads((OUT/'android24-before-upgrade.json').read_text());check('Success' in adb(S,'install','-r',ROOT/'artifacts/release-1.5/Launcher-1.5.1.apk'),'production APK updates Android 7 in place through its compatible v2 signer');adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity');tree(S)
    check(data()['Notes']==before['Notes'],'Android 7 upgrade preserves captured notes');screenshot('android24-production')
    # Stage while the system launcher is foreground so Android cannot recreate HOME during file replacement.
    adb(S,'shell','am','start','-W','-n','com.android.launcher3/.Launcher');adb(S,'shell','am','force-stop','com.launcher.home');saved=data();now=int(time.time()*1000);due=now+30000;saved['Timers']=[{'Id':'7'*32,'Title':'Android 7 restart timer','DurationMs':30000,'RemainingMs':30000,'EndsAt':due}]
    fixture=OUT/'android24-timer-state.json';fixture.write_text(json.dumps(saved));uid=adb(S,'shell','stat','-c','%u',STATE).strip();adb(S,'push',fixture,'/data/local/tmp/launcher-min-state.json');adb(S,'shell','cp','/data/local/tmp/launcher-min-state.json',STATE);adb(S,'shell','chown',uid+':'+uid,STATE);adb(S,'shell','restorecon',STATE);adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity');tree(S)
    adb(S,'shell','am','start','-W','-n','com.android.launcher3/.Launcher');adb(S,'shell','am','force-stop','com.launcher.home');adb(S,'shell','am','start','-W','-n','com.launcher.home/.MainActivity');time.sleep(max(0,min(35,(due-int(time.time()*1000))/1000+3)))
    notification=adb(S,'shell','dumpsys','notification');(OUT/'android24-notifications.txt').write_text(notification);check('timer:'+'7'*32 in notification and data()['Timers'][0]['EndsAt']==0,'Android 7 reschedules and delivers a timer after force-stop and relaunch')
    (OUT/'android24-package.txt').write_text(adb(S,'shell','dumpsys','package','com.launcher.home'))
if __name__=='__main__':{'stage':stage,'upgrade':upgrade}[sys.argv[1]]()
