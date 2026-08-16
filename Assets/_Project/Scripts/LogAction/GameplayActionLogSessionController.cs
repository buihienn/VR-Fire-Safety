using UnityEngine;

public class GameplayActionLogSessionController : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    [Header("Session")]
    [SerializeField] private bool beginSessionOnStart;
    [SerializeField] private bool saveSessionOnDestroy = true;
    [SerializeField] private bool attachCurrentRecordingVideo = true;
    [SerializeField] private bool logToConsole = true;

    private bool sessionStarted;
    private bool sessionSaved;
    private PlayerActionLogManager actionLogManager;

    private void Awake()
    {
        actionLogManager = GetComponent<PlayerActionLogManager>();

        if (actionLogManager == null)
        {
            Debug.LogError($"[{DebugPrefix}] GameplayActionLogSessionController requires PlayerActionLogManager on the same GameObject.");
        }
    }

    private void OnEnable()
    {
        if (attachCurrentRecordingVideo && QuestScreenRecordingManager.Instance != null)
        {
            QuestScreenRecordingManager.Instance.RecordingStarted += OnRecordingStarted;
        }
    }

    private void Start()
    {
        if (beginSessionOnStart)
        {
            BeginGameplayLogSession();
        }
    }

    public void BeginGameplayLogSession()
    {
        if (sessionStarted)
        {
            return;
        }

        if (actionLogManager == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] Cannot begin gameplay action log session because PlayerActionLogManager is missing.");
            return;
        }

        QuestScreenRecordingManager recordingManager = QuestScreenRecordingManager.Instance;
        if (attachCurrentRecordingVideo && recordingManager != null && !recordingManager.IsCaptureActive)
        {
            Log("Waiting for the native recording-start callback before beginning the action log session.");
            return;
        }

        string videoPath = GetCurrentRecordingPath();
        if (attachCurrentRecordingVideo && recordingManager != null && !string.IsNullOrEmpty(videoPath))
        {
            actionLogManager.BeginSession(
                videoPath,
                recordingManager.RecordingStartedRealtime);
            ResetScoreForSynchronizedSession();
            Log("Gameplay action log session started with recording path: " + videoPath);
        }
        else
        {
            actionLogManager.BeginSession();
            ResetScoreForSynchronizedSession();
            Log("Gameplay action log session started without recording path.");
        }

        sessionStarted = true;
        sessionSaved = false;
    }

    public void SaveGameplayLogSession()
    {
        if (sessionSaved || actionLogManager == null)
        {
            return;
        }

        actionLogManager.SaveSession();
        sessionSaved = true;
        Log("Gameplay action log session save requested.");
    }

    private void OnRecordingStarted(string videoPath)
    {
        if (!attachCurrentRecordingVideo || actionLogManager == null || string.IsNullOrEmpty(videoPath))
        {
            return;
        }

        if (!sessionStarted)
        {
            QuestScreenRecordingManager recordingManager = QuestScreenRecordingManager.Instance;
            float recordingStartRealtime = recordingManager != null
                ? recordingManager.RecordingStartedRealtime
                : Time.realtimeSinceStartup;

            actionLogManager.BeginSession(videoPath, recordingStartRealtime);
            ResetScoreForSynchronizedSession();
            sessionStarted = true;
            sessionSaved = false;
            Log("Recording started before log session, beginning session with path: " + videoPath);
            return;
        }

        actionLogManager.AttachVideoPath(videoPath);
        Log("Recording path attached to active action log session: " + videoPath);
    }

    private string GetCurrentRecordingPath()
    {
        if (QuestScreenRecordingManager.Instance == null)
        {
            return string.Empty;
        }

        return QuestScreenRecordingManager.Instance.LastRecordingPath;
    }

    private static void ResetScoreForSynchronizedSession()
    {
        ScoreManager scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager != null)
        {
            scoreManager.ResetScore();
        }
    }

    private void OnDisable()
    {
        if (QuestScreenRecordingManager.Instance != null)
        {
            QuestScreenRecordingManager.Instance.RecordingStarted -= OnRecordingStarted;
        }
    }

    private void OnDestroy()
    {
        if (saveSessionOnDestroy)
        {
            SaveGameplayLogSession();
        }
    }

    private void Log(string message)
    {
        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [GameplayActionLogSessionController] {message}");
        }
    }
}
