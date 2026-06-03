using UnityEngine;

public class FlameSfxController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private string fireLoopSoundName = "FlameLoop";

    [Header("Check")]
    [SerializeField] private float visibleBurnThreshold = 0.02f;
    [SerializeField] private bool checkOnUpdate = true;

    [Header("Events")]
    [SerializeField] private bool raiseFireExtinguishedEvent = true;
    [SerializeField] private string actorId = "Player";

    private bool isFireLoopPlaying;
    private bool wasAnyActiveFire;

    private void Start()
    {
        SyncFireLoopSfx();
    }

    private void Update()
    {
        if (!checkOnUpdate) return;
        SyncFireLoopSfx();
    }

    public void SyncFireLoopSfx()
    {
        bool hasActiveFire = HasAnyActiveFire();

        if (raiseFireExtinguishedEvent && wasAnyActiveFire && !hasActiveFire)
        {
            GameplayEventBus.Raise(
                GameplayEventType.FireExtinguished,
                actorId: actorId,
                targetId: gameObject.name);
        }

        if (hasActiveFire)
        {
            if (!isFireLoopPlaying)
            {
                isFireLoopPlaying = true;

                if (AudioManager.Instance != null &&
                    !AudioManager.Instance.IsPlaying(fireLoopSoundName))
                {
                    AudioManager.Instance.Play(fireLoopSoundName);
                }
            }
        }
        else
        {
            if (isFireLoopPlaying)
            {
                isFireLoopPlaying = false;

                if (AudioManager.Instance != null &&
                    AudioManager.Instance.IsPlaying(fireLoopSoundName))
                {
                    AudioManager.Instance.Stop(fireLoopSoundName);
                }
            }
        }

        wasAnyActiveFire = hasActiveFire;
    }

    private bool HasAnyActiveFire()
    {
        foreach (FlameNode node in FlameNode.All)
        {
            if (node == null) continue;

            // Đang cháy
            if (node.IsBurning)
                return true;

            // vẫn còn visual lửa chưa tắt hẳn
            if (node.Burn01 > visibleBurnThreshold)
                return true;
        }

        return false;
    }
}