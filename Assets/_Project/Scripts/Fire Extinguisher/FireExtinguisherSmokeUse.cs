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

    [Tooltip("Bật GameObject của ParticleSystem khi xịt. Chỉ áp dụng cho smoke child, không phải object chứa script này.")]
    [SerializeField] private bool enableSmokeObjectWhileSpraying = true;

    [Tooltip("Chỉ người đang xịt mới bật Particle Collision để dập lửa. Người khác chỉ thấy smoke visual.")]
    [SerializeField] private bool collisionOnlyForLocalSprayer = true;

    [Header("Safety Pin Requirement")]
    [SerializeField] private bool requirePinRemoved = true;
    [SerializeField] private SafetyPinDetachOnPull safetyPin;

    [Header("Spray Limit")]
    [Tooltip("Tổng thời gian CO2 của bình. Ví dụ 60 nghĩa là bình chỉ xịt được tổng cộng 60 giây.")]
    [SerializeField] private float maxSpraySeconds = 60f;

    [SerializeField, Tooltip("Runtime read-only. Trong multiplayer, giá trị thật nằm ở RemainingSpraySecondsNet.")]
    private float remainingSpraySeconds = 60f;

    [Header("Start Delay")]
    [SerializeField] private float delayBeforeSpray = 0.5f;

    [Header("Optional Logic")]
    [SerializeField] private bool requireCanSpray = false;
    [SerializeField] private bool canSpray = true;

    [Header("Audio")]
    [SerializeField] private bool useAudio = true;
    [SerializeField] private string sprayLoopSound = "FESpray";

    [Header("Events")]
    public UnityEvent OnSprayStart;
    public UnityEvent OnSprayStop;
    public UnityEvent OnSprayEmpty;
    public UnityEvent<float> OnUseProgressChanged;

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool isSprayingLocal;
    [SerializeField] private bool waitingForDelay;
    [SerializeField] private bool wantsToSpray;
    [SerializeField] private bool localSmokeCanDamageFire;
    [SerializeField] private float currentUseProgress;

    [Header("Spray Time Debug")]
    [SerializeField] private float currentHoldSpraySeconds;
    [SerializeField] private float totalUsedSpraySeconds;
    [SerializeField] private float lastConsumeDeltaDebug;

    [Header("Network Debug")]
    [SerializeField] private bool hasStateAuthority;
    [SerializeField] private bool isSprayingNetDebug;
    [SerializeField] private float remainingNetDebug;
    [SerializeField] private string spraySourceDebug;

    [Networked] private bool IsSprayingNet { get; set; }
    [Networked] private bool CanSprayNet { get; set; }
    [Networked] private float RemainingSpraySecondsNet { get; set; }
    [Networked] private PlayerRef SpraySourcePlayerNet { get; set; }

    private Quaternion releasedLocalRotation;
    private Quaternion pressedLocalRotation;

    private Vector3 releasedLocalPosition;
    private Vector3 pressedLocalPosition;

    private float dampedUseStrength;
    private float lastUseTime;

    private Coroutine startRoutine;

    private bool originalSmokeCollisionEnabled;
    private bool hasCachedSmokeCollision;

    private bool lastObservedSprayingNet;
    private PlayerRef lastObservedSpraySourceNet;

    private bool emptyEventInvoked;

    public float RemainingSpraySeconds => fusionSpawned ? RemainingSpraySecondsNet : remainingSpraySeconds;
    public float MaxSpraySeconds => maxSpraySeconds;
    public float TotalUsedSpraySeconds => totalUsedSpraySeconds;
    public bool IsEmpty => RemainingSpraySeconds <= 0f;
    public bool IsSpraying => isSprayingLocal;
    public bool CanSpray => fusionSpawned ? CanSprayNet : canSpray;
    public ParticleSystem SmokeFX => smokeFX;

    private void Awake()
    {
        remainingSpraySeconds = Mathf.Clamp(maxSpraySeconds, 0f, maxSpraySeconds);

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
            RemainingSpraySecondsNet = Mathf.Clamp(maxSpraySeconds, 0f, maxSpraySeconds);
            CanSprayNet = canSpray;
            IsSprayingNet = false;
            SpraySourcePlayerNet = PlayerRef.None;
        }

        remainingSpraySeconds = RemainingSpraySecondsNet;
        canSpray = CanSprayNet;

        lastObservedSprayingNet = IsSprayingNet;
        lastObservedSpraySourceNet = SpraySourcePlayerNet;

        StopSprayVisual();
        RefreshDebugValues();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
        CancelStartDelay();
        StopSprayVisual();
        UpdateLever(0f);
    }

    private void Update()
    {
        // Single player fallback. Khi có Fusion thì KHÔNG trừ timer trong Update.
        if (!fusionSpawned && isSprayingLocal)
        {
            ConsumeSprayTimeLocal(Time.unscaledDeltaTime);
        }

        RefreshDebugValues();
    }

    public override void FixedUpdateNetwork()
    {
        if (!fusionSpawned)
            return;

        // Hướng 1: Host / StateAuthority là nơi duy nhất trừ CO2.
        if (!Object.HasStateAuthority)
            return;

        if (!IsSprayingNet)
            return;

        float deltaTime = Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;
        ConsumeSprayTimeAuthority(deltaTime);
    }

    public override void Render()
    {
        if (!fusionSpawned)
            return;

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

        RefreshDebugValues();
    }

    private void ConsumeSprayTimeLocal(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (remainingSpraySeconds <= 0f)
        {
            ForceStopBecauseEmptyLocal();
            return;
        }

        lastConsumeDeltaDebug = deltaTime;
        currentHoldSpraySeconds += deltaTime;
        totalUsedSpraySeconds += deltaTime;

        remainingSpraySeconds = Mathf.Clamp(
            remainingSpraySeconds - deltaTime,
            0f,
            maxSpraySeconds
        );

        if (remainingSpraySeconds <= 0f)
            ForceStopBecauseEmptyLocal();
    }

    private void ConsumeSprayTimeAuthority(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (RemainingSpraySecondsNet <= 0f)
        {
            ForceStopBecauseEmptyAuthority();
            return;
        }

        lastConsumeDeltaDebug = deltaTime;
        currentHoldSpraySeconds += deltaTime;
        totalUsedSpraySeconds += deltaTime;

        RemainingSpraySecondsNet = Mathf.Clamp(
            RemainingSpraySecondsNet - deltaTime,
            0f,
            maxSpraySeconds
        );

        remainingSpraySeconds = RemainingSpraySecondsNet;

        if (RemainingSpraySecondsNet <= 0f)
            ForceStopBecauseEmptyAuthority();
    }

    private void ForceStopBecauseEmptyLocal()
    {
        remainingSpraySeconds = 0f;
        wantsToSpray = false;

        CancelStartDelay();
        StopSprayVisual();
        InvokeEmptyEventOnce();
    }

    private void ForceStopBecauseEmptyAuthority()
    {
        if (!Object.HasStateAuthority)
            return;

        RemainingSpraySecondsNet = 0f;
        remainingSpraySeconds = 0f;

        IsSprayingNet = false;
        SpraySourcePlayerNet = PlayerRef.None;

        RPC_ApplySprayState(false, PlayerRef.None, RemainingSpraySecondsNet);

        wantsToSpray = false;

        CancelStartDelay();
        StopSprayVisual();
        InvokeEmptyEventOnce();
    }

    private void InvokeEmptyEventOnce()
    {
        if (emptyEventInvoked)
            return;

        emptyEventInvoked = true;
        OnSprayEmpty?.Invoke();
    }

    private void RefreshDebugValues()
    {
        hasStateAuthority = fusionSpawned && Object != null && Object.HasStateAuthority;
        isSprayingNetDebug = fusionSpawned && IsSprayingNet;
        remainingNetDebug = fusionSpawned ? RemainingSpraySecondsNet : remainingSpraySeconds;

        if (!fusionSpawned)
        {
            spraySourceDebug = "No Fusion";
        }
        else if (SpraySourcePlayerNet == PlayerRef.None)
        {
            spraySourceDebug = "None";
        }
        else
        {
            spraySourceDebug = $"Player_{SpraySourcePlayerNet.PlayerId}";
        }
    }

    private void OnDisable()
    {
        CancelStartDelay();

        if (fusionSpawned && isSprayingLocal)
            RequestStopSpray();

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
        if (triggerLever == null)
            return;

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
        if (!CanAttemptSprayLocal())
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

        if (!wantsToSpray)
            yield break;

        if (!CanAttemptSprayLocal())
            yield break;

        if (currentUseProgress < sprayThreshold)
            yield break;

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

    private bool CanAttemptSprayLocal()
    {
        if (RemainingSpraySeconds <= 0f)
            return false;

        if (requireCanSpray && !CanSpray)
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

    private bool CanAttemptSprayAuthority(bool requestingClientSaysPinRemoved)
    {
        if (RemainingSpraySecondsNet <= 0f)
            return false;

        if (requireCanSpray && !CanSprayNet)
            return false;

        if (requirePinRemoved)
        {
            bool authorityKnowsPinRemoved = safetyPin != null && safetyPin.IsRemoved;

            if (!authorityKnowsPinRemoved && !requestingClientSaysPinRemoved)
                return false;
        }

        return true;
    }

    private bool IsPinRemovedLocal()
    {
        if (!requirePinRemoved)
            return true;

        return safetyPin != null && safetyPin.IsRemoved;
    }

    private void RequestStartSpray()
    {
        if (!CanAttemptSprayLocal())
            return;

        currentHoldSpraySeconds = 0f;

        if (!fusionSpawned)
        {
            StartSprayVisual(true);
            return;
        }

        bool pinRemovedLocal = IsPinRemovedLocal();

        if (Object.HasStateAuthority)
        {
            PlayerRef source = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            ApplySprayStateOnAuthority(true, source, pinRemovedLocal);
        }
        else
        {
            // Client chỉ gửi request. Không tự bật smoke trước khi Host xác nhận.
            RPC_RequestStartSpray(pinRemovedLocal);
        }
    }

    private void RequestStopSpray()
    {
        CancelStartDelay();

        if (!fusionSpawned)
        {
            StopSprayVisual();
            return;
        }

        if (Object.HasStateAuthority)
        {
            PlayerRef source = Runner != null ? Runner.LocalPlayer : PlayerRef.None;
            ApplySprayStateOnAuthority(false, source, true);
        }
        else
        {
            RPC_RequestStopSpray();
            StopSprayVisual();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStartSpray(bool requestingClientSaysPinRemoved, RpcInfo info = default)
    {
        ApplySprayStateOnAuthority(true, info.Source, requestingClientSaysPinRemoved);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStopSpray(RpcInfo info = default)
    {
        ApplySprayStateOnAuthority(false, info.Source, true);
    }

    private void ApplySprayStateOnAuthority(bool spraying, PlayerRef source, bool requestingClientSaysPinRemoved)
    {
        if (fusionSpawned && !Object.HasStateAuthority)
            return;

        if (spraying)
        {
            if (!CanAttemptSprayAuthority(requestingClientSaysPinRemoved))
            {
                IsSprayingNet = false;
                SpraySourcePlayerNet = PlayerRef.None;
                RPC_ApplySprayState(false, source, RemainingSpraySecondsNet);
                RefreshDebugValues();
                return;
            }

            IsSprayingNet = true;
            SpraySourcePlayerNet = source;
            RPC_ApplySprayState(true, source, RemainingSpraySecondsNet);
        }
        else
        {
            IsSprayingNet = false;
            SpraySourcePlayerNet = PlayerRef.None;
            RPC_ApplySprayState(false, source, RemainingSpraySecondsNet);
        }

        RefreshDebugValues();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplySprayState(bool spraying, PlayerRef source, float remaining)
    {
        remainingSpraySeconds = Mathf.Clamp(remaining, 0f, maxSpraySeconds);

        if (spraying && remainingSpraySeconds > 0f)
        {
            bool isLocalSource = IsLocalPlayer(source);
            StartSprayVisual(isLocalSource);
        }
        else
        {
            StopSprayVisual();
        }

        RefreshDebugValues();
    }

    private void StartSprayVisual(bool canDamageFire)
    {
        if (RemainingSpraySeconds <= 0f)
        {
            StopSprayVisual();
            return;
        }

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

        if (!wasSpraying && useAudio && AudioManager.Instance != null)
        {
            if (!AudioManager.Instance.IsPlaying(sprayLoopSound))
                AudioManager.Instance.Play(sprayLoopSound);
        }

        if (!wasSpraying)
            OnSprayStart?.Invoke();
    }

    private void StopSprayVisual()
    {
        bool wasSpraying = isSprayingLocal;

        isSprayingLocal = false;
        localSmokeCanDamageFire = false;

        if (smokeFX != null)
        {
            SetSmokeCollisionEnabled(false);

            if (smokeFX.isPlaying)
                smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (wasSpraying && useAudio && AudioManager.Instance != null)
            AudioManager.Instance.Stop(sprayLoopSound);

        if (wasSpraying)
            OnSprayStop?.Invoke();
    }

    private void StopSmokeImmediate()
    {
        isSprayingLocal = false;
        localSmokeCanDamageFire = false;

        if (smokeFX == null)
            return;

        SetSmokeCollisionEnabled(false);
        smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (enableSmokeObjectWhileSpraying)
            smokeFX.gameObject.SetActive(false);
    }

    private void CacheSmokeCollisionState()
    {
        if (smokeFX == null)
            return;

        ParticleSystem.CollisionModule collision = smokeFX.collision;
        originalSmokeCollisionEnabled = collision.enabled;
        hasCachedSmokeCollision = true;
    }

    private void SetSmokeCollisionEnabled(bool enabledForDamage)
    {
        if (smokeFX == null)
            return;

        if (!collisionOnlyForLocalSprayer)
            return;

        if (!hasCachedSmokeCollision)
            CacheSmokeCollisionState();

        ParticleSystem.CollisionModule collision = smokeFX.collision;
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
            ApplyCanSprayOnAuthority(value);
        }
        else
        {
            RPC_RequestSetCanSpray(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetCanSpray(bool value)
    {
        ApplyCanSprayOnAuthority(value);
    }

    private void ApplyCanSprayOnAuthority(bool value)
    {
        if (!Object.HasStateAuthority)
            return;

        CanSprayNet = value;
        canSpray = value;

        RPC_ApplyCanSpray(value);

        if (!value)
            ApplySprayStateOnAuthority(false, SpraySourcePlayerNet, true);

        RefreshDebugValues();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyCanSpray(bool value)
    {
        canSpray = value;

        if (!canSpray)
            StopSprayVisual();

        RefreshDebugValues();
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
        emptyEventInvoked = false;

        if (!fusionSpawned)
        {
            remainingSpraySeconds = maxSpraySeconds;
            totalUsedSpraySeconds = 0f;
            currentHoldSpraySeconds = 0f;
            StopSprayVisual();
            return;
        }

        if (Object.HasStateAuthority)
        {
            RemainingSpraySecondsNet = maxSpraySeconds;
            remainingSpraySeconds = maxSpraySeconds;

            totalUsedSpraySeconds = 0f;
            currentHoldSpraySeconds = 0f;

            IsSprayingNet = false;
            SpraySourcePlayerNet = PlayerRef.None;

            RPC_ApplySprayState(false, PlayerRef.None, RemainingSpraySecondsNet);
            RefreshDebugValues();
        }
        else
        {
            RPC_RequestRefill();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRefill()
    {
        if (!Object.HasStateAuthority)
            return;

        emptyEventInvoked = false;

        RemainingSpraySecondsNet = maxSpraySeconds;
        remainingSpraySeconds = maxSpraySeconds;

        totalUsedSpraySeconds = 0f;
        currentHoldSpraySeconds = 0f;

        IsSprayingNet = false;
        SpraySourcePlayerNet = PlayerRef.None;

        RPC_ApplySprayState(false, PlayerRef.None, RemainingSpraySecondsNet);
        RefreshDebugValues();
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
