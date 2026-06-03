using UnityEngine;

public class GameplayEventTestInput : MonoBehaviour
{
    private void Update()
    {
        GameplayEventBus.Raise(
                GameplayEventType.ValveClosed,
                actorId: "TestPlayer",
                targetId: "TestValve"
            );
    }
}