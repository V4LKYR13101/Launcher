package com.launcher.wear;

import android.content.pm.PackageInfo;
import android.os.ParcelFileDescriptor;
import android.util.Log;
import androidx.wear.watchfacepush.WatchFacePushManager;
import androidx.wear.watchfacepush.WatchFacePushManagerFactory;
import com.google.android.gms.tasks.Task;
import com.google.android.gms.tasks.Tasks;
import com.google.android.gms.wearable.WearableListenerService;
import java.io.ByteArrayInputStream;
import java.io.DataInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import kotlin.ResultKt;
import kotlin.coroutines.Continuation;
import kotlin.coroutines.CoroutineContext;
import kotlin.coroutines.intrinsics.IntrinsicsKt;
import kotlinx.coroutines.Dispatchers;

public final class FacePushService extends WearableListenerService {
    private static final String PREFIX = "/launcher/watchface/v1/";
    private static final String PACKAGE = "com.launcher.home.watchfacepush.roll";
    private static final int MAX_REQUEST = 96 * 1024;
    private static final ExecutorService worker = Executors.newSingleThreadExecutor();

    @Override
    public Task<byte[]> onRequest(String nodeId, String path, byte[] data) {
        return Tasks.call(worker, () -> {
            try {
                return handle(path, data).getBytes(StandardCharsets.UTF_8);
            } catch (Exception error) {
                Log.w("LauncherWear", "Watch face request failed", error);
                return "failed".getBytes(StandardCharsets.UTF_8);
            }
        });
    }

    private String handle(String path, byte[] data) throws Exception {
        if (!WatchFacePushManagerFactory.isSupported()) return "unsupported";
        if (!path.equals(PREFIX + "version") && !path.equals(PREFIX + "install")) return "failed";
        WatchFacePushManager manager = WatchFacePushManagerFactory.createWatchFacePushManager(this);
        WatchFacePushManager.ListWatchFacesResponse slots = await(manager::listWatchFaces);
        WatchFacePushManager.WatchFaceDetails installed = slots.getInstalledWatchFaceDetails().stream()
            .filter(face -> face.getPackageName().equals(PACKAGE)).findFirst().orElse(null);
        if (path.equals(PREFIX + "version")) {
            return Long.toString(installed == null ? 0 : installed.getVersionCode());
        }
        if (data == null || data.length > MAX_REQUEST) return "failed";
        if (installed == null && slots.getRemainingSlotCount() == 0) return "no-slot";

        DataInputStream input = new DataInputStream(new ByteArrayInputStream(data));
        long build = input.readLong();
        int tokenLength = input.readUnsignedShort();
        if (tokenLength == 0 || tokenLength > 4096 || input.available() <= tokenLength) return "failed";
        String token = new String(input.readNBytes(tokenLength), StandardCharsets.UTF_8);
        if (build <= 0) return "failed";
        if (installed != null && build <= installed.getVersionCode()) return "current";

        File apk = File.createTempFile("launcher-roll-", ".apk", getCacheDir());
        try {
            try (FileOutputStream output = new FileOutputStream(apk)) {
                input.transferTo(output);
            }
            PackageInfo info = getPackageManager().getPackageArchiveInfo(apk.getAbsolutePath(), 0);
            if (info == null || !PACKAGE.equals(info.packageName) || info.getLongVersionCode() != build) return "failed";

            try (ParcelFileDescriptor fd = ParcelFileDescriptor.open(apk, ParcelFileDescriptor.MODE_READ_ONLY)) {
                if (installed == null) {
                    FacePushService.<WatchFacePushManager.WatchFaceDetails>await(
                        callback -> manager.addWatchFace(fd, token, callback));
                    return "added";
                }
                FacePushService.<WatchFacePushManager.WatchFaceDetails>await(
                    callback -> manager.updateWatchFace(installed.getSlotId(), fd, token, callback));
                return "updated";
            }
        } finally {
            apk.delete();
        }
    }

    private interface SuspendCall<T> {
        Object run(Continuation<T> callback) throws Exception;
    }

    // AndroidX exposes suspend methods. Adapt their completion on the worker thread.
    @SuppressWarnings("unchecked")
    private static <T> T await(SuspendCall<T> call) throws Exception {
        CompletableFuture<T> result = new CompletableFuture<>();
        Object immediate = call.run(new Continuation<T>() {
            @Override public CoroutineContext getContext() { return Dispatchers.getIO(); }
            @Override public void resumeWith(Object value) {
                try {
                    ResultKt.throwOnFailure(value);
                    result.complete((T) value);
                } catch (Throwable error) {
                    result.completeExceptionally(error);
                }
            }
        });
        return immediate == IntrinsicsKt.getCOROUTINE_SUSPENDED()
            ? result.get(25, TimeUnit.SECONDS) : (T) immediate;
    }
}
