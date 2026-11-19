package com.kevlog.launcher.discover;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.res.Configuration;
import android.net.Uri;
import android.os.Bundle;
import android.os.IBinder;
import android.os.Process;
import android.os.RemoteException;
import android.util.Log;
import android.view.View;
import android.view.WindowManager;

import com.google.android.libraries.launcherclient.ILauncherOverlay;
import com.google.android.libraries.launcherclient.ILauncherOverlayCallback;

public final class DiscoverClient implements ServiceConnection {

    private static final String TAG = "KevDiscover";
    private static final String GOOGLE =
            "com.google.android.googlequicksearchbox";
    private static final String ACTION =
            "com.android.launcher3.WINDOW_OVERLAY";

    private final Activity activity;

    private ILauncherOverlay overlay;
    private boolean bound;
    private boolean attached;
    private boolean resumed;

    private int apiVersion = 1;
    private volatile int serviceStatus;
    private String error = "";

    private final ILauncherOverlayCallback callback =
        new ILauncherOverlayCallback.Stub() {
            @Override
            public void overlayScrollChanged(float progress) {
                Log.d(TAG, "Overlay progress: " + progress);
            }

            @Override
            public void overlayStatusChanged(int status) {
                serviceStatus = status;
                Log.i(TAG, "Overlay status: " + status);
            }
        };

    public DiscoverClient(Activity activity) {
        this.activity = activity;
    }

    public int readyState() {
        if (overlay == null) return 0;
        return (serviceStatus & 1) != 0 ? 2 : 1;
    }

    public String lastError() {
        return error;
    }

    private void fail(String message, Exception exception) {
        error = message;
        if (exception == null) {
            Log.w(TAG, message);
        } else {
            Log.e(TAG, message, exception);
        }
    }

    public void connect() {
        if (bound) return;

        Intent intent = new Intent(ACTION);
        intent.setPackage(GOOGLE);

        Uri uri = Uri.parse(
            "app://" + activity.getPackageName() + ":" +
            Process.myUid());

        intent.setData(
            uri.buildUpon()
               .appendQueryParameter("v", "7")
               .appendQueryParameter("cv", "9")
               .build());

        try {
            ResolveInfo service =
                activity.getPackageManager().resolveService(
                    intent, PackageManager.GET_META_DATA);

            if (service == null) {
                fail("Google overlay service unavailable.", null);
                return;
            }

            if (service.serviceInfo.metaData != null) {
                apiVersion = service.serviceInfo.metaData.getInt(
                    "service.api.version", 1);
            }

            bound = activity.bindService(
                intent,
                this,
                Context.BIND_AUTO_CREATE | Context.BIND_IMPORTANT);

            if (!bound) {
                fail("Google rejected the service binding.", null);
            } else {
                Log.i(TAG, "Overlay binding requested.");
            }
        } catch (RuntimeException exception) {
            bound = false;
            fail("Unable to connect to Google Discover.", exception);
        }
    }

    @Override
    public void onServiceConnected(
            ComponentName name, IBinder binder) {

        overlay = ILauncherOverlay.Stub.asInterface(binder);

        Log.i(TAG, "Google overlay service connected: " + name);

        activity.runOnUiThread(this::attach);
    }

    @Override
    public void onServiceDisconnected(ComponentName name) {
        overlay = null;
        attached = false;
        serviceStatus = 0;

        Log.w(TAG, "Google overlay service disconnected.");
    }

    private void attach() {
        if (overlay == null || attached) return;

        View decor = activity.getWindow().getDecorView();

        if (!decor.isAttachedToWindow()) {
            decor.post(this::attach);
            return;
        }

        try {
            WindowManager.LayoutParams params =
                activity.getWindow().getAttributes();

            if (apiVersion >= 3) {
                Bundle data = new Bundle();

                data.putParcelable("layout_params", params);
                data.putParcelable(
                    "configuration",
                    new Configuration(
                        activity.getResources().getConfiguration()));

                data.putInt("client_options", 1);

                overlay.windowAttached2(data, callback);
            } else {
                overlay.windowAttached(params, callback, 1);
            }

            attached = true;
            sendActivityState();

            Log.i(TAG, "Google Discover window attached.");
        } catch (RemoteException | RuntimeException exception) {
            attached = false;
            fail("Overlay attachment failed.", exception);
        }
    }

    private void sendActivityState() {
        if (overlay == null || !attached) return;

        try {
            if (apiVersion >= 4) {
                overlay.setActivityState(resumed ? 3 : 1);
            } else if (resumed) {
                overlay.onResume();
            } else {
                overlay.onPause();
            }
        } catch (RemoteException exception) {
            fail("Could not update overlay state.", exception);
        }
    }

    public void resume() {
        resumed = true;

        if (!bound) connect();

        if (!attached) {
            attach();
        } else {
            sendActivityState();
        }
    }

    public void pause() {
        resumed = false;
        sendActivityState();
    }

    public boolean openOverlay() {
        if (readyState() != 2) return false;

        try {
            overlay.openOverlay(1);
            return true;
        } catch (RemoteException | RuntimeException exception) {
            fail("Could not open Google Discover.", exception);
            return false;
        }
    }

    public void closeOverlay() {
        if (overlay == null) return;

        try {
            overlay.closeOverlay(1);
        } catch (RemoteException exception) {
            fail("Could not close Google Discover.", exception);
        }
    }

    public void disconnect() {
        if (overlay != null && attached) {
            try {
                overlay.windowDetached(
                    activity.isChangingConfigurations());
            } catch (RemoteException exception) {
                Log.w(TAG, "Overlay detach failed.", exception);
            }
        }

        overlay = null;
        attached = false;
        serviceStatus = 0;

        if (bound) {
            try {
                activity.unbindService(this);
            } catch (IllegalArgumentException exception) {
                Log.w(TAG, "Overlay already unbound.", exception);
            }
        }

        bound = false;
    }
}
