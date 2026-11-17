from pathlib import Path
import subprocess,zipfile
sdk=Path('C:/Program Files (x86)/Android/android-sdk');build=sdk/'build-tools/37.0.0';java=Path('C:/Program Files/Eclipse Adoptium/jdk-21.0.12.101-hotspot/bin');jar=sdk/'platforms/android-37.0/android.jar';source=Path('tools/overlay-inspector');out=Path('artifacts/overlay-inspector');out.mkdir(exist_ok=True);classes=out/'classes';classes.mkdir(exist_ok=True)
def run(args):subprocess.run([str(a) for a in args],check=True)
run([java/'javac.exe','-source','8','-target','8','-classpath',jar,'-d',classes,source/'Inspector.java'])
run([build/'d8.bat','--lib',jar,'--output',out,*classes.rglob('*.class')])
run([build/'aapt2.exe','link','-o',out/'unsigned.apk','-I',jar,'--manifest',source/'AndroidManifest.xml'])
with zipfile.ZipFile(out/'unsigned.apk','a') as z:z.write(out/'classes.dex','classes.dex')
run([build/'zipalign.exe','-f','4',out/'unsigned.apk',out/'aligned.apk'])
run([build/'apksigner.bat','sign','--ks',Path.home()/'AppData/Local/Xamarin/Mono for Android/debug.keystore','--ks-pass','pass:android','--out',out/'inspector.apk',out/'aligned.apk'])
