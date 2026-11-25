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
    private bool PackageLocked(string package) => state.Apps.Any(p => p.Key.StartsWith(package + "/", StringComparison.Ordinal) && p.Value.BiometricLocked);
    private bool ConcealPreview(string package) => state.HideProtectedPreviews && PackageLocked(package);
    private void WithProtectedSettings(Action action)
    {
        if (state.Apps.Values.Any(r => r.BiometricLocked))
            AuthenticateDevice(Localize(Resource.String.ui_protected_settings), action);
        else
            action();
    }
    private void WithAppUnlock(string package, Action action)
    {
        if (!PackageLocked(package))
        {
            action();
            return;
        }
        AuthenticateDevice(apps.FirstOrDefault(a => a.PackageName == package)?.Label ?? Localize(Resource.String.ui_app_2), action);
    }
    private void AuthenticateDevice(string label, Action action)
    {
        if (authenticationPending)
            return;
        var keyguard = (KeyguardManager?)GetSystemService(KeyguardService);
        if (keyguard?.IsDeviceSecure != true)
        {
            Toast(Localize(Resource.String.ui_set_up_a_screen_lock_and_biometrics_in_android));
            return;
        }
        HideKeyboard();
        authenticationPending = true;
        void Succeeded()
        {
            if (!authenticationPending || IsDestroyed)
                return;
            authenticationPending = false;
            appAuthentication = null;
            action();
        }
        void Credential()
        {
            authenticationPending = true;
#pragma warning disable CA1422 // Legacy Android fallback if native biometric authentication is unavailable.
            var intent = keyguard.CreateConfirmDeviceCredentialIntent(Localize(Resource.String.ui_unlock) + label, Localize(Resource.String.ui_confirm_your_screen_lock_to_continue));
#pragma warning restore CA1422
            if (intent is null)
            {
                authenticationPending = false;
                Toast(Localize(Resource.String.ui_screen_lock_authentication_is_unavailable));
                return;
            }
            credentialAction = Succeeded;
            StartActivityForResult(intent, 811);
        }
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            Credential();
            return;
        }
        try
        {
            var builder = new BiometricPrompt.Builder(this).SetTitle(Localize(Resource.String.ui_unlock) + label)!
                .SetSubtitle(Localize(Resource.String.ui_authenticate_to_continue_in_launcher))!;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                builder.SetAllowedAuthenticators(0x000F | 0x8000);
            else if (OperatingSystem.IsAndroidVersionAtLeast(29))
                builder.SetDeviceCredentialAllowed(true);
            else
                builder.SetNegativeButton(Localize(Resource.String.ui_use_screen_lock), MainExecutor!, new AuthenticationFallback(Credential));
            appAuthentication = new CancellationSignal();
            builder.Build()!.Authenticate(appAuthentication, MainExecutor!, new AppAuthenticationCallback(Succeeded, () =>
            {
                // API 28's negative button opens the separate credential activity.
                if (credentialAction is null)
                {
                    authenticationPending = false;
                    appAuthentication = null;
                }
            }));
        }
        catch (Java.Lang.RuntimeException) { Credential(); }
    }
    private void CompleteCredentialAuthentication(Result result)
    {
        var action = credentialAction;
        credentialAction = null;
        if (result == Result.Ok)
            action?.Invoke();
        else
            authenticationPending = false;
    }
    private sealed class AppAuthenticationCallback(Action success, Action failed) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result) => success();
        public override void OnAuthenticationError(BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString) => failed();
    }
    private sealed class AuthenticationFallback(Action action) : Java.Lang.Object, IDialogInterfaceOnClickListener
    {
        public void OnClick(IDialogInterface? dialog, int which) => action();
    }

    private void SetAppLock(AppEntry app, bool locked)
    {
        AuthenticateDevice(app.Label, () =>
        {
            foreach (var entry in state.Apps.Where(p => p.Key.StartsWith(app.PackageName + "/", StringComparison.Ordinal)))
                entry.Value.BiometricLocked = locked;
            Save();
            Render();
        });
    }
    private void OpenNotification(Services.Conversation notification) => WithAppUnlock(notification.Package, () =>
    {
        HideKeyboard();
        if (Services.ConversationService.TryOpenActivity(this, notification))
            return;
        // A notification may have no content intent, or its token may have expired.
        // Launch the source app directly, as with the now-playing card.
        try
        {
            var launch = PackageManager?.GetLaunchIntentForPackage(notification.Package);
            if (launch is not null)
            {
                launch.AddFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);
                StartActivity(launch);
                return;
            }
            var entry = apps.FirstOrDefault(a => a.PackageName == notification.Package);
            if (entry is not null)
            {
                StartAppDirect(entry);
                return;
            }
        }
        catch (Exception e) when (e is ActivityNotFoundException or Java.Lang.SecurityException) { }
        Toast(Localize(Resource.String.ui_this_notification_no_longer_has_an_available_app_screen));
    });
}
