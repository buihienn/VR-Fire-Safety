using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestScreenRecordingManager : MonoBehaviour
{
    public static QuestScreenRecordingManager Instance { get; private set; }

    [Header("Recording")]
    [SerializeField] private bool startRecordingOnStart = true;
    [SerializeField] private string endGameSceneName = "EndGameScene";
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] private int bitrate = 8000000;

    [Header("Debug")]
    [SerializeField] private bool stopAfterDebugDelay = true;
    [SerializeField] private float debugStopDelay = 5f;

    [Header("Editor Test")]
    [SerializeField] private string editorFallbackVideoPath;

    public string LastRecordingPath { get; private set; }
    public bool IsRecording { get; private set; }
    public bool HasRecordingReady => !IsRecording && !waitingForStopCallback && !string.IsNullOrEmpty(LastRecordingPath);

    private bool waitingForStopCallback;
    private float recordingElapsedTime;
    private bool hasDebugStopTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (startRecordingOnStart)
        {
            StartRecording();
        }
    }

    private void Update()
    {
        if (!stopAfterDebugDelay || !IsRecording || waitingForStopCallback || hasDebugStopTriggered)
        {
            return;
        }

        recordingElapsedTime += Time.deltaTime;

        if (recordingElapsedTime >= debugStopDelay)
        {
            hasDebugStopTriggered = true;
            Debug.Log("Debug stop recording after " + debugStopDelay + " seconds.");
            StopRecording();
        }
    }

    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        recordingElapsedTime = 0f;
        hasDebugStopTriggered = false;

        string fileName = "review_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";

#if UNITY_ANDROID && !UNITY_EDITOR
        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaClass bridge = new AndroidJavaClass("com.vrfiresafety.screenrecorder.ScreenRecorderBridge");

        LastRecordingPath = bridge.CallStatic<string>(
            "startRecording",
            activity,
            fileName,
            width,
            height,
            fps,
            bitrate,
            gameObject.name);

        IsRecording = true;
#else
        LastRecordingPath = ResolveEditorFallbackPath();
        IsRecording = true;
        PlayerActionLogManager.Instance?.BeginSession(LastRecordingPath);
        Debug.Log("Editor fallback recording started: " + LastRecordingPath);
#endif
    }

    public void StopRecording()
    {
        if (!IsRecording)
        {
            TryAttachLastRecordingToVideoPlayer();
            return;
        }

        waitingForStopCallback = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaClass bridge = new AndroidJavaClass("com.vrfiresafety.screenrecorder.ScreenRecorderBridge");

        bridge.CallStatic("stopRecording", activity);
#else
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.Log("Editor fallback recording stopped: " + LastRecordingPath);
        PlayerActionLogManager.Instance?.SaveSession();
        TryAttachLastRecordingToVideoPlayer();
#endif
    }

    public void OnScreenRecordStarted(string videoPath)
    {
        LastRecordingPath = videoPath;
        IsRecording = true;
        recordingElapsedTime = 0f;
        hasDebugStopTriggered = false;
        PlayerActionLogManager.Instance?.BeginSession(videoPath);
        Debug.Log("Screen recording started: " + videoPath);
    }

    public void OnScreenRecordStopped(string videoPath)
    {
        if (!string.IsNullOrEmpty(videoPath))
        {
            LastRecordingPath = videoPath;
        }

        IsRecording = false;
        waitingForStopCallback = false;
        Debug.Log("Screen recording stopped: " + LastRecordingPath);
        PlayerActionLogManager.Instance?.SaveSession();
        TryAttachLastRecordingToVideoPlayer();
    }

    public void OnScreenRecordPermissionDenied(string message)
    {
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.LogWarning("Screen recording permission denied.");
    }

    public void OnScreenRecordFailed(string error)
    {
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.LogError("Screen recording failed: " + error);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == endGameSceneName)
        {
            StopRecording();
        }
    }

    private void TryAttachLastRecordingToVideoPlayer()
    {
        if (waitingForStopCallback || string.IsNullOrEmpty(LastRecordingPath))
        {
            return;
        }

        VideoPlayerController controller = FindObjectOfType<VideoPlayerController>();
        if (controller == null)
        {
            return;
        }

        controller.PlayVideoUrl(LastRecordingPath);
    }

    public bool TryPlayLastRecording(VideoPlayerController controller)
    {
        if (controller == null || !HasRecordingReady)
        {
            return false;
        }

        controller.PlayVideoUrl(LastRecordingPath);
        return true;
    }

    private string ResolveEditorFallbackPath()
    {
        if (string.IsNullOrEmpty(editorFallbackVideoPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(editorFallbackVideoPath))
        {
            return editorFallbackVideoPath;
        }

        return Path.Combine(Application.dataPath, editorFallbackVideoPath);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
