package com.vrfiresafety.screenrecorder;

import android.app.Activity;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Bundle;
import com.unity3d.player.UnityPlayerGameActivity;

public class ProjectionPermissionActivity extends Activity {
    private static final int REQUEST_MEDIA_PROJECTION = 2701;

    private String outputPath;
    private int width;
    private int height;
    private int fps;
    private int bitrate;
    private String unityCallbackObject;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Intent launchIntent = getIntent();
        outputPath = launchIntent.getStringExtra(ScreenRecordService.EXTRA_OUTPUT_PATH);
        width = launchIntent.getIntExtra(ScreenRecordService.EXTRA_WIDTH, 1280);
        height = launchIntent.getIntExtra(ScreenRecordService.EXTRA_HEIGHT, 720);
        fps = launchIntent.getIntExtra(ScreenRecordService.EXTRA_FPS, 30);
        bitrate = launchIntent.getIntExtra(ScreenRecordService.EXTRA_BITRATE, 8000000);
        unityCallbackObject = launchIntent.getStringExtra(ScreenRecordService.EXTRA_UNITY_CALLBACK_OBJECT);

        MediaProjectionManager manager = (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        Intent permissionIntent = manager.createScreenCaptureIntent();
        startActivityForResult(permissionIntent, REQUEST_MEDIA_PROJECTION);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode == REQUEST_MEDIA_PROJECTION && resultCode == RESULT_OK && data != null) {
            Intent serviceIntent = new Intent(this, ScreenRecordService.class);
            serviceIntent.setAction(ScreenRecordService.ACTION_START);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_RESULT_CODE, resultCode);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_RESULT_DATA, data);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_OUTPUT_PATH, outputPath);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_WIDTH, width);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_HEIGHT, height);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_FPS, fps);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_BITRATE, bitrate);
            serviceIntent.putExtra(ScreenRecordService.EXTRA_UNITY_CALLBACK_OBJECT, unityCallbackObject);

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(serviceIntent);
            } else {
                startService(serviceIntent);
            }
        } else {
            ScreenRecordService.sendUnityMessage(unityCallbackObject, "OnScreenRecordPermissionDenied", "");
        }

        returnToUnityActivity();
    }

    private void returnToUnityActivity() {
        Intent unityIntent = new Intent(this, UnityPlayerGameActivity.class);
        unityIntent.addFlags(
                Intent.FLAG_ACTIVITY_REORDER_TO_FRONT |
                Intent.FLAG_ACTIVITY_SINGLE_TOP);

        startActivity(unityIntent);
        finish();
        overridePendingTransition(0, 0);
    }
}
