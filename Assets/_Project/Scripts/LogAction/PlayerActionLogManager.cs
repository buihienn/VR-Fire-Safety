using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerActionResult
{
    Correct,
    Wrong,
    Neutral
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
}

[Serializable]
public class PlayerActionLogSession
{
    public string sessionId;
    public string videoPath;
    public string createdAt;
    public List<PlayerActionLogEntry> actions = new List<PlayerActionLogEntry>();
}

public class PlayerActionLogManager : MonoBehaviour
{
    private const string DebugPrefix = "Record review debug";

    public static PlayerActionLogManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = false;
    [SerializeField] private bool saveActiveSessionOnDestroy = true;
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool listenToGameplayEvents = true;

    public string CurrentJsonPath { get; private set; }
    public bool IsSessionActive { get; private set; }

    private PlayerActionLogSession currentSession;
    private float sessionStartTime;
    private bool subscribedToGameplayEvents;

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
    }

    private void OnEnable()
    {
        if (!listenToGameplayEvents || subscribedToGameplayEvents)
        {
            return;
        }

        GameplayEventBus.OnEvent += OnGameplayEvent;
        subscribedToGameplayEvents = true;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Subscribed to GameplayEventBus.");
        }
    }

    private void OnDisable()
    {
        if (!subscribedToGameplayEvents)
        {
            return;
        }

        GameplayEventBus.OnEvent -= OnGameplayEvent;
        subscribedToGameplayEvents = false;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Unsubscribed from GameplayEventBus.");
        }
    }

    public void BeginSession(string videoPath)
    {
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
        sessionStartTime = Time.realtimeSinceStartup;
        IsSessionActive = true;

        if (logToConsole)
        {
            Debug.Log($"[{DebugPrefix}] [PlayerActionLogManager] Player action log session started: {CurrentJsonPath}");
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
        LogAction(actionId, title, description, PlayerActionResult.Wrong);
    }

    public void LogNeutralAction(string actionId, string title, string description = "")
    {
        LogAction(actionId, title, description, PlayerActionResult.Neutral);
    }

    public void LogAction(string actionId, string title, string description, PlayerActionResult result)
    {
        if (!IsSessionActive || currentSession == null)
        {
            Debug.LogWarning($"[{DebugPrefix}] [PlayerActionLogManager] Cannot log player action because no action log session is active.");
            return;
        }

        AddActionEntry(null, actionId, title, description, result, null, null);
    }

    private void OnGameplayEvent(GameplayEvent gameplayEvent)
    {
        if (logToConsole)
        {
            Debug.Log(
                $"[{DebugPrefix}] [PlayerActionLogManager] Received event {gameplayEvent.Type} | Actor={gameplayEvent.ActorId} | Target={gameplayEvent.TargetId} | SessionActive={IsSessionActive}");
        }

        if (!IsSessionActive || currentSession == null)
        {
            if (logToConsole)
            {
                Debug.LogWarning($"[{DebugPrefix}] [PlayerActionLogManager] Event {gameplayEvent.Type} ignored because no action log session is active.");
            }

            return;
        }

        PlayerActionResult result = GetResultForGameplayEvent(gameplayEvent.Type);
        string actionId = gameplayEvent.Type.ToString();
        string title = GetTitleForGameplayEvent(gameplayEvent.Type);
        string description = CreateDescription(gameplayEvent);

        AddActionEntry(
            gameplayEvent.Type.ToString(),
            actionId,
            title,
            description,
            result,
            gameplayEvent.ActorId,
            gameplayEvent.TargetId);
    }

    private void AddActionEntry(
        string eventType,
        string actionId,
        string title,
        string description,
        PlayerActionResult result,
        string actorId,
        string targetId)
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
            sceneName = SceneManager.GetActiveScene().name
        };

        currentSession.actions.Add(entry);

        if (logToConsole)
        {
            Debug.Log(
                $"[{DebugPrefix}] [PlayerActionLogManager] Action logged #{currentSession.actions.Count} [{entry.result}] {entry.time:0.00}s - {entry.title} | EventType={entry.eventType} | Actor={entry.actorId} | Target={entry.targetId} | Scene={entry.sceneName}");
        }
    }

    private PlayerActionResult GetResultForGameplayEvent(GameplayEventType type)
    {
        switch (type)
        {
            case GameplayEventType.FireExtinguished:
            case GameplayEventType.GasLeakStopped:
            case GameplayEventType.ValveClosed:
                return PlayerActionResult.Correct;

            case GameplayEventType.PlayerEnteredDangerZone:
            case GameplayEventType.PlayerFainted:
            case GameplayEventType.WrongActionPerformed:
                return PlayerActionResult.Wrong;

            default:
                return PlayerActionResult.Neutral;
        }
    }

    private string GetTitleForGameplayEvent(GameplayEventType type)
    {
        switch (type)
        {
            case GameplayEventType.ValveClosed:
                return "Dong van gas";
            case GameplayEventType.ValveOpened:
                return "Mo van gas";
            case GameplayEventType.WindowOpened:
                return "Mo cua so";
            case GameplayEventType.WindowClosed:
                return "Dong cua so";
            case GameplayEventType.FireIgnited:
                return "Lua bat dau chay";
            case GameplayEventType.FireExtinguished:
                return "Dap tat dam chay";
            case GameplayEventType.GasLeakStarted:
                return "Ro ri gas bat dau";
            case GameplayEventType.GasLeakStopped:
                return "Da xu ly ro ri gas";
            case GameplayEventType.GasLevelChanged:
                return "Muc gas thay doi";
            case GameplayEventType.PlayerEnteredDangerZone:
                return "Nguoi choi vao vung nguy hiem";
            case GameplayEventType.PlayerExitedDangerZone:
                return "Nguoi choi roi vung nguy hiem";
            case GameplayEventType.PlayerFainted:
                return "Nguoi choi bi ngat";
            case GameplayEventType.MatchStarted:
                return "Bat dau man choi";
            case GameplayEventType.MatchEnded:
                return "Ket thuc man choi";
            case GameplayEventType.WrongActionPerformed:
                return "Thuc hien hanh dong sai";
            default:
                return type.ToString();
        }
    }

    private string CreateDescription(GameplayEvent gameplayEvent)
    {
        return $"Actor={gameplayEvent.ActorId}, Target={gameplayEvent.TargetId}";
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
