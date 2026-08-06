using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestScreenRecordingManager : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";
    private const string LatestRecordingFileName = "review_latest.mp4";

    public static QuestScreenRecordingManager Instance { get; private set; }
    public event Action<string> RecordingStarted;
    public event Action<string> RecordingReady;

    [Header("Recording")]
    [SerializeField] private bool startRecordingOnStart = true;
    [SerializeField] private bool startRecordingOnSceneLoaded;
    [SerializeField] private string gameplaySceneName = "MainScene";
    [SerializeField] private string endGameSceneName = "EndGameScene";
    [SerializeField] private bool autoAttachRecordingToVideoPlayerOnReady;
    [SerializeField] private int width = 1280;
    [SerializeField] private int height = 720;
    [SerializeField] private int fps = 30;
    [SerializeField] private int bitrate = 8000000;

    [Header("Debug")]
    [SerializeField] private bool stopAfterDebugDelay;
    [SerializeField] private float debugStopDelay = 5f;

    [Header("Editor Test")]
    [SerializeField] private string editorFallbackVideoPath;

    public string LastRecordingPath { get; private set; }
    public bool IsRecording { get; private set; }
    public bool HasRecordingReady => !IsRecording && !waitingForStopCallback && !string.IsNullOrEmpty(LastRecordingPath);
    public bool IsStopping => waitingForStopCallback;
    public string DebugState =>
        $"IsRecording={IsRecording}, WaitingForStopCallback={waitingForStopCallback}, PendingStartAfterStop={pendingStartAfterStop}, HasRecordingReady={HasRecordingReady}, LastRecordingPath={LastRecordingPath}, GameplaySceneName={gameplaySceneName}, EndGameSceneName={endGameSceneName}";

    private bool waitingForStopCallback;
    private float recordingElapsedTime;
    private bool hasDebugStopTriggered;
    private bool pendingStartAfterStop;

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
            Debug.Log($"[{DebugPrefix}] Debug stop recording after {debugStopDelay} seconds.");
            StopRecording();
        }
    }

    public void StartRecording()
    {
        if (waitingForStopCallback)
        {
            pendingStartAfterStop = true;
            Debug.Log($"[{DebugPrefix}] StartRecording queued until the previous recording stops. {DebugState}");
            return;
        }

        if (IsRecording)
        {
            Debug.Log($"[{DebugPrefix}] StartRecording ignored because recording is already active. {DebugState}");
            return;
        }

        recordingElapsedTime = 0f;
        hasDebugStopTriggered = false;

        pendingStartAfterStop = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        string fileName = LatestRecordingFileName;
        Debug.Log($"[{DebugPrefix}] StartRecording requested on Android. Output file name: {fileName}");

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

        DeletePreviousJsonLog(LastRecordingPath);

        IsRecording = true;
        Debug.Log($"[{DebugPrefix}] StartRecording permission flow launched. {DebugState}");
#else
        LastRecordingPath = ResolveEditorFallbackPath();
        IsRecording = true;
        NotifyRecordingStarted();
        Debug.Log($"[{DebugPrefix}] Editor fallback recording started: {LastRecordingPath}");
#endif
    }

    public void StopRecording()
    {
        Debug.Log($"[{DebugPrefix}] StopRecording requested. {DebugState}");

        if (!IsRecording)
        {
            Debug.Log($"[{DebugPrefix}] StopRecording ignored because manager is not recording. {DebugState}");
            TryAttachLastRecordingToVideoPlayer();
            return;
        }

        waitingForStopCallback = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log($"[{DebugPrefix}] Sending Android stopRecording command. {DebugState}");

        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaClass bridge = new AndroidJavaClass("com.vrfiresafety.screenrecorder.ScreenRecorderBridge");

        bridge.CallStatic("stopRecording", activity);
        Debug.Log($"[{DebugPrefix}] Android stopRecording command sent. Waiting for OnScreenRecordStopped callback.");
#else
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.Log($"[{DebugPrefix}] Editor fallback recording stopped: {LastRecordingPath}");
        NotifyRecordingReady();
        TryAttachLastRecordingToVideoPlayer();
#endif
    }

    public void OnScreenRecordStarted(string videoPath)
    {
        Debug.Log($"[{DebugPrefix}] OnScreenRecordStarted callback received: {videoPath}");
        LastRecordingPath = videoPath;
        IsRecording = true;
        recordingElapsedTime = 0f;
        hasDebugStopTriggered = false;
        NotifyRecordingStarted();
        Debug.Log($"[{DebugPrefix}] Screen recording started: {videoPath}");
    }

    public void OnScreenRecordStopped(string videoPath)
    {
        Debug.Log($"[{DebugPrefix}] OnScreenRecordStopped callback received: {videoPath}");

        if (!string.IsNullOrEmpty(videoPath))
        {
            LastRecordingPath = videoPath;
        }

        IsRecording = false;
        waitingForStopCallback = false;
        Debug.Log($"[{DebugPrefix}] Screen recording stopped: {LastRecordingPath}");
        NotifyRecordingReady();
        TryAttachLastRecordingToVideoPlayer();

        if (pendingStartAfterStop)
        {
            pendingStartAfterStop = false;
            StartRecording();
        }
    }

    public void OnScreenRecordPermissionDenied(string message)
    {
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.LogWarning($"[{DebugPrefix}] Screen recording permission denied. {DebugState}");
    }

    public void OnScreenRecordFailed(string error)
    {
        IsRecording = false;
        waitingForStopCallback = false;
        Debug.LogError($"[{DebugPrefix}] Screen recording failed: {error}. {DebugState}");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[{DebugPrefix}] Scene loaded: {scene.name}. Mode={mode}. {DebugState}");

        if (startRecordingOnSceneLoaded && scene.name == gameplaySceneName)
        {
            GameOverPayload.Clear();
            Debug.Log($"[{DebugPrefix}] Gameplay scene reached, starting recording.");
            StartRecording();
            return;
        }

        if (scene.name == endGameSceneName)
        {
            Debug.Log($"[{DebugPrefix}] End game scene reached, stopping recording.");
            StopRecording();
        }
    }

    private void TryAttachLastRecordingToVideoPlayer()
    {
        if (!autoAttachRecordingToVideoPlayerOnReady)
        {
            return;
        }

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

    public void StopRecordingAndPlayWhenReady(VideoPlayerController controller)
    {
        if (controller == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot play last recording because VideoPlayerController is missing.");
            return;
        }

        if (TryPlayLastRecording(controller))
        {
            return;
        }

        if (!IsRecording && !waitingForStopCallback)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot play last recording because recording is not ready. {DebugState}");
            return;
        }

        StartCoroutine(PlayWhenRecordingReady(controller));

        if (IsRecording && !waitingForStopCallback)
        {
            StopRecording();
        }
    }

    private IEnumerator PlayWhenRecordingReady(VideoPlayerController controller)
    {
        while (!HasRecordingReady)
        {
            yield return null;
        }

        if (controller != null)
        {
            controller.PlayVideoUrl(LastRecordingPath);
        }
    }

    private void NotifyRecordingReady()
    {
        if (!HasRecordingReady)
        {
            return;
        }

        RecordingReady?.Invoke(LastRecordingPath);
    }

    private void NotifyRecordingStarted()
    {
        if (string.IsNullOrEmpty(LastRecordingPath))
        {
            return;
        }

        RecordingStarted?.Invoke(LastRecordingPath);
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

    private static void DeletePreviousJsonLog(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            return;
        }

        string jsonPath = Path.ChangeExtension(videoPath, ".json");
        try
        {
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
                Debug.Log($"[{DebugPrefix}] Deleted previous JSON log before starting the latest attempt: {jsonPath}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{DebugPrefix}] Could not delete previous JSON log '{jsonPath}': {exception.Message}");
        }
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
