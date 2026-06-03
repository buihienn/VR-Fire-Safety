using System;

public static class GameplayEventBus
{
    public static event Action<GameplayEvent> OnEvent;

    public static void Raise(GameplayEvent gameplayEvent)
    {
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
}
