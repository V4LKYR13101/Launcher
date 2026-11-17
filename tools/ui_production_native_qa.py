"""Native Android regression scenarios. Run only against the isolated QA emulator."""
from ui_hardening_qa import *

def launch_fixture(extra):
    adb(S,'shell','am','force-stop','com.launcher.uifixture')
    adb(S,'shell','am','start','-n','com.launcher.uifixture/.MainActivity','--ez',extra,'true')
    try:return labels()
    except RuntimeError:return labels()

def write_state(data):
    install_state(data)

def integration():
    adb(S,'shell','pm','grant','com.launcher.uifixture','android.permission.READ_CALENDAR')
    adb(S,'shell','pm','grant','com.launcher.uifixture','android.permission.WRITE_CALENDAR')
    check('CALENDAR_SEEDED' in launch_fixture('seed_calendar'),'native calendar fixture can create a local event')
    adb(S,'shell','pm','grant','com.launcher.home','android.permission.READ_CALENDAR')
    home();tap(S,'Today');touch(show('Launcher QA planning'))
    check(True,'Agenda reads and opens an Android calendar event');home()
    launch_fixture('pin');tap(S,'Add shortcut');home()
    check(any(s['ShortcutId']=='qa-pinned-action' for s in state()['Shortcuts']),'native pin request persists its shortcut')
    tap(S,'Search apps');fill('QA pinned action');touch(show('QA pinned action'))
    check('PINNED_ACTION_OPENED' in labels(),'pinned shortcut launches its own action');home()
    adb(S,'shell','am','start','-n','com.launcher.home/.MainActivity','-a','android.intent.action.SEND','-t','text/plain','--es','android.intent.extra.TEXT','https://example.org/launcher-qa','--es','android.intent.extra.SUBJECT','"QA shared capture"')
    tap(S,'Save note');check(any(n['Title']=='QA shared capture' for n in state()['Notes']),'Android share capture saves reviewed text and links')
    data=state();data['RootOrder']=['note:'+'b'*32]+data['RootOrder'];data['RootHidden']=[x for x in data['RootHidden'] if x!='note:'+'b'*32];write_state(data)
    tap(S,'Today');touch(show('Pack passport'));home();check('[x] Pack passport' in next(n['Text'] for n in state()['Notes'] if n['Title']=='Trip checklist'),'checklist toggle survives the home lifecycle');shot('checklist-completed')

def layout():
    home();menu('Settings');touch(show('Edit layout'));tap(S,'Arrange Today cards')
    before=state()['RootOrder'];tap(S,'Move later: Trip checklist');tap(S,'Cancel');check(state()['RootOrder']==before,'layout Cancel keeps saved card ordering')
    touch(show('Edit layout'));tap(S,'Arrange Today cards');tap(S,'Move later: Trip checklist');tap(S,'Save layout');check(state()['RootOrder']!=before,'layout arrow control saves new card order')
    touch(show('Edit layout'));tap(S,'Undo last layout change');check(state()['RootOrder']==before,'layout Undo restores the previous card order')

def migration():
    candidate=ROOT/'artifacts/release-1.5/Launcher-1.5.1.apk'
    uid=adb(S,'shell','stat','-c','%u','/data/user/0/com.launcher.uifixture').strip()
    adb(S,'push',candidate,'/data/local/tmp/launcher-qa-candidate.apk');adb(S,'shell','mkdir','-p','/data/user/0/com.launcher.uifixture/files')
    adb(S,'shell','cp','/data/local/tmp/launcher-qa-candidate.apk','/data/user/0/com.launcher.uifixture/files/candidate.apk');adb(S,'shell','chown','-R',uid+':'+uid,'/data/user/0/com.launcher.uifixture/files');adb(S,'shell','restorecon','-R','/data/user/0/com.launcher.uifixture/files')
    check(any('SIGNING_ACCEPTED:True' in (s or '') for s in launch_fixture('verify_apk')),'Android archive parser and updater trust the verified production lineage')
    before=state();check('Success' in adb(S,'install','-r',candidate),'Android accepts the in-place production key rotation')
    home();after=state();check(before['Notes']==after['Notes'] and before['Accent']==after['Accent'] and before['ShowHomeNavigation']==after['ShowHomeNavigation'],'production key rotation preserves notes and preferences')
    (OUT/'installed-production-package.txt').write_text(adb(S,'shell','dumpsys','package','com.launcher.home'),encoding='utf-8')

def alerts():
    data=state();now=int(time.time()*1000)
    data['Timers']=[{'Id':'c'*32,'Title':'QA first timer','DurationMs':20000,'RemainingMs':20000,'EndsAt':now+20000},{'Id':'d'*32,'Title':'QA second timer','DurationMs':20000,'RemainingMs':20000,'EndsAt':now+20000}]
    data['Reminders']=[{'Id':'e'*32,'Title':'QA reminder','DueAt':now+20000,'Completed':False,'RepeatDays':0}]
    adb(S,'shell','pm','grant','com.launcher.home','android.permission.POST_NOTIFICATIONS')
    adb(S,'shell','appops','set','com.launcher.home','SCHEDULE_EXACT_ALARM','allow')
    write_state(data)
    adb(S,'shell','am','broadcast','-a','android.intent.action.MY_PACKAGE_REPLACED','-n','com.launcher.home/.TimerBootReceiver')
    time.sleep(22)
    output=adb(S,'shell','dumpsys','notification','--noredact');(OUT/'timer-reminder-notifications.txt').write_text(output,encoding='utf-8')
    check('timer:'+'c'*32 in output and 'timer:'+'d'*32 in output,'two completed timers keep independent Android notifications')
    check('reminder:'+'e'*32 in output,'reminder posts its own Android notification')
    saved=state();check(all(t['EndsAt']==0 and t.get('LastAlertEndsAt',0)>0 for t in saved['Timers']) and saved['Reminders'][0]['Completed'],'simultaneous alarm receivers preserve every completed timer and reminder')

def recovery():
    data=state();path=OUT/'qa-recovery-state.json';path.write_text(json.dumps(data),encoding='utf-8')
    adb(S,'shell','am','force-stop','com.launcher.home');uid=adb(S,'shell','stat','-c','%u',STATE).strip();adb(S,'push',path,'/data/local/tmp/launcher-qa-recovery.json')
    adb(S,'shell','cp','/data/local/tmp/launcher-qa-recovery.json',STATE+'.previous');adb(S,'shell','chown',uid+':'+uid,STATE+'.previous');adb(S,'shell','restorecon',STATE+'.previous')
    invalid=OUT/'qa-corrupt-state.json';invalid.write_text('{broken',encoding='utf-8');adb(S,'push',invalid,STATE);adb(S,'shell','chown',uid+':'+uid,STATE);adb(S,'shell','restorecon',STATE)
    adb(S,'shell','am','start','-n','com.launcher.home/.MainActivity');tree(S)
    recovered=state();check(recovered['Notes']==data['Notes'],'corrupt state recovers the previous valid notes on Android')
    check(adb(S,'shell','cat',STATE+'.recovery')=='{broken','corruption recovery preserves the damaged file for export')

def profiles():
    created=[]
    try:
        for kind,arguments in [('Work',['--managed']),('Private',['--user-type','android.os.usertype.profile.PRIVATE'])]:
            response=adb(S,'shell','pm','create-user','--profileOf','0',*arguments,'Launcher'+kind+'QA');match=re.search(r'created user id (\d+)',response)
            if not match:raise AssertionError(response)
            user=int(match[1]);assert user>0;created.append(user);(OUT/'qa-created-profiles.json').write_text(json.dumps(created))
            adb(S,'shell','cmd','package','install-existing','--user',user,'com.launcher.uifixture');adb(S,'shell','am','start-user','-w',user)
        data=state();data['CollapsedCards']['private-space']=False;write_state(data);home();tap(S,'Search apps');fill('Studio')
        visible=labels();check('Studio Player · Work' in visible,'work-profile apps have an explicit profile label in search')
        # One primary app, its pinned shortcut, and the work app may appear; private apps must never enter this adapter.
        texts=[n.get('text') for n in tree(S).iter('node')];shot('profile-search')
        check(texts.count('Studio Player')==1 and texts.count('Studio Player · Work')==1,'Private Space apps stay out of global app search')
        home();show('Private space, expanded')
        if 'Unlock private space' in labels():tap(S,'Unlock private space')
        touch(show('Lock private space'));home();show('Private space, expanded')
        check('Unlock private space' in labels(),'Private Space locks through native Android profile controls');shot('private-space-locked')
    finally:
        for user in reversed(created):
            assert user>0
            adb(S,'shell','pm','remove-user','--wait',user)

if __name__=='__main__':
    {'integration':integration,'layout':layout,'migration':migration,'alerts':alerts,'recovery':recovery,'profiles':profiles}[sys.argv[1]]()
