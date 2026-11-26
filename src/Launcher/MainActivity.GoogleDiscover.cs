using Com.Kevlog.Launcher.Discover;

namespace LauncherApp;

public partial class MainActivity
{
    private DiscoverClient? discoverClient;

    private bool discoverOpenPending;

    private bool GoogleDiscoverEnabled =>
        state.LeftFeedProvider == "Google";

    // Create or release the Google Discover connection
    // based on the user's selected feed provider.
    private void SyncGoogleDiscover()
    {
        if (!GoogleDiscoverEnabled)
        {
            DisposeGoogleDiscover();
            return;
        }

        discoverClient ??= new DiscoverClient(this);

        discoverClient.Connect();

        if (resumed)
        {
            discoverClient.Resume();
        }
    }

    // Called when Launcher goes into the background.
    private void PauseGoogleDiscover()
    {
        discoverClient?.Pause();
    }

    // Called when Launcher is destroyed, or when
    // Google Discover is disabled.
    private void DisposeGoogleDiscover()
    {
        discoverOpenPending = false;

        if (discoverClient is null)
            return;

        discoverClient.Disconnect();
        discoverClient.Dispose();

        discoverClient = null;
    }

    // Called when the user swipes right from Today.
    private void OpenNativeGoogleDiscover()
    {
        if (!GoogleDiscoverEnabled || discoverOpenPending)
            return;

        SyncGoogleDiscover();

        // The Java service connects asynchronously.
        // If it is ready, open the native overlay.
        if (TryOpenNativeDiscover())
            return;

        // Give Android a brief chance to finish
        // the service connection before falling back.
        discoverOpenPending = true;

        handler.PostDelayed(() =>
        {
            if (!discoverOpenPending)
                return;

            discoverOpenPending = false;

            if (IsDestroyed || !resumed ||
                !GoogleDiscoverEnabled)
                return;

            if (TryOpenNativeDiscover())
                return;

            // Native integration unavailable.
            // Open the installed Google application.
            OpenGoogleApplicationFallback();

        }, 1000);
    }

    private bool TryOpenNativeDiscover()
    {
        if (discoverClient is null)
            return false;

        // Java readyState() becomes ReadyState()
        // in the generated .NET Android binding.
        if (discoverClient.ReadyState() != 2)
            return false;

        return discoverClient.OpenOverlay();
    }
}
