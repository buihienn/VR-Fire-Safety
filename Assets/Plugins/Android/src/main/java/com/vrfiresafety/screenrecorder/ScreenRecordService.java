package com.vrfiresafety.screenrecorder;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.MediaRecorder;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.IBinder;
import android.view.Surface;
import com.unity3d.player.UnityPlayer;

public class ScreenRecordService extends Service {
    public static final String ACTION_START = "com.vrfiresafety.screenrecorder.START";
    public static final String ACTION_STOP = "com.vrfiresafety.screenrecorder.STOP";

    public static final String EXTRA_RESULT_CODE = "resultCode";
    public static final String EXTRA_RESULT_DATA = "resultData";
    public static final String EXTRA_OUTPUT_PATH = "outputPath";
    public static final String EXTRA_WIDTH = "width";
    public static final String EXTRA_HEIGHT = "height";
    public static final String EXTRA_FPS = "fps";
    public static final String EXTRA_BITRATE = "bitrate";
    public static final String EXTRA_UNITY_CALLBACK_OBJECT = "unityCallbackObject";

    private static final String CHANNEL_ID = "screen_recording";
    private static final int NOTIFICATION_ID = 2702;

    private MediaProjection mediaProjection;
    private MediaProjectionManager projectionManager;
    private VirtualDisplay virtualDisplay;
    private MediaRecorder mediaRecorder;
    private HandlerThread handlerThread;
    private Handler handler;
    private String outputPath;
    private String unityCallbackObject;

    private final MediaProjection.Callback projectionCallback = new MediaProjection.Callback() {
        @Override
        public void onStop() {
            stopRecordingInternal(false);
        }
    };

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null) {
            return START_NOT_STICKY;
        }

        String action = intent.getAction();
        if (ACTION_STOP.equals(action)) {
            stopRecordingInternal(true);
            return START_NOT_STICKY;
        }

        if (ACTION_START.equals(action)) {
            startForegroundForProjection();
            startRecording(intent);
        }

        return START_STICKY;
    }

    private void startForegroundForProjection() {
        NotificationManager notificationManager = getSystemService(NotificationManager.class);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID,
                    "Screen recording",
                    NotificationManager.IMPORTANCE_LOW);
            notificationManager.createNotificationChannel(channel);
        }

        Notification.Builder builder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);

        Notification notification = builder
                .setSmallIcon(getApplicationInfo().icon)
                .setContentTitle("Recording gameplay")
                .setContentText("VR Fire Safety is recording this session.")
                .setOngoing(true)
                .build();

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, notification, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
        } else {
            startForeground(NOTIFICATION_ID, notification);
        }
    }

    private void startRecording(Intent intent) {
        outputPath = intent.getStringExtra(EXTRA_OUTPUT_PATH);
        unityCallbackObject = intent.getStringExtra(EXTRA_UNITY_CALLBACK_OBJECT);
        int width = intent.getIntExtra(EXTRA_WIDTH, 1280);
        int height = intent.getIntExtra(EXTRA_HEIGHT, 720);
        int fps = intent.getIntExtra(EXTRA_FPS, 30);
        int bitrate = intent.getIntExtra(EXTRA_BITRATE, 8000000);
        int resultCode = intent.getIntExtra(EXTRA_RESULT_CODE, 0);
        Intent resultData = intent.getParcelableExtra(EXTRA_RESULT_DATA);

        try {
            handlerThread = new HandlerThread("ScreenRecordThread");
            handlerThread.start();
            handler = new Handler(handlerThread.getLooper());

            mediaRecorder = Build.VERSION.SDK_INT >= Build.VERSION_CODES.S
                    ? new MediaRecorder(this)
                    : new MediaRecorder();
            mediaRecorder.setVideoSource(MediaRecorder.VideoSource.SURFACE);
            mediaRecorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
            mediaRecorder.setOutputFile(outputPath);
            mediaRecorder.setVideoEncoder(MediaRecorder.VideoEncoder.H264);
            mediaRecorder.setVideoSize(width, height);
            mediaRecorder.setVideoFrameRate(fps);
            mediaRecorder.setVideoEncodingBitRate(bitrate);
            mediaRecorder.prepare();

            projectionManager = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
            mediaProjection = projectionManager.getMediaProjection(resultCode, resultData);
            mediaProjection.registerCallback(projectionCallback, handler);

            Surface surface = mediaRecorder.getSurface();
            virtualDisplay = mediaProjection.createVirtualDisplay(
                    "VRFireSafetyScreenRecord",
                    width,
                    height,
                    getResources().getConfiguration().densityDpi,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                    surface,
                    null,
                    handler);

            mediaRecorder.start();
            sendUnityMessage(unityCallbackObject, "OnScreenRecordStarted", outputPath);
        } catch (Exception exception) {
            sendUnityMessage(unityCallbackObject, "OnScreenRecordFailed", exception.toString());
            stopRecordingInternal(true);
        }
    }

    private void stopRecordingInternal(boolean notifyUnity) {
        try {
            if (virtualDisplay != null) {
                virtualDisplay.release();
                virtualDisplay = null;
            }

            if (mediaRecorder != null) {
                try {
                    mediaRecorder.stop();
                } catch (RuntimeException ignored) {
                }
                mediaRecorder.reset();
                mediaRecorder.release();
                mediaRecorder = null;
            }

            if (mediaProjection != null) {
                mediaProjection.unregisterCallback(projectionCallback);
                mediaProjection.stop();
                mediaProjection = null;
            }
        } catch (Exception exception) {
            sendUnityMessage(unityCallbackObject, "OnScreenRecordFailed", exception.toString());
        } finally {
            if (handlerThread != null) {
                handlerThread.quitSafely();
                handlerThread = null;
                handler = null;
            }

            if (notifyUnity) {
                sendUnityMessage(unityCallbackObject, "OnScreenRecordStopped", outputPath == null ? "" : outputPath);
            }

            stopForeground(true);
            stopSelf();
        }
    }

    public static void sendUnityMessage(String gameObjectName, String methodName, String message) {
        if (gameObjectName == null || gameObjectName.length() == 0) {
            return;
        }

        UnityPlayer.UnitySendMessage(gameObjectName, methodName, message == null ? "" : message);
    }
}
