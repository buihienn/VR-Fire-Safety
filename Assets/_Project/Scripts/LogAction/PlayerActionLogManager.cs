using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public enum PlayerActionResult
{
    Correct = 0,
    Incorrect = 1
}

[Serializable]
public class PlayerActionLogEntry
{
    public float time;
    public string eventType;
    public string actionId;
    public string title;
    public string description;
    public PlayerActionResult result;
    public string actorId;
    public string targetId;
    public string sceneName;
    public int gasLevel;
    public int scoreDelta;
}

[Serializable]
public class PlayerActionLogSession
{
    public string sessionId;
    public string videoPath;
    public string createdAt;
    public int totalScore;
    public int correctActionCount;
    public int incorrectActionCount;
    public List<PlayerActionLogEntry> actions = new List<PlayerActionLogEntry>();
}

public class PlayerActionLogManager : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    public static PlayerActionLogManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = false;
    [SerializeField] private bool saveActiveSessionOnDestroy = true;
    [SerializeField] private bool logToConsole = true;
    [FormerlySerializedAs("listenToGameplayEvents")]
    [SerializeField] private bool listenToEvaluatedActions = true;

    public string CurrentJsonPath { get; private set; }
    public bool IsSessionActive { get; private set; }

    private PlayerActionLogSession currentSession;
    private float sessionStartTime;
    private bool subscribedToEvaluatedActions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        GameplayActionEvaluator evaluator = GetComponent<GameplayActionEvaluator>();
        if (evaluator == null)
            evaluator = gameObject.AddComponent<GameplayActionEvaluator>();

        evaluator.ResetEvaluationState();
    }

    private void OnEnable()
    {
        if (!listenToEvaluatedActions || subscribedToEvaluatedActions)
        {
            return;
        }

        GameplayActionEvaluationBus.OnActionEvaluated += OnActionEvaluated;
        subscribedToEvaluatedActions = true;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Subscribed to evaluated gameplay actions.");
        }
    }

    private void OnDisable()
    {
        if (!subscribedToEvaluatedActions)
        {
            return;
        }

        GameplayActionEvaluationBus.OnActionEvaluated -= OnActionEvaluated;
        subscribedToEvaluatedActions = false;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Unsubscribed from evaluated gameplay actions.");
        }
    }

    public void BeginSession(string videoPath)
    {
        BeginSession(videoPath, Time.realtimeSinceStartup);
    }

    public void BeginSession(string videoPath, float timelineStartRealtime)
    {
        GameplayActionEvaluator evaluator = GetComponent<GameplayActionEvaluator>();
        if (evaluator != null)
        {
            evaluator.ResetEvaluationState();
        }

        string sessionId = Path.GetFileNameWithoutExtension(videoPath);
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = "review_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        currentSession = new PlayerActionLogSession
        {
            sessionId = sessionId,
            videoPath = videoPath,
            createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        CurrentJsonPath = CreateJsonPath(videoPath, sessionId);
        sessionStartTime = timelineStartRealtime >= 0f
            ? timelineStartRealtime
            : Time.realtimeSinceStartup;
        IsSessionActive = true;

        if (logToConsole)
        {
            Debug.Log(
                $"[{DebugPrefix}] [PlayerActionLogManager] Player action log session started: " +
                $"{CurrentJsonPath} | TimelineStartRealtime={sessionStartTime:0.000}");
        }
    }

    public void BeginSession()
    {
        BeginSession(string.Empty);
    }

    public void AttachVideoPath(string videoPath)
    {
        if (currentSession == null || string.IsNullOrEmpty(videoPath))
        {
            return;
        }

        string sessionId = Path.GetFileNameWithoutExtension(videoPath);
        if (!string.IsNullOrEmpty(sessionId))
        {
            currentSession.sessionId = sessionId;
        }

        currentSession.videoPath = videoPath;
        CurrentJsonPath = CreateJsonPath(videoPath, currentSession.sessionId);

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Player action log attached to video: {CurrentJsonPath}");
        }
    }

    public void LogCorrectAction(string actionId, string title, string description = "")
    {
        LogAction(actionId, title, description, PlayerActionResult.Correct);
    }

    public void LogWrongAction(string actionId, string title, string description = "")
    {
        LogAction(actionId, title, description, PlayerActionResult.Incorrect);
    }

    public void LogAction(string actionId, string title, string description, PlayerActionResult result)
    {
        if (!IsSessionActive || currentSession == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] [PlayerActionLogManager] Cannot log player action because no action log session is active.");
            return;
        }

        AddActionEntry(
            null, actionId, title, description, result,
            null, null, gasLevel: 0, scoreDelta: 0);
    }

    private void OnActionEvaluated(EvaluatedGameplayAction action)
    {
        if (logToConsole)
        {
            Debug.Log(
                $"[{DebugPrefix}] [PlayerActionLogManager] Received evaluated action {action.actionId} " +
                $"| Result={action.result} | Actor={action.actorId} | SessionActive={IsSessionActive}");
        }

        if (!IsSessionActive || currentSession == null)
        {
            if (logToConsole)
            {
                Debug.LogWarning(
                    $"[{DebugPrefix}] [PlayerActionLogManager] Evaluated action {action.actionId} " +
                    "ignored because no action log session is active.");
            }

            return;
        }

        AddActionEntry(
            action.sourceEventType,
            action.actionId,
            action.title,
            action.feedback,
            action.result,
            action.actorId,
            action.targetId,
            action.gasLevel,
            action.scoreDelta);
    }

    private void AddActionEntry(
        string eventType,
        string actionId,
        string title,
        string description,
        PlayerActionResult result,
        string actorId,
        string targetId,
        int gasLevel,
        int scoreDelta)
    {
        PlayerActionLogEntry entry = new PlayerActionLogEntry
        {
            time = Time.realtimeSinceStartup - sessionStartTime,
            eventType = eventType,
            actionId = actionId,
            title = title,
            description = description,
            result = result,
            actorId = actorId,
            targetId = targetId,
            sceneName = SceneManager.GetActiveScene().name,
            gasLevel = gasLevel,
            scoreDelta = scoreDelta
        };

        currentSession.actions.Add(entry);

        if (result == PlayerActionResult.Correct)
            currentSession.correctActionCount++;
        else
            currentSession.incorrectActionCount++;

        if (logToConsole)
        {
            Debug.Log(
                $"[{DebugPrefix}] [PlayerActionLogManager] Action logged #{currentSession.actions.Count} [{entry.result}] {entry.time:0.00}s - {entry.title} | EventType={entry.eventType} | Actor={entry.actorId} | Target={entry.targetId} | Scene={entry.sceneName}");
        }
    }

    public void SaveSession()
    {
        if (!IsSessionActive || currentSession == null || string.IsNullOrEmpty(CurrentJsonPath))
        {
            return;
        }

        string directoryPath = Path.GetDirectoryName(CurrentJsonPath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        currentSession.totalScore = ScoreManager.LastScore;

        string json = JsonUtility.ToJson(currentSession, true);
        File.WriteAllText(CurrentJsonPath, json);
        IsSessionActive = false;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Player action log saved: {CurrentJsonPath} | ActionCount={currentSession.actions.Count}");
        }
    }

    private string CreateJsonPath(string videoPath, string sessionId)
    {
        if (!string.IsNullOrEmpty(videoPath))
        {
            string videoDirectory = Path.GetDirectoryName(videoPath);
            if (!string.IsNullOrEmpty(videoDirectory))
            {
                return Path.Combine(videoDirectory, sessionId + ".json");
            }
        }

        return Path.Combine(Application.persistentDataPath, "Movies", "Replays", sessionId + ".json");
    }

    private void OnDestroy()
    {
        if (saveActiveSessionOnDestroy)
        {
            SaveSession();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
