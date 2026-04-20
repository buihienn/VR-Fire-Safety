using UnityEngine;

public class SparkIgnitionTrigger : MonoBehaviour
{
    [Header("Spark FX")]
    [SerializeField] private ParticleSystem sparksFx;
    [SerializeField] private AudioSource sparkAudio;

    [Header("Ignition Target")]
    [SerializeField] private FlameNode ignitionNode;
    [SerializeField] private bool autoFindNearestIfMissing = true;
    [SerializeField] private float autoFindRadius = 1.0f;

    [Header("Gas Rule")]
    [Range(0, 3)]
    [SerializeField] private int requiredGasLevelForIgnition = 2;

    [Header("Options")]
    [SerializeField] private bool playSparkEvenIfCannotIgnite = true;
    [SerializeField] private bool debugLog = false;

    public void OnButtonPressed()
    {
        if (playSparkEvenIfCannotIgnite)
            PlaySparkFx();

        TryIgniteFromSpark();
    }

    private void PlaySparkFx()
    {
        if (sparksFx != null)
        {
            sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparksFx.Play(true);
        }

        if (sparkAudio != null)
            sparkAudio.Play();
    }

    private void TryIgniteFromSpark()
    {
        if (GasSystem.Instance == null)
        {
            if (debugLog)
                Debug.LogWarning("[SparkIgnitionTrigger] GasSystem.Instance is null.");
            return;
        }

        bool canIgnite = GasSystem.Instance.CanIgniteBySpark(requiredGasLevelForIgnition);

        if (!canIgnite)
        {
            if (debugLog)
            {
                Debug.Log(
                    $"[SparkIgnitionTrigger] Spark played but GasLevel={GasSystem.Instance.GasLevel()} " +
                    $"is below required level {requiredGasLevelForIgnition}."
                );
            }
            return;
        }

        FlameNode target = ignitionNode;

        if (target == null && autoFindNearestIfMissing)
            target = FindNearestFlameNode();

        if (target == null)
        {
            if (debugLog)
                Debug.LogWarning("[SparkIgnitionTrigger] No FlameNode found to ignite.");
            return;
        }

        target.ForceIgnite();

        if (debugLog)
            Debug.Log($"[SparkIgnitionTrigger] Ignited: {target.name}");
    }

    private FlameNode FindNearestFlameNode()
    {
        float radiusSqr = autoFindRadius * autoFindRadius;
        Vector3 myPos = transform.position;

        FlameNode best = null;
        float bestSqr = float.MaxValue;

        foreach (FlameNode node in FlameNode.All)
        {
            if (node == null) continue;

            Vector3 delta = node.transform.position - myPos;
            float sqr = delta.sqrMagnitude;

            if (sqr > radiusSqr) continue;
            if (sqr >= bestSqr) continue;

            best = node;
            bestSqr = sqr;
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, autoFindRadius);
    }
}