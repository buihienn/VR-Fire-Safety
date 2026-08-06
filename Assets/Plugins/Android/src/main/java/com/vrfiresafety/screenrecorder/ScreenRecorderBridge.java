package com.vrfiresafety.screenrecorder;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.provider.MediaStore;
import android.os.Environment;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

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
        if (outputFile.exists()) {
            outputFile.delete();
        }

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

    public static String exportVideoToMovies(Context context, String sourcePath, String exportFileName) {
        if (sourcePath == null || sourcePath.trim().isEmpty()) {
            return "";
        }

        File sourceFile = new File(sourcePath);
        if (!sourceFile.exists()) {
            return "";
        }

        String safeFileName = exportFileName;
        if (safeFileName == null || safeFileName.trim().isEmpty()) {
            safeFileName = sourceFile.getName();
        }
        if (!safeFileName.endsWith(".mp4")) {
            safeFileName += ".mp4";
        }

        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
            return exportVideoWithMediaStore(context, sourceFile, safeFileName);
        }

        return exportVideoWithPublicFile(sourceFile, safeFileName);
    }

    public static String exportJsonToMovies(Context context, String sourcePath, String exportFileName) {
        if (sourcePath == null || sourcePath.trim().isEmpty()) {
            return "";
        }

        File sourceFile = new File(sourcePath);
        if (!sourceFile.exists()) {
            return "";
        }

        String safeFileName = exportFileName;
        if (safeFileName == null || safeFileName.trim().isEmpty()) {
            safeFileName = sourceFile.getName();
        }
        if (!safeFileName.endsWith(".json")) {
            safeFileName += ".json";
        }

        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
            return exportJsonWithMediaStore(context, sourceFile, safeFileName);
        }

        return exportJsonWithPublicFile(sourceFile, safeFileName);
    }

    private static String exportVideoWithMediaStore(Context context, File sourceFile, String fileName) {
        ContentResolver resolver = context.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.Video.Media.DISPLAY_NAME, fileName);
        values.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");
        values.put(MediaStore.Video.Media.RELATIVE_PATH, Environment.DIRECTORY_MOVIES + "/VRFireSafety/Replays");
        values.put(MediaStore.Video.Media.IS_PENDING, 1);

        Uri uri = resolver.insert(MediaStore.Video.Media.EXTERNAL_CONTENT_URI, values);
        if (uri == null) {
            return "";
        }

        try (InputStream input = new FileInputStream(sourceFile);
             OutputStream output = resolver.openOutputStream(uri)) {
            if (output == null) {
                return "";
            }

            copyStream(input, output);

            values.clear();
            values.put(MediaStore.Video.Media.IS_PENDING, 0);
            resolver.update(uri, values, null, null);
            return uri.toString();
        } catch (Exception exception) {
            resolver.delete(uri, null, null);
            return "";
        }
    }

    private static String exportVideoWithPublicFile(File sourceFile, String fileName) {
        File moviesDirectory = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES);
        File exportDirectory = new File(moviesDirectory, "VRFireSafety/Replays");
        if (!exportDirectory.exists()) {
            exportDirectory.mkdirs();
        }

        File exportFile = new File(exportDirectory, fileName);
        try (InputStream input = new FileInputStream(sourceFile);
             OutputStream output = new FileOutputStream(exportFile)) {
            copyStream(input, output);
            return exportFile.getAbsolutePath();
        } catch (Exception exception) {
            return "";
        }
    }

    private static String exportJsonWithMediaStore(Context context, File sourceFile, String fileName) {
        ContentResolver resolver = context.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.MediaColumns.DISPLAY_NAME, fileName);
        values.put(MediaStore.MediaColumns.MIME_TYPE, "application/json");
        values.put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_MOVIES + "/VRFireSafety/Replays");
        values.put(MediaStore.MediaColumns.IS_PENDING, 1);

        Uri collection = MediaStore.Files.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
        Uri uri = resolver.insert(collection, values);
        if (uri == null) {
            return "";
        }

        try (InputStream input = new FileInputStream(sourceFile);
             OutputStream output = resolver.openOutputStream(uri)) {
            if (output == null) {
                resolver.delete(uri, null, null);
                return "";
            }

            copyStream(input, output);

            values.clear();
            values.put(MediaStore.MediaColumns.IS_PENDING, 0);
            resolver.update(uri, values, null, null);
            return uri.toString();
        } catch (Exception exception) {
            resolver.delete(uri, null, null);
            return "";
        }
    }

    private static String exportJsonWithPublicFile(File sourceFile, String fileName) {
        File moviesDirectory = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES);
        File exportDirectory = new File(moviesDirectory, "VRFireSafety/Replays");
        if (!exportDirectory.exists()) {
            exportDirectory.mkdirs();
        }

        File exportFile = new File(exportDirectory, fileName);
        try (InputStream input = new FileInputStream(sourceFile);
             OutputStream output = new FileOutputStream(exportFile)) {
            copyStream(input, output);
            return exportFile.getAbsolutePath();
        } catch (Exception exception) {
            return "";
        }
    }

    private static void copyStream(InputStream input, OutputStream output) throws java.io.IOException {
        byte[] buffer = new byte[8192];
        int read;
        while ((read = input.read(buffer)) != -1) {
            output.write(buffer, 0, read);
        }
    }
}
