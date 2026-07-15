using System;
using System.IO;
using UnityEngine;

public class ExportRecordedVideoButton : MonoBehaviour
{
    [SerializeField] private bool copyJsonMetadata;
    [SerializeField] private string exportFilePrefix = "VRFireSafety_Review";

    public void ExportLastRecording()
    {
        QuestScreenRecordingManager manager = QuestScreenRecordingManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("Cannot export video because QuestScreenRecordingManager was not found.");
            return;
        }

        if (!manager.HasRecordingReady)
        {
            Debug.LogWarning("Cannot export video because no completed recording is ready.");
            return;
        }

        ExportVideo(manager.LastRecordingPath);
    }

    public void ExportVideo(string sourceVideoPath)
    {
        if (string.IsNullOrEmpty(sourceVideoPath))
        {
            Debug.LogWarning("Cannot export video because source path is empty.");
            return;
        }

        if (!File.Exists(sourceVideoPath))
        {
            Debug.LogWarning("Cannot export video because source file does not exist: " + sourceVideoPath);
            return;
        }

        string exportFileName = CreateExportFileName(sourceVideoPath);

#if UNITY_ANDROID && !UNITY_EDITOR
        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaClass bridge = new AndroidJavaClass("com.vrfiresafety.screenrecorder.ScreenRecorderBridge");

        string exportedUri = bridge.CallStatic<string>(
            "exportVideoToMovies",
            activity,
            sourceVideoPath,
            exportFileName);

        if (string.IsNullOrEmpty(exportedUri))
        {
            Debug.LogWarning("Failed to export video: " + sourceVideoPath);
            return;
        }

        Debug.Log("Video exported to public Movies through MediaStore: " + exportedUri);
#else
        string exportedUri = ExportVideoInEditor(sourceVideoPath, exportFileName);
        Debug.Log("Video exported in Editor: " + exportedUri);
#endif

        if (copyJsonMetadata)
        {
            ExportJsonMetadata(sourceVideoPath, exportFileName);
        }
    }

    private string CreateExportFileName(string sourceVideoPath)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourceVideoPath);
        if (string.IsNullOrEmpty(sourceName))
        {
            sourceName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        return exportFilePrefix + "_" + sourceName + ".mp4";
    }

    private void ExportJsonMetadata(string sourceVideoPath, string exportVideoFileName)
    {
        string sourceJsonPath = Path.ChangeExtension(sourceVideoPath, ".json");
        if (!File.Exists(sourceJsonPath))
        {
            return;
        }

        string exportJsonFileName = Path.ChangeExtension(exportVideoFileName, ".json");

#if UNITY_ANDROID && !UNITY_EDITOR
        string destinationDirectory = GetAndroidPublicMoviesPath();
#else
        string destinationDirectory = Path.Combine(Application.persistentDataPath, "ExportedReplays");
#endif

        Directory.CreateDirectory(destinationDirectory);
        string destinationPath = Path.Combine(destinationDirectory, exportJsonFileName);
        try
        {
            File.Copy(sourceJsonPath, destinationPath, true);
            Debug.Log("Review metadata exported: " + destinationPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Video was exported, but JSON metadata export failed: " + exception.Message);
        }
    }

    private string GetAndroidPublicMoviesPath()
    {
        string persistentPath = Application.persistentDataPath.Replace("\\", "/");
        int androidDataIndex = persistentPath.IndexOf("/Android/data/", StringComparison.Ordinal);
        string storageRoot = androidDataIndex > 0
            ? persistentPath.Substring(0, androidDataIndex)
            : "/storage/emulated/0";

        if (string.IsNullOrEmpty(storageRoot))
        {
            storageRoot = "/storage/emulated/0";
        }

        return Path.Combine(storageRoot, "Movies", "VRFireSafety", "Replays");
    }

    private string ExportVideoInEditor(string sourceVideoPath, string exportFileName)
    {
        string exportDirectory = Path.Combine(Application.persistentDataPath, "ExportedReplays");
        Directory.CreateDirectory(exportDirectory);

        string destinationPath = Path.Combine(exportDirectory, exportFileName);
        File.Copy(sourceVideoPath, destinationPath, true);
        return destinationPath;
    }
}
