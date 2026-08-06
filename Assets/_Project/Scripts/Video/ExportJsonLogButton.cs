using System;
using System.IO;
using UnityEngine;

public class ExportJsonLogButton : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    [SerializeField] private string exportFilePrefix = "VRFireSafety_Review";

    public void ExportLastJsonLog()
    {
        QuestScreenRecordingManager manager = QuestScreenRecordingManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot export JSON log because QuestScreenRecordingManager was not found.");
            return;
        }

        ExportJsonNextToVideo(manager.LastRecordingPath);
    }

    public void ExportJsonNextToVideo(string sourceVideoPath)
    {
        if (string.IsNullOrEmpty(sourceVideoPath))
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot export JSON log because the recording path is empty.");
            return;
        }

        string sourceJsonPath = Path.ChangeExtension(sourceVideoPath, ".json");
        if (!File.Exists(sourceJsonPath))
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot export JSON log because the source file does not exist: {sourceJsonPath}");
            return;
        }

        string exportFileName = CreateExportFileName(sourceJsonPath);

#if UNITY_ANDROID && !UNITY_EDITOR
        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaClass bridge = new AndroidJavaClass("com.vrfiresafety.screenrecorder.ScreenRecorderBridge");

        string exportedUri = bridge.CallStatic<string>(
            "exportJsonToMovies",
            activity,
            sourceJsonPath,
            exportFileName);

        if (string.IsNullOrEmpty(exportedUri))
        {
            Debug.LogWarning($"[{DebugPrefix}] Failed to export JSON log: {sourceJsonPath}");
            return;
        }

        Debug.Log($"[{DebugPrefix}] JSON log exported beside the video through MediaStore: {exportedUri}");
#else
        try
        {
            string exportDirectory = Path.Combine(Application.persistentDataPath, "ExportedReplays");
            Directory.CreateDirectory(exportDirectory);

            string destinationPath = Path.Combine(exportDirectory, exportFileName);
            File.Copy(sourceJsonPath, destinationPath, true);
            Debug.Log($"[{DebugPrefix}] JSON log exported in Editor: {destinationPath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{DebugPrefix}] Failed to export JSON log: {exception.Message}");
        }
#endif
    }

    private string CreateExportFileName(string sourceJsonPath)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourceJsonPath);
        if (string.IsNullOrEmpty(sourceName))
        {
            sourceName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        return exportFilePrefix + "_" + sourceName + ".json";
    }
}
