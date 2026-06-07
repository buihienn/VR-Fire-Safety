using System.Collections;
using Fusion;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Events;

public class FireExtinguisherSmokeUse : NetworkBehaviour, IHandGrabUseDelegate
{
    [Header("Lever / Trigger")]
    [SerializeField] private Transform triggerLever;

    [Tooltip("Góc xoay thêm khi bóp cò. Nếu xoay sai chiều, đổi dấu X/Y/Z.")]
    [SerializeField] private Vector3 pressedEulerOffset = new Vector3(-25f, 0f, 0f);

    [Tooltip("Dùng khi pivot của Trigger_Lever bị lệch, cần kéo/move lever thêm một chút.")]
    [SerializeField] private bool usePositionOffset = false;

    [SerializeField] private Vector3 pressedLocalPositionOffset = Vector3.zero;

    [Header("Use Settings")]
    [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float sprayThreshold = 0.7f;
    [SerializeField] private float triggerSpeed = 10f;

    [SerializeField]
    private AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Smoke / CO2 FX")]
    [SerializeField] private ParticleSystem smokeFX;

    [Tooltip("Bật nếu object ParticleSystem đang bị SetActive(false) lúc đầu.")]
    [SerializeField] private bool enableSmokeObjectWhileSpraying = true;

    [Tooltip("Chỉ người đang xịt mới bật Particle Collision để dập lửa. Client khác chỉ thấy smoke visual.")]
    [SerializeField] private bool collisionOnlyForLocalSprayer = true;

    [Header("Safety Pin Requirement")]
    [SerializeField] private bool requirePinRemoved = true;
    [SerializeField] private SafetyPinDetachOnPull safetyPin;

    [Header("Spray Limit")]
    [Tooltip("Tổng thời gian được phép xịt cho cả bình.")]
    [SerializeField] private float maxSpraySeconds = 60f;

    [SerializeField, Tooltip("Runtime read-only. Thời gian xịt còn lại.")]
    private float remainingSpraySeconds = 60f;

    [Header("Start Delay")]
    [Tooltip("Giống NozzleFireSmokeTrigger: bóp cò xong chờ một chút rồi mới phun.")]
    [SerializeField] private float delayBeforeSpray = 1f;

    [Header("Optional Logic")]
    [SerializeField] private bool requireCanSpray = false;

    [Tooltip("Nếu requireCanSpray = true, chỉ phun khi biến này true. Có thể gọi AllowSpray() sau khi rút chốt.")]
    [SerializeField] private bool canSpray = true;

    [Header("Audio")]
    [SerializeField] private bool useAudio = true;
    [SerializeField] private string sprayLoopSound = "FESpray";

    [Header("Events")]
    public UnityEvent OnSprayStart;
    public UnityEvent OnSprayStop;
    public UnityEvent<float> OnUseProgressChanged;

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool isSprayingLocal;
    [SerializeField] private bool localSmokeCanDamageFire;
    [SerializeField] private bool waitingForDelay;
    [SerializeField] private bool wantsToSpray;
    [SerializeField] private float currentUseProgress;

    [Networked] private bool IsSprayingNet { get; set; }
    [Networked] private bool CanSprayNet { get; set; }
    [Networked] private float RemainingSpraySecondsNet { get; set; }
    [Networked] private PlayerRef SpraySourcePlayerNet { get; set; }

    private Quaternion releasedLocalRotation;
    private Quaternion pressedLocalRotation;

    private Vector3 releasedLocalPosition;
    private Vector3 pressedLocalPosition;

    private float dampedUseStrength = 0f;
    private float lastUseTime;

    private Coroutine startRoutine;
    private bool originalSmokeCollisionEnabled;
    private bool hasCachedSmokeCollision;

    private bool lastObservedSprayingNet;
    private PlayerRef lastObservedSpraySourceNet;

    public float RemainingSpraySeconds => remainingSpraySeconds;
    public bool IsEmpty => remainingSpraySeconds <= 0f;
    public bool IsSpraying => isSprayingLocal;
    public bool CanSpray => canSpray;
    public ParticleSystem SmokeFX => smokeFX;

    private void Awake()
    {
        remainingSpraySeconds = maxSpraySeconds;

        if (triggerLever != null)
        {
            releasedLocalRotation = triggerLever.localRotation;
            pressedLocalRotation = releasedLocalRotation * Quaternion.Euler(pressedEulerOffset);

            releasedLocalPosition = triggerLever.localPosition;
            pressedLocalPosition = releasedLocalPosition + pressedLocalPositionOffset;
        }

        CacheSmokeCollisionState();
        StopSmokeImmediate();
        UpdateLever(0f);
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
        {
            RemainingSpraySecondsNet = Mathf.Clamp(remainingSpraySeconds, 0f, maxSpraySeconds);
            CanSprayNet = canSpray;
            IsSprayingNet = false;
            SpraySourcePlayerNet = PlayerRef.None;
        }
        else
        {
            remainingSpraySeconds = RemainingSpraySecondsNet;
            canSpray = CanSprayNet;
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
            StopSprayVisual();
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
        canSpray = CanSprayNet;

        bool sprayingChanged = IsSprayingNet != lastObservedSprayingNet;
        bool sourceChanged = SpraySourcePlayerNet != lastObservedSpraySourceNet;

        if (sprayingChanged || sourceChanged)
        {
            lastObservedSprayingNet = IsSprayingNet;
            lastObservedSpraySourceNet = SpraySourcePlayerNet;

            if (IsSprayingNet)
            {
                bool isLocalSource = IsLocalPlayer(SpraySourcePlayerNet);
                StartSprayVisual(isLocalSource);
            }
            else
            {
                StopSprayVisual();
            }
        }
    }

    private void Update()
    {
        // Single-player fallback khi chưa chạy Fusion.
        if (fusionSpawned) return;

        if (!isSprayingLocal) return;

        remainingSpraySeconds -= Time.deltaTime;

        if (remainingSpraySeconds <= 0f)
        {
            remainingSpraySeconds = 0f;
            StopSprayVisual();
        }
    }

    private void OnDisable()
    {
        CancelStartDelay();

        if (fusionSpawned && IsSpraying)
        {
            RequestStopSpray();
        }

        StopSprayVisual();
        UpdateLever(0f);
    }

    public void BeginUse()
    {
        dampedUseStrength = 0f;
        lastUseTime = Time.realtimeSinceStartup;
        wantsToSpray = false;
    }

    public void EndUse()
    {
        dampedUseStrength = 0f;
        currentUseProgress = 0f;
        wantsToSpray = false;

        CancelStartDelay();
        UpdateLever(0f);
        RequestStopSpray();
    }

    public float ComputeUseStrength(float strength)
    {
        float delta = Time.realtimeSinceStartup - lastUseTime;
        lastUseTime = Time.realtimeSinceStartup;

        if (strength > dampedUseStrength)
        {
            dampedUseStrength = Mathf.Lerp(
                dampedUseStrength,
                strength,
                triggerSpeed * delta
            );
        }
        else
        {
            dampedUseStrength = strength;
        }

        float progress = strengthCurve.Evaluate(dampedUseStrength);
        currentUseProgress = progress;

        UpdateLever(progress);
        UpdateSprayByProgress(progress);

        OnUseProgressChanged?.Invoke(progress);

        return progress;
    }

    private void UpdateLever(float progress)
    {
        if (triggerLever == null) return;

        triggerLever.localRotation = Quaternion.Lerp(
            releasedLocalRotation,
            pressedLocalRotation,
            progress
        );

        if (usePositionOffset)
        {
            triggerLever.localPosition = Vector3.Lerp(
                releasedLocalPosition,
                pressedLocalPosition,
                progress
            );
        }
    }

    private void UpdateSprayByProgress(float progress)
    {
        if (!CanAttemptSpray())
        {
            wantsToSpray = false;
            CancelStartDelay();
            RequestStopSpray();
            return;
        }

        if (progress >= sprayThreshold)
        {
            if (!wantsToSpray && !waitingForDelay && !isSprayingLocal)
            {
                wantsToSpray = true;
                BeginSprayAfterDelay();
            }
        }
        else if (progress <= releaseThreshold)
        {
            wantsToSpray = false;
            CancelStartDelay();
            RequestStopSpray();
        }
    }

    private void BeginSprayAfterDelay()
    {
        CancelStartDelay();

        if (delayBeforeSpray <= 0f)
        {
            RequestStartSpray();
            return;
        }

        startRoutine = StartCoroutine(StartSprayAfterDelay());
    }

    private IEnumerator StartSprayAfterDelay()
    {
        waitingForDelay = true;

        yield return new WaitForSeconds(delayBeforeSpray);

        waitingForDelay = false;
        startRoutine = null;

        if (!wantsToSpray) yield break;
        if (!CanAttemptSpray()) yield break;
        if (currentUseProgress < sprayThreshold) yield break;

        RequestStartSpray();
    }

    private void CancelStartDelay()
    {
        waitingForDelay = false;

        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }
    }

    private bool CanAttemptSpray()
    {
        if (IsEmpty)
            return false;

        if (requireCanSpray && !canSpray)
            return false;

        if (requirePinRemoved)
        {
            if (safetyPin == null)
            {
                Debug.LogWarning("FireExtinguisherSmokeUse: chưa gán SafetyPinDetachOnPull.", this);
                return false;
            }

            if (!safetyPin.IsRemoved)
                return false;
        }

        return true;
    }

    private void RequestStartSpray()
    {
        if (!CanAttemptSpray()) return;

        // Local responsive visual.
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
        CancelStartDelay();

        // Local responsive stop.
        StopSprayVisual();

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

            if (requireCanSpray && !CanSprayNet)
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
            StopSprayVisual();
        }
    }

    private void StartSprayVisual(bool canDamageFire)
    {
        bool wasSpraying = isSprayingLocal;

        isSprayingLocal = true;
        localSmokeCanDamageFire = canDamageFire;

        if (smokeFX != null)
        {
            if (enableSmokeObjectWhileSpraying)
                smokeFX.gameObject.SetActive(true);

            SetSmokeCollisionEnabled(canDamageFire);

            if (!smokeFX.isPlaying)
                smokeFX.Play(true);
        }

        if (useAudio && AudioManager.Instance != null)
        {
            if (!AudioManager.Instance.IsPlaying(sprayLoopSound))
                AudioManager.Instance.Play(sprayLoopSound);
        }

        if (!wasSpraying)
            OnSprayStart?.Invoke();
    }

    private void StopSprayVisual()
    {
        if (!isSprayingLocal && smokeFX == null)
            return;

        bool wasSpraying = isSprayingLocal;

        isSprayingLocal = false;
        localSmokeCanDamageFire = false;

        if (smokeFX != null)
        {
            SetSmokeCollisionEnabled(false);

            if (smokeFX.isPlaying)
                smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (useAudio && AudioManager.Instance != null)
            AudioManager.Instance.Stop(sprayLoopSound);

        if (wasSpraying)
            OnSprayStop?.Invoke();
    }

    private void StopSmokeImmediate()
    {
        isSprayingLocal = false;
        localSmokeCanDamageFire = false;

        if (smokeFX == null) return;

        SetSmokeCollisionEnabled(false);

        smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (enableSmokeObjectWhileSpraying)
            smokeFX.gameObject.SetActive(false);
    }

    private void CacheSmokeCollisionState()
    {
        if (smokeFX == null) return;

        var collision = smokeFX.collision;
        originalSmokeCollisionEnabled = collision.enabled;
        hasCachedSmokeCollision = true;
    }

    private void SetSmokeCollisionEnabled(bool enabledForDamage)
    {
        if (smokeFX == null) return;
        if (!collisionOnlyForLocalSprayer) return;

        if (!hasCachedSmokeCollision)
            CacheSmokeCollisionState();

        var collision = smokeFX.collision;
        collision.enabled = enabledForDamage && originalSmokeCollisionEnabled;
    }

    public void SetCanSpray(bool value)
    {
        if (!fusionSpawned)
        {
            canSpray = value;

            if (!canSpray)
                RequestStopSpray();

            return;
        }

        if (Object.HasStateAuthority)
        {
            ApplyCanSprayOnHost(value);
        }
        else
        {
            RPC_RequestSetCanSpray(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetCanSpray(bool value)
    {
        ApplyCanSprayOnHost(value);
    }

    private void ApplyCanSprayOnHost(bool value)
    {
        if (!Object.HasStateAuthority) return;

        CanSprayNet = value;
        canSpray = value;

        RPC_ApplyCanSpray(value);

        if (!value)
            ApplySprayStateOnHost(false, SpraySourcePlayerNet);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyCanSpray(bool value)
    {
        canSpray = value;

        if (!canSpray)
            StopSprayVisual();
    }

    public void AllowSpray()
    {
        SetCanSpray(true);
    }

    public void BlockSpray()
    {
        SetCanSpray(false);
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
            return Object.HasStateAuthority;

        return player == Runner.LocalPlayer;
    }
}