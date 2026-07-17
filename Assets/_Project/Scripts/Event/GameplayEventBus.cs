using System;
using UnityEngine;

public static class GameplayEventBus
{
    public static event Action<GameplayEvent> OnEvent;
    public static bool LogRaisedEvents { get; set; } = true;
    private const string DebugPrefix = "Record review debug";

    public static void Raise(GameplayEvent gameplayEvent)
    {
        if (LogRaisedEvents)
        {
            Debug.Log(
                $"[{DebugPrefix}] [GameplayEventBus] Raise {gameplayEvent.Type} | Actor={gameplayEvent.ActorId} | Target={gameplayEvent.TargetId} | Payload={FormatPayload(gameplayEvent.Payload)} | Subscribers={OnEvent?.GetInvocationList().Length ?? 0}");
        }

        OnEvent?.Invoke(gameplayEvent);
    }

    public static void Raise(
        GameplayEventType type,
        string actorId = null,
        string targetId = null,
        object payload = null)
    {
        Raise(new GameplayEvent(type, actorId, targetId, payload));
    }

    private static string FormatPayload(object payload)
    {
        return payload == null ? "null" : payload.ToString();
    }
}
