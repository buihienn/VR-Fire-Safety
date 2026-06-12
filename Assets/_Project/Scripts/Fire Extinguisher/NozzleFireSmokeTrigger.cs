using System.Collections;
using Fusion;
using UnityEngine;

public class NozzleFireSmokeTrigger : NetworkBehaviour
{
    [Header("Smoke")]
    public ParticleSystem fireSmoke;
    public float delay = 1f;

    [Header("Safety Pin Requirement")]
    public bool requirePinRemoved = true;
    public SafetyPinDetachOnPull safetyPin;

    [Header("Spray Limit")]
    [Tooltip("Tổng thời gian được phép xịt cho cả bình (giây).")]
    public float maxSpraySeconds = 60f;

    [SerializeField, Tooltip("Thời gian xịt còn lại. Runtime sẽ tự giảm.")]
    private float remainingSpraySeconds = 60f;

    [Header("Multiplayer")]
    [Tooltip("Chỉ người đang xịt mới bật Particle Collision để dập lửa. Người khác chỉ thấy smoke visual.")]
    [SerializeField] private bool collisionOnlyForLocalSprayer = true;

    [Tooltip("Nếu ParticleSystem object đang inactive lúc đầu thì bật lên khi phun.")]
    [SerializeField] private bool enableSmokeObjectWhileSpraying = true;

    [Header("Audio")]
    [SerializeField] private string sprayLoopSound = "FESpray";

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool isSpraying;
    [SerializeField] private bool localSmokeCanDamageFire;

    [Networked] private bool IsSprayingNet { get; set; }
    [Networked] private float RemainingSpraySecondsNet { get; set; }
    [Networked] private PlayerRef SpraySourcePlayerNet { get; set; }

    private Coroutine startRoutine;
    private bool lastObservedSprayingNet;
    private PlayerRef lastObservedSpraySourceNet;

    private bool originalSmokeCollisionEnabled;
    private bool hasCachedSmokeCollision;

    public float RemainingSpraySeconds => remainingSpraySeconds;
    public bool IsEmpty => remainingSpraySeconds <= 0f;
    public bool IsSpraying => isSpraying;

    private void Awake()
    {
        // Mỗi lần vào scene thì bình bắt đầu đầy.
        remainingSpraySeconds = maxSpraySeconds;

        CacheSmokeCollisionState();
        StopSmokeImmediate();
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
        {
            RemainingSpraySecondsNet = Mathf.Clamp(remainingSpraySeconds, 0f, maxSpraySeconds);
            IsSprayingNet = false;
            SpraySourcePlayerNet = PlayerRef.None;
        }
        else
        {
            remainingSpraySeconds = RemainingSpraySecondsNet;
        }

        lastObservedSprayingNet = IsSprayingNet;
        lastObservedSpraySourceNet = SpraySourcePlayerNet;

        if (IsSprayingNet)
        {
            bool isLocalSource = IsLocalPlayer(SpraySourcePlayerNet);
            StartSprayVisual(isLocalSource);
        }
        else
        {
            StopSprayingVisualOnly();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsSprayingNet) return;

        float remaining = RemainingSpraySecondsNet;
        remaining -= Runner.DeltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            RemainingSpraySecondsNet = remaining;
            ApplySprayStateOnHost(false, SpraySourcePlayerNet);
            return;
        }

        RemainingSpraySecondsNet = remaining;
    }

    public override void Render()
    {
        if (!fusionSpawned) return;

        remainingSpraySeconds = RemainingSpraySecondsNet;

        bool sprayingChanged = IsSprayingNet != lastObservedSprayingNet;
        bool sourceChanged = SpraySourcePlayerNet != lastObservedSpraySourceNet;

        if (!sprayingChanged && !sourceChanged)
            return;

        lastObservedSprayingNet = IsSprayingNet;
        lastObservedSpraySourceNet = SpraySourcePlayerNet;

        if (IsSprayingNet)
        {
            bool isLocalSource = IsLocalPlayer(SpraySourcePlayerNet);
            StartSprayVisual(isLocalSource);
        }
        else
        {
            StopSprayingVisualOnly();
        }
    }

    private void Update()
    {
        // Single-player fallback nếu chưa chạy Fusion.
        if (fusionSpawned) return;
        if (!isSpraying) return;

        remainingSpraySeconds -= Time.deltaTime;

        if (remainingSpraySeconds <= 0f)
        {
            remainingSpraySeconds = 0f;
            StopSpraying();
        }
    }

    private void OnDisable()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        if (fusionSpawned && isSpraying)
            RequestStopSpray();

        StopSprayingVisualOnly();
    }

    public void OnGrab()
    {
        // Hết bình thì không cho xịt nữa.
        if (IsEmpty)
            return;

        if (requirePinRemoved)
        {
            if (safetyPin == null)
            {
                Debug.LogWarning("NozzleFireSmokeTrigger: chưa gán SafetyPinDetachOnPull.", this);
                return;
            }

            if (!safetyPin.IsRemoved)
            {
                // Chưa rút chốt thì không cho xịt.
                return;
            }
        }

        if (isSpraying || startRoutine != null)
            return;

        startRoutine = StartCoroutine(StartAfterDelay());
    }

    public void OnRelease()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        StopSpraying();
    }

    private IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        startRoutine = null;

        // Sau delay mà đã hết bình thì thôi.
        if (IsEmpty)
            yield break;

        if (requirePinRemoved)
        {
            if (safetyPin == null || !safetyPin.IsRemoved)
                yield break;
        }

        RequestStartSpray();
    }

    private void RequestStartSpray()
    {
        if (IsEmpty) return;

        // Local responsive visual cho người đang cầm.
        StartSprayVisual(true);

        if (!fusionSpawned)
            return;

        if (Object.HasStateAuthority)
        {
            PlayerRef source = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            ApplySprayStateOnHost(true, source);
        }
        else
        {
            RPC_RequestStartSpray();
        }
    }

    private void RequestStopSpray()
    {
        StopSprayingVisualOnly();

        if (!fusionSpawned)
            return;

        if (Object.HasStateAuthority)
        {
            PlayerRef source = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            ApplySprayStateOnHost(false, source);
        }
        else
        {
            RPC_RequestStopSpray();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartSpray(RpcInfo info = default)
    {
        ApplySprayStateOnHost(true, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStopSpray(RpcInfo info = default)
    {
        ApplySprayStateOnHost(false, info.Source);
    }

    private void ApplySprayStateOnHost(bool spraying, PlayerRef source)
    {
        if (!Object.HasStateAuthority) return;

        if (spraying)
        {
            if (RemainingSpraySecondsNet <= 0f)
            {
                IsSprayingNet = false;
                RPC_ApplySprayState(false, source, RemainingSpraySecondsNet);
                return;
            }

            IsSprayingNet = true;
            SpraySourcePlayerNet = source;
            RPC_ApplySprayState(true, source, RemainingSpraySecondsNet);
        }
        else
        {
            IsSprayingNet = false;
            RPC_ApplySprayState(false, source, RemainingSpraySecondsNet);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplySprayState(bool spraying, PlayerRef source, float remaining)
    {
        remainingSpraySeconds = Mathf.Clamp(remaining, 0f, maxSpraySeconds);

        if (spraying)
        {
            bool isLocalSource = IsLocalPlayer(source);
            StartSprayVisual(isLocalSource);
        }
        else
        {
            StopSprayingVisualOnly();
        }
    }

    private void StopSpraying()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        RequestStopSpray();
    }

    private void StartSprayVisual(bool canDamageFire)
    {
        bool wasSpraying = isSpraying;

        isSpraying = true;
        localSmokeCanDamageFire = canDamageFire;

        if (fireSmoke != null)
        {
            if (enableSmokeObjectWhileSpraying)
                fireSmoke.gameObject.SetActive(true);

            SetSmokeCollisionEnabled(canDamageFire);

            if (!fireSmoke.isPlaying)
                fireSmoke.Play(true);
        }

        if (AudioManager.Instance != null)
        {
            if (!AudioManager.Instance.IsPlaying(sprayLoopSound))
                AudioManager.Instance.Play(sprayLoopSound);
        }
    }

    private void StopSprayingVisualOnly()
    {
        bool wasSpraying = isSpraying;

        isSpraying = false;
        localSmokeCanDamageFire = false;

        if (fireSmoke != null)
        {
            SetSmokeCollisionEnabled(false);

            if (fireSmoke.isPlaying)
                fireSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.Stop(sprayLoopSound);
    }

    private void StopSmokeImmediate()
    {
        isSpraying = false;
        localSmokeCanDamageFire = false;

        if (fireSmoke == null) return;

        SetSmokeCollisionEnabled(false);
        fireSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (enableSmokeObjectWhileSpraying)
            fireSmoke.gameObject.SetActive(false);
    }

    private void CacheSmokeCollisionState()
    {
        if (fireSmoke == null) return;

        var collision = fireSmoke.collision;
        originalSmokeCollisionEnabled = collision.enabled;
        hasCachedSmokeCollision = true;
    }

    private void SetSmokeCollisionEnabled(bool enabledForDamage)
    {
        if (fireSmoke == null) return;
        if (!collisionOnlyForLocalSprayer) return;

        if (!hasCachedSmokeCollision)
            CacheSmokeCollisionState();

        var collision = fireSmoke.collision;
        collision.enabled = enabledForDamage && originalSmokeCollisionEnabled;
    }

    public void Refill()
    {
        if (!fusionSpawned)
        {
            remainingSpraySeconds = maxSpraySeconds;
            return;
        }

        if (Object.HasStateAuthority)
        {
            RemainingSpraySecondsNet = maxSpraySeconds;
            remainingSpraySeconds = maxSpraySeconds;
        }
        else
        {
            RPC_RequestRefill();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRefill()
    {
        RemainingSpraySecondsNet = maxSpraySeconds;
        remainingSpraySeconds = maxSpraySeconds;
    }

    private bool IsLocalPlayer(PlayerRef player)
    {
        if (Runner == null)
            return true;

        if (player == PlayerRef.None)
            return Object != null && Object.HasStateAuthority;

        return player == Runner.LocalPlayer;
    }
}