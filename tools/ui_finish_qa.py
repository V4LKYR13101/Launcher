"""Focused regressions for the final UI controls, using local emulator fixtures only."""
from ui_production_native_qa import *

def controls():
    home();menu('Settings');fill('');touch(show('Edit layout'));check('Close Edit your layout' in labels(),'sheets expose a named close button');tap(S,'Close Edit your layout')
    home();menu('Settings');fill('tabs');check('Appearance' in labels(),'settings search finds page-tab controls through Appearance');fill('zznomatchzz');check(any('No settings match' in (value or '') for value in labels()),'settings search explains an empty result');fill('');home()
    data=state();camera=next(key for key in data['Apps'] if key.startswith('com.android.camera2/'));chrome=next(key for key in data['Apps'] if key.startswith('com.android.chrome/'))
    data['Apps'][camera].update(Pinned=True,Order=0,Hidden=False);data['Apps'][chrome].update(Pinned=True,Order=1,Hidden=False);write_state(data)
    menu('Settings');touch(show('Edit layout'));tap(S,'Arrange favorites');tap(S,'Move later: Camera');tap(S,'Save layout');saved=state();check(saved['Apps'][camera]['Order']>saved['Apps'][chrome]['Order'],'favorites can be reordered across the dock')
    touch(show('Edit layout'));tap(S,'Undo last layout change');saved=state();check(saved['Apps'][camera]['Order']<saved['Apps'][chrome]['Order'],'favorite ordering participates in layout Undo');home()
    launch_fixture('pin');tap(S,'Add shortcut');home();tap(S,'Search apps');fill('QA pinned action');tap(S,'QA pinned action',True);touch(show('Remove shortcut'));tap(S,'Remove shortcut');check(not any(item['ShortcutId']=='qa-pinned-action' for item in state()['Shortcuts']),'native pinned shortcuts can be removed without uninstalling the app');home();shot('apps-final-sky')

def private_controls():
    data=state();data['ShowPrivateSpace']=True;data['HidePrivateSpaceWhenLocked']=False;data['CollapsedCards']['private-space']=False;write_state(data);home();show('Private space, expanded')
    current=labels()
    if 'Lock private space' not in current:
        try:touch(show('Unlock private space'))
        except AssertionError:pass
    touch(show('Lock private space'));show('Private space, expanded');touch(show('Unlock private space'));check(True,'Private Space locks and unlocks through Android');shot('private-space-unlocked')
    home();menu('Settings');touch(show('Setup & feature health'));touch(show('Profiles & private space'));tap(S,'Show Private Space in Apps');check(not state()['ShowPrivateSpace'],'Private Space container has an explicit hide/show preference');tap(S,'Show Private Space in Apps');tap(S,'Hide Private Space when locked');check(state()['HidePrivateSpaceWhenLocked'],'Private Space can hide itself while locked');tap(S,'Close Profiles & private space')
    private_tail()

def private_tail():
    home();tap(S,'Apps, selected');show('Private space, expanded');touch(show('Lock private space'));home();tap(S,'Search apps');fill('Studio');texts=[n.get('text') for n in tree(S).iter('node')];check(texts.count('Studio Player')==1,'locked Private Space apps are absent from search');shot('private-space-hidden-search')
    fill('private space');tap(S,'Private space\nPrivate space');check('Profiles & private space' in labels(),'explicit Private Space search opens its hidden-container controls');tap(S,'Close Profiles & private space');home()

def privacy():
    data=state();before=json.loads(json.dumps(data));fixture=next(key for key in data['Apps'] if key.startswith('com.launcher.uifixture/') and '@profile:' not in key and '/shortcut:' not in key);data['Apps'][fixture]['BiometricLocked']=True;data['HideProtectedPreviews']=True;data['ConversationExcludedApps']=[];write_state(data)
    try:
        adb(S,'shell','pm','grant','com.launcher.uifixture','android.permission.POST_NOTIFICATIONS');launch_fixture('sample');home();tap(S,'Notifications');visible=labels();check('Protected notification' in visible,'protected notifications show a redacted title');check(not any('Coffee after work' in (value or '') or 'photos from this weekend' in (value or '') for value in visible),'protected message bodies stay out of the notification page');shot('protected-notifications')
        home();tap(S,'Open quick panel');visible=labels();check(not any('Coffee after work' in (value or '') or 'photos from this weekend' in (value or '') for value in visible),'quick panel also redacts protected message content')
    finally:write_state(before)

def tabs_and_reminder():
    appearance();touch(show('Page tab buttons'));tap(S,'Notifications');tap(S,'Done');home();check('Notifications' not in labels(),'Notifications tab can be hidden independently');check('Today' in labels() and 'Apps, selected' in labels(),'other page buttons remain visible');shot('apps-two-tabs')
    appearance();touch(show('Page tab buttons'));tap(S,'Notifications');tap(S,'Done');home();tap(S,'Today');tap(S,'+ Capture');tap(S,'Reminder');fill('QA picker reminder')
    root=tree(S);date=next(n for n in root.iter('node') if n.get('text','').startswith('Date · '));touch(date);check(any(n.get('class')=='android.widget.DatePicker' for n in tree(S).iter('node')),'reminder date uses the native date picker');back()
    root=tree(S);clock=next(n for n in root.iter('node') if n.get('text','').startswith('Time · '));touch(clock);check(any(n.get('class')=='android.widget.TimePicker' for n in tree(S).iter('node')),'reminder time uses the native time picker');back();touch(show('Repeat · Once'));tap(S,'Daily');check('Repeat · Daily' in labels(),'reminder repeat choice updates its visible summary');tap(S,'Save reminder');check(any(item['Title']=='QA picker reminder' and item['RepeatDays']==1 for item in state()['Reminders']),'picker-based reminder saves its recurrence');shot('reminder-created')

if __name__=='__main__':
    {'controls':controls,'private':private_controls,'private_tail':private_tail,'privacy':privacy,'tabs_reminder':tabs_and_reminder}[sys.argv[1]]()
