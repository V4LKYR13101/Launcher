using Android.App;
using Android.Content;
using Android.Hardware.Biometrics;
using Android.OS;
using Launcher.Core;

namespace LauncherApp;

public partial class MainActivity
{
    private CancellationSignal? appAuthentication;
    private Action? credentialAction;
    private bool authenticationPending;
    private bool PackageLocked(string package) => state.Apps.Any(p=>p.Key.StartsWith(package+"/",StringComparison.Ordinal)&&p.Value.BiometricLocked);
    private void WithAppUnlock(string package,Action action)
    {
        if(!PackageLocked(package)){action();return;}
        AuthenticateDevice(apps.FirstOrDefault(a=>a.PackageName==package)?.Label??"app",action);
    }
    private void AuthenticateDevice(string label,Action action)
    {
        if(authenticationPending)return;
        var keyguard=(KeyguardManager?)GetSystemService(KeyguardService);
        if(keyguard?.IsDeviceSecure!=true){Toast("Set up a screen lock and biometrics in Android settings first.");return;}
        HideKeyboard();authenticationPending=true;
        void Succeeded()
        {
            if(!authenticationPending||IsDestroyed)return;
            authenticationPending=false;appAuthentication=null;action();
        }
        void Credential()
        {
            authenticationPending=true;
#pragma warning disable CA1422 // Legacy Android fallback if native biometric authentication is unavailable.
            var intent=keyguard.CreateConfirmDeviceCredentialIntent("Unlock "+label,"Confirm your screen lock to continue.");
#pragma warning restore CA1422
            if(intent is null){authenticationPending=false;Toast("Screen lock authentication is unavailable.");return;}
            credentialAction=Succeeded;StartActivityForResult(intent,811);
        }
        if(!OperatingSystem.IsAndroidVersionAtLeast(28)){Credential();return;}
        try
        {
            var builder=new BiometricPrompt.Builder(this).SetTitle("Unlock "+label)!
                .SetSubtitle("Authenticate to continue in Launcher.")!;
            if(OperatingSystem.IsAndroidVersionAtLeast(30))builder.SetAllowedAuthenticators(0x000F|0x8000);
            else if(OperatingSystem.IsAndroidVersionAtLeast(29))builder.SetDeviceCredentialAllowed(true);
            else builder.SetNegativeButton("Use screen lock",MainExecutor!,new AuthenticationFallback(Credential));
            appAuthentication=new CancellationSignal();
            builder.Build()!.Authenticate(appAuthentication,MainExecutor!,new AppAuthenticationCallback(Succeeded,()=>
            {
                // API 28's negative button opens the separate credential activity.
                if(credentialAction is null){authenticationPending=false;appAuthentication=null;}
            }));
        }
        catch(Java.Lang.RuntimeException){Credential();}
    }
    private void CompleteCredentialAuthentication(Result result)
    {
        var action=credentialAction;credentialAction=null;
        if(result==Result.Ok)action?.Invoke();
        else authenticationPending=false;
    }
    private sealed class AppAuthenticationCallback(Action success,Action failed):BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)=>success();
        public override void OnAuthenticationError(BiometricErrorCode errorCode,Java.Lang.ICharSequence? errString)=>failed();
    }
    private sealed class AuthenticationFallback(Action action):Java.Lang.Object,IDialogInterfaceOnClickListener
    {public void OnClick(IDialogInterface? dialog,int which)=>action();}

    private void SetAppLock(AppEntry app,bool locked)
    {
        AuthenticateDevice(app.Label,()=>
        {
            foreach(var entry in state.Apps.Where(p=>p.Key.StartsWith(app.PackageName+"/",StringComparison.Ordinal)))entry.Value.BiometricLocked=locked;
            Save();Render();
        });
    }
    private void OpenNotification(Services.Conversation notification)=>WithAppUnlock(notification.Package,()=>
    {
        try{notification.Open?.Send();}
        catch(PendingIntent.CanceledException){Toast("This notification is no longer available.");}
    });
}
