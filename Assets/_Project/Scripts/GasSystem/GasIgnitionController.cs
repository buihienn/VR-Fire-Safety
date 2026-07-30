using Fusion;
using UnityEngine;

public class GasIgnitionController : NetworkBehaviour
{
    private enum IgnitionOutcome
    {
        None = 0,
        Flare = 1,
        Explosion = 2
    }

    [Header("References")]
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private GameObject levelOneFlarePrefab;
    [SerializeField] private GasExplosionEffect explosionPrefab;
    [SerializeField] private Transform explosionPoint;

    [Header("Gas Rules")]
    [Range(0, 3)]
    [SerializeField] private int flareGasLevel = 1;

    [Range(0, 3)]
    [SerializeField] private int explosionGasLevel = 2;

    [Header("Effect Lifetime")]
    [SerializeField, Min(0.1f)] private float flareLifetime = 3f;
    [SerializeField, Min(0.1f)] private float explosionLifetime = 6f;

    [Header("Game Flow")]
    [SerializeField] private bool endMatchOnExplosion = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Networked] private NetworkBool FlareTriggeredNet { get; set; }
    [Networked] private NetworkBool ExplosionTriggeredNet { get; set; }

    private bool fusionSpawned;
    private bool flareTriggeredLocal;
    private bool explosionTriggeredLocal;

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

    public bool TryIgnite(Vector3 ignitionPosition, string sourceId)
    {
        ResolveGasSystem();

        if (!HasIgnitionAuthority || gasSystem == null)
            return false;

        int gasLevel = gasSystem.GasLevel();
        IgnitionOutcome outcome = EvaluateOutcome(gasLevel);

        if (outcome == IgnitionOutcome.None)
            return false;

        if (outcome == IgnitionOutcome.Explosion)
        {
            if (HasExplosionTriggered())
                return false;

            SetExplosionTriggered();
        }
        else
        {
            if (HasFlareTriggered())
                return false;

            SetFlareTriggered();
        }

        Vector3 effectPosition =
            outcome == IgnitionOutcome.Explosion && explosionPoint != null
                ? explosionPoint.position
                : ignitionPosition;

        PlayOutcomeForEveryone(outcome, effectPosition, gasLevel);
        RaiseOutcomeEvent(outcome, gasLevel, sourceId);

        if (outcome == IgnitionOutcome.Explosion &&
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

    private IgnitionOutcome EvaluateOutcome(int gasLevel)
    {
        int acceptedExplosionLevel = Mathf.Clamp(explosionGasLevel, 0, 3);
        int acceptedFlareLevel = Mathf.Clamp(flareGasLevel, 0, acceptedExplosionLevel);

        if (gasLevel >= acceptedExplosionLevel)
            return IgnitionOutcome.Explosion;

        if (gasLevel >= acceptedFlareLevel && gasSystem.HasGasInRoom)
            return IgnitionOutcome.Flare;

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
        Vector3 position,
        int gasLevel)
    {
        if (fusionSpawned)
        {
            RPC_PlayOutcome((int)outcome, position, gasLevel);
            return;
        }

        PlayOutcomeLocal(outcome, position);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayOutcome(int outcomeValue, Vector3 position, int gasLevel)
    {
        PlayOutcomeLocal((IgnitionOutcome)outcomeValue, position);
    }

    private void PlayOutcomeLocal(IgnitionOutcome outcome, Vector3 position)
    {
        switch (outcome)
        {
            case IgnitionOutcome.Flare:
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
                        "[GasIgnitionController] Level One Flare Prefab is not assigned.",
                        this);
                }
                break;

            case IgnitionOutcome.Explosion:
                if (explosionPrefab != null)
                {
                    GasExplosionEffect explosion = Instantiate(
                        explosionPrefab,
                        position,
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

    private void RaiseOutcomeEvent(
        IgnitionOutcome outcome,
        int gasLevel,
        string sourceId)
    {
        GameplayEventType eventType = outcome == IgnitionOutcome.Explosion
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
