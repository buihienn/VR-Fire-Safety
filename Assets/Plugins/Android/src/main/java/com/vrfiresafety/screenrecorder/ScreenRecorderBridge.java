package com.vrfiresafety.screenrecorder;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Environment;
import java.io.File;

public final class ScreenRecorderBridge {
    private ScreenRecorderBridge() {
    }

    public static String startRecording(
            Activity activity,
            String fileName,
            int width,
            int height,
            int fps,
            int bitrate,
            String unityCallbackObject) {
        File replayDir = new File(activity.getExternalFilesDir(Environment.DIRECTORY_MOVIES), "Replays");
        if (!replayDir.exists()) {
            replayDir.mkdirs();
        }

        String safeFileName = fileName;
        if (safeFileName == null || safeFileName.trim().isEmpty()) {
            safeFileName = "review_recording.mp4";
        }
        if (!safeFileName.endsWith(".mp4")) {
            safeFileName += ".mp4";
        }

        File outputFile = new File(replayDir, safeFileName);

        Intent intent = new Intent(activity, ProjectionPermissionActivity.class);
        intent.putExtra(ScreenRecordService.EXTRA_OUTPUT_PATH, outputFile.getAbsolutePath());
        intent.putExtra(ScreenRecordService.EXTRA_WIDTH, width);
        intent.putExtra(ScreenRecordService.EXTRA_HEIGHT, height);
        intent.putExtra(ScreenRecordService.EXTRA_FPS, fps);
        intent.putExtra(ScreenRecordService.EXTRA_BITRATE, bitrate);
        intent.putExtra(ScreenRecordService.EXTRA_UNITY_CALLBACK_OBJECT, unityCallbackObject);
        activity.startActivity(intent);

        return outputFile.getAbsolutePath();
    }

    public static void stopRecording(Context context) {
        Intent intent = new Intent(context, ScreenRecordService.class);
        intent.setAction(ScreenRecordService.ACTION_STOP);
        context.startService(intent);
    }
}
