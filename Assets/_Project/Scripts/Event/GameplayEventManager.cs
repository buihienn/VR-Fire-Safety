using UnityEngine;

public class GameplayEventManager : MonoBehaviour
{
    public static GameplayEventManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool logEvents;

    private bool subscribedToLog;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        if (logEvents)
        {
            GameplayEventBus.OnEvent += LogEvent;
            subscribedToLog = true;
        }
    }

    private void OnDisable()
    {
        if (subscribedToLog)
        {
            GameplayEventBus.OnEvent -= LogEvent;
            subscribedToLog = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Raise(GameplayEventType type)
    {
        GameplayEventBus.Raise(type);
    }

    public void Raise(GameplayEventType type, string actorId, string targetId)
    {
        GameplayEventBus.Raise(type, actorId, targetId);
    }

    private void LogEvent(GameplayEvent gameplayEvent)
    {
        Debug.Log(
            $"[GameplayEvent] {gameplayEvent.Type} Actor={gameplayEvent.ActorId} Target={gameplayEvent.TargetId} Time={gameplayEvent.Time}",
            this);
    }
}
