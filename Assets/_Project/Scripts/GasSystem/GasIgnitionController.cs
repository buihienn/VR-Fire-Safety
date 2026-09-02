using Fusion;
using UnityEngine;

public class GasIgnitionController : NetworkBehaviour
{
    private enum IgnitionOutcome
    {
        None = 0,
        Fire = 1,
        ExplosionAndFire = 2
    }

    [Header("References")]
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private GameObject levelOneFlarePrefab;
    [SerializeField] private GasExplosionEffect explosionPrefab;
    [SerializeField] private Transform explosionPoint;

    [Header("Gas Rules")]
    [Range(0, 3)]
    [SerializeField] private int flareGasLevel = 2;

    [Range(0, 3)]
    [SerializeField] private int explosionGasLevel = 3;

    [Header("Effect Lifetime")]
    [SerializeField, Min(0.1f)] private float flareLifetime = 3f;
    [SerializeField, Min(0.1f)] private float explosionLifetime = 6f;

    [Header("Game Flow")]
    [SerializeField] private bool endMatchOnExplosion = true;

    [Header("Voice Over")]
    [SerializeField] private string fireIgnitedVoKey = "VO_FireIgnited";

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Networked] private NetworkBool FlareTriggeredNet { get; set; }
    [Networked] private NetworkBool ExplosionTriggeredNet { get; set; }

    private bool fusionSpawned;
    private bool flareTriggeredLocal;
    private bool explosionTriggeredLocal;
    private bool fireIgnitedVoPlayedLocal;

    public bool HasIgnitionAuthority =>
        !fusionSpawned || (Object != null && Object.HasStateAuthority);

    private void Awake()
    {
        ResolveGasSystem();
    }

    public override void Spawned()
    {
        fusionSpawned = true;
        ResolveGasSystem();
    }

    public bool TryIgnite(
        Vector3 ignitionPosition,
        string sourceId,
        bool requireExactFlameTarget = false)
    {
        ResolveGasSystem();

        if (!HasIgnitionAuthority || gasSystem == null)
            return false;

        int gasLevel = gasSystem.GasLevel();
        IgnitionOutcome outcome = EvaluateOutcome(gasLevel);

        if (outcome == IgnitionOutcome.None)
            return false;

        if (outcome == IgnitionOutcome.ExplosionAndFire)
        {
            // The first successful gas ignition locks the outcome. If Level 2
            // already produced a fire, rising to Level 3 must not explode later.
            if (HasExplosionTriggered() || HasFlareTriggered())
                return false;

            SetExplosionTriggered();
        }
        else
        {
            if (HasFlareTriggered())
                return false;

            SetFlareTriggered();
        }

        // Dedicated appliance nodes are a Level 2 fire origin. At Level 3 the
        // gas explosion handles the outcome without lighting that node first.
        FlameNode nearestNode = null;
        if (outcome == IgnitionOutcome.Fire || !requireExactFlameTarget)
        {
            nearestNode = IgniteNearestFlameNode(
                ignitionPosition,
                requireExactFlameTarget);
        }
        Vector3 firePosition = nearestNode != null
            ? nearestNode.transform.position
            : ignitionPosition;
        Vector3 explosionPosition = explosionPoint != null
            ? explosionPoint.position
            : ignitionPosition;

        PlayOutcomeForEveryone(outcome, firePosition, explosionPosition);
        RaiseOutcomeEvent(outcome, gasLevel, sourceId);

        if (outcome == IgnitionOutcome.ExplosionAndFire &&
            endMatchOnExplosion &&
            GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ReportGasExplosion();
        }

        if (debugLog)
        {
            Debug.Log(
                $"[GasIgnitionController] {outcome} triggered by {sourceId} " +
                $"at gas level {gasLevel}.",
                this);
        }

        return true;
    }

    public bool RequestIgnite(
        Vector3 ignitionPosition,
        string sourceId,
        bool requireExactFlameTarget = false)
    {
        ResolveGasSystem();

        if (!fusionSpawned || Object == null || Object.HasStateAuthority)
            return TryIgnite(
                ignitionPosition,
                sourceId,
                requireExactFlameTarget);

        RPC_RequestIgnite(ignitionPosition, requireExactFlameTarget);
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void RPC_RequestIgnite(
        Vector3 ignitionPosition,
        bool requireExactFlameTarget,
        RpcInfo info = default)
    {
        TryIgnite(
            ignitionPosition,
            $"Player_{info.Source.PlayerId}",
            requireExactFlameTarget);
    }

    private IgnitionOutcome EvaluateOutcome(int gasLevel)
    {
        int acceptedExplosionLevel = Mathf.Clamp(explosionGasLevel, 0, 3);
        int acceptedFlareLevel = Mathf.Clamp(flareGasLevel, 0, acceptedExplosionLevel);

        if (gasLevel >= acceptedExplosionLevel)
            return IgnitionOutcome.ExplosionAndFire;

        if (gasLevel >= acceptedFlareLevel && gasSystem.HasGasInRoom)
            return IgnitionOutcome.Fire;

        return IgnitionOutcome.None;
    }

    private bool HasFlareTriggered()
    {
        return fusionSpawned ? FlareTriggeredNet : flareTriggeredLocal;
    }

    private bool HasExplosionTriggered()
    {
        return fusionSpawned ? ExplosionTriggeredNet : explosionTriggeredLocal;
    }

    private void SetFlareTriggered()
    {
        if (fusionSpawned)
            FlareTriggeredNet = true;
        else
            flareTriggeredLocal = true;
    }

    private void SetExplosionTriggered()
    {
        if (fusionSpawned)
            ExplosionTriggeredNet = true;
        else
            explosionTriggeredLocal = true;
    }

    private void PlayOutcomeForEveryone(
        IgnitionOutcome outcome,
        Vector3 firePosition,
        Vector3 explosionPosition)
    {
        if (fusionSpawned)
        {
            RPC_PlayOutcome(
                (int)outcome,
                firePosition,
                explosionPosition);
            return;
        }

        PlayOutcomeLocal(outcome, firePosition, explosionPosition);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_PlayOutcome(
        int outcomeValue,
        Vector3 firePosition,
        Vector3 explosionPosition)
    {
        PlayOutcomeLocal(
            (IgnitionOutcome)outcomeValue,
            firePosition,
            explosionPosition);
    }

    private void PlayOutcomeLocal(
        IgnitionOutcome outcome,
        Vector3 firePosition,
        Vector3 explosionPosition)
    {
        switch (outcome)
        {
            case IgnitionOutcome.Fire:
                PlayFlareLocal(firePosition);
                PlayFireIgnitedVoOnceLocal();
                break;

            case IgnitionOutcome.ExplosionAndFire:
                PlayFlareLocal(firePosition);

                if (explosionPrefab != null)
                {
                    GasExplosionEffect explosion = Instantiate(
                        explosionPrefab,
                        explosionPosition,
                        Quaternion.identity);
                    Destroy(explosion.gameObject, explosionLifetime);
                }
                else
                {
                    Debug.LogWarning(
                        "[GasIgnitionController] Explosion Prefab is not assigned.",
                        this);
                }
                break;
        }
    }

    private void PlayFireIgnitedVoOnceLocal()
    {
        if (fireIgnitedVoPlayedLocal || AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayOneShot(fireIgnitedVoKey);
        fireIgnitedVoPlayedLocal = true;
    }

    private void PlayFlareLocal(Vector3 position)
    {
        if (levelOneFlarePrefab != null)
        {
            GameObject flare = Instantiate(
                levelOneFlarePrefab,
                position,
                Quaternion.identity);
            Destroy(flare, flareLifetime);
        }
        else if (debugLog)
        {
            Debug.LogWarning(
                "[GasIgnitionController] Gas flare prefab is not assigned.",
                this);
        }
    }

    private FlameNode IgniteNearestFlameNode(
        Vector3 ignitionPosition,
        bool requireExactFlameTarget)
    {
        FlameNode nearestNode = null;
        float nearestDistanceSqr = float.MaxValue;

        foreach (FlameNode node in FlameNode.All)
        {
            if (node == null || !node.gameObject.activeInHierarchy)
                continue;

            float distanceSqr =
                (node.transform.position - ignitionPosition).sqrMagnitude;

            // Dedicated appliance sparks send the exact target position. This
            // prevents that spark from falling back to a different FlameNode.
            if (requireExactFlameTarget && distanceSqr > 0.0001f)
                continue;

            // Nodes that reject spread are reserved for their assigned source
            // and are ignored by generic sources such as a lighter or phone.
            if (!requireExactFlameTarget && !node.AllowIgniteFromSpread)
                continue;

            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearestNode = node;
            nearestDistanceSqr = distanceSqr;
        }

        if (nearestNode == null)
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "[GasIgnitionController] No active FlameNode was found.",
                    this);
            }

            return null;
        }

        if (FireManager.Instance != null)
            FireManager.Instance.RequestIgnite(nearestNode);
        else
            nearestNode.ForceIgnite();

        return nearestNode;
    }

    private void RaiseOutcomeEvent(
        IgnitionOutcome outcome,
        int gasLevel,
        string sourceId)
    {
        GameplayEventType eventType = outcome == IgnitionOutcome.ExplosionAndFire
            ? GameplayEventType.GasExploded
            : GameplayEventType.GasFlareIgnited;

        GameplayEventBus.Raise(
            eventType,
            actorId: string.IsNullOrWhiteSpace(sourceId) ? "IgnitionSource" : sourceId,
            targetId: gasSystem.gameObject.name,
            payload: gasLevel);
    }

    private void ResolveGasSystem()
    {
        if (gasSystem == null)
            gasSystem = GetComponent<GasSystem>();

        if (gasSystem == null)
            gasSystem = GetComponentInParent<GasSystem>();
    }

    private void OnValidate()
    {
        flareGasLevel = Mathf.Clamp(flareGasLevel, 0, 3);
        explosionGasLevel = Mathf.Clamp(explosionGasLevel, flareGasLevel, 3);
        flareLifetime = Mathf.Max(0.1f, flareLifetime);
        explosionLifetime = Mathf.Max(0.1f, explosionLifetime);
    }
}
