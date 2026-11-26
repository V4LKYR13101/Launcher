
package com.google.android.libraries.launcherclient;

oneway interface ILauncherOverlayCallback {
    void overlayScrollChanged(float progress);
    void overlayStatusChanged(int status);
}
