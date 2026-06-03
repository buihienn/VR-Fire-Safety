using System;
using UnityEngine;

[Serializable]
public struct GameplayEvent
{
    public GameplayEventType Type;
    public string ActorId;
    public string TargetId;
    public float Time;
    public object Payload;

    public GameplayEvent(
        GameplayEventType type,
        string actorId = null,
        string targetId = null,
        object payload = null,
        float time = -1f)
    {
        Type = type;
        ActorId = actorId;
        TargetId = targetId;
        Payload = payload;
        Time = time >= 0f ? time : UnityEngine.Time.time;
    }
}
