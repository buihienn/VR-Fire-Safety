using Fusion;
using UnityEngine;

public class GasValveLeakByAngle : NetworkBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    [SerializeField] private Transform valveHandle;
    [SerializeField] private GasSystem gasSystem;

    [Header("Valve Read")]
    [SerializeField] private Axis localAxis = Axis.Y;

    [Tooltip("Goc dong hoan toan")]
    [SerializeField] private float closedAngle = -45f;

    [Tooltip("Goc mo toi da")]
    [SerializeField] private float fullyOpenAngle = 135f;

    [Tooltip("Trong vung nay coi nhu da dong kin")]
    [SerializeField] private float closedDeadZoneDeg = 5f;

    [Header("Network Send Optimization")]
    [SerializeField] private float sendThreshold = 0.02f;
    [SerializeField] private float sendInterval = 0.1f;

    [Tooltip("Tranh client vua xoay xong bi snap nguoc lai ngay lap tuc boi gia tri network cu.")]
    [SerializeField] private float ignoreNetworkVisualAfterLocalEdit = 0.15f;

    [Header("Events")]
    [SerializeField] private bool raiseValveClosedEvent = true;
    [SerializeField] private bool raiseValveOpenedEvent = true;
    [SerializeField] private bool raiseOncePerScene = true;

    [Header("Voice Over")]
    [SerializeField] private string valveClosedVoKey = "VO_RightAction";
    [SerializeField] private bool playValveClosedVoOnce = true;

    [Header("Debug")]
    [SerializeField] private float currentAngle;
    [Range(0f, 1f)] [SerializeField] private float valveOpen01;
    [SerializeField] private bool isClosed;
    [SerializeField] private bool fusionSpawned;

    [Networked] private float ValveOpen01Net { get; set; }

    private bool wasClosed;
    private bool hasRaisedCloseInScene;
    private bool hasRaisedOpenInScene;
    private bool hasPlayedValveClosedVoLocal;

    private float lastSentValveOpen01 = -999f;
    private float nextSendTime;
    private float lastAppliedNetworkOpen01 = -999f;
    private float ignoreNetworkVisualUntilTime;

    private void Awake()
    {
        if (!gasSystem)
            gasSystem = FindFirstObjectByType<GasSystem>();
    }

    private void Start()
    {
        currentAngle = ReadCurrentAngle();
        valveOpen01 = ReadValveOpen01();
        isClosed = valveOpen01 <= 0f;
        wasClosed = isClosed;

        // Single-player fallback before Fusion Spawned.
        if (!fusionSpawned && gasSystem != null)
            gasSystem.SetMainValveOpen01(valveOpen01);
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        currentAngle = ReadCurrentAngle();
        valveOpen01 = ReadValveOpen01();
        isClosed = valveOpen01 <= 0f;
        wasClosed = isClosed;

        if (Object.HasStateAuthority)
        {
            ApplyValveOpen01OnHost(valveOpen01, GetLocalActorId(), force: true);
        }
        else
        {
            valveOpen01 = Mathf.Clamp01(ValveOpen01Net);
            ApplyVisualFromOpen01(valveOpen01);
            lastAppliedNetworkOpen01 = valveOpen01;
        }
    }

    private void Update()
    {
        if (!valveHandle || !gasSystem) return;

        // Nếu chưa chạy Fusion, giữ hành vi single-player như code cũ.
        if (!fusionSpawned)
        {
            currentAngle = ReadCurrentAngle();
            valveOpen01 = ReadValveOpen01();
            isClosed = valveOpen01 <= 0f;

            gasSystem.SetMainValveOpen01(valveOpen01);
            RaiseValveEventsIfNeeded(GetLocalActorId(), valveOpen01);

            wasClosed = isClosed;
            return;
        }

        currentAngle = ReadCurrentAngle();
        float localValveOpen01 = ReadValveOpen01();

        if (Object.HasStateAuthority)
        {
            // Host có thể tự xoay van hoặc nhận state từ client qua RPC.
            bool changedEnough = Mathf.Abs(localValveOpen01 - valveOpen01) >= sendThreshold;
            bool intervalReached = Time.time >= nextSendTime;

            if (changedEnough || intervalReached)
            {
                ApplyValveOpen01OnHost(localValveOpen01, GetLocalActorId(), force: false);
                nextSendTime = Time.time + sendInterval;
            }
        }
        else
        {
            // Client xoay local handle -> gửi request lên Host.
            bool changedEnough = Mathf.Abs(localValveOpen01 - lastSentValveOpen01) >= sendThreshold;
            bool intervalReached = Time.time >= nextSendTime;

            if (changedEnough && intervalReached)
            {
                lastSentValveOpen01 = localValveOpen01;
                nextSendTime = Time.time + sendInterval;
                ignoreNetworkVisualUntilTime = Time.time + ignoreNetworkVisualAfterLocalEdit;

                RPC_RequestSetValveOpen01(localValveOpen01);
            }
        }
    }

    public override void Render()
    {
        if (!fusionSpawned) return;
        if (Object.HasStateAuthority) return;
        if (valveHandle == null) return;

        // Nếu client vừa xoay local, đợi một chút để tránh snap giật.
        if (Time.time < ignoreNetworkVisualUntilTime)
            return;

        float netOpen01 = Mathf.Clamp01(ValveOpen01Net);

        if (Mathf.Abs(netOpen01 - lastAppliedNetworkOpen01) < 0.001f)
            return;

        valveOpen01 = netOpen01;
        isClosed = valveOpen01 <= 0f;

        ApplyVisualFromOpen01(valveOpen01);
        lastAppliedNetworkOpen01 = valveOpen01;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetValveOpen01(float requestedOpen01, RpcInfo info = default)
    {
        string actorId = PlayerToActorId(info.Source);
        ApplyValveOpen01OnHost(requestedOpen01, actorId, force: false);
    }

    private void ApplyValveOpen01OnHost(float open01, string actorId, bool force)
    {
        if (fusionSpawned && !Object.HasStateAuthority)
            return;

        open01 = Mathf.Clamp01(open01);

        bool changedEnough = Mathf.Abs(open01 - valveOpen01) >= sendThreshold;
        if (!force && !changedEnough)
            return;

        valveOpen01 = open01;
        isClosed = valveOpen01 <= 0f;

        if (fusionSpawned)
            ValveOpen01Net = valveOpen01;

        // Host cũng update visual, đặc biệt khi client là người xoay.
        ApplyVisualFromOpen01(valveOpen01);

        // Đây là dòng quan trọng nhất:
        // Host set state thật cho GasSystem.
        if (gasSystem != null)
            gasSystem.SetMainValveOpen01(valveOpen01);

        RaiseValveEventsIfNeeded(actorId, valveOpen01);

        wasClosed = isClosed;
    }

    private void RaiseValveEventsIfNeeded(string actorId, float open01)
    {
        bool closedNow = open01 <= 0f;
        bool openedNow = open01 > 0f;

        if (raiseValveClosedEvent && !wasClosed && closedNow)
        {
            bool canRaise = !raiseOncePerScene || !hasRaisedCloseInScene;

            if (canRaise)
            {
                GameplayEventBus.Raise(
                    GameplayEventType.ValveClosed,
                    actorId: actorId,
                    targetId: gameObject.name
                );

                if (raiseOncePerScene)
                    hasRaisedCloseInScene = true;
            }
        }

        if (!wasClosed && closedNow)
            PlayValveClosedVoForEveryone();

        if (raiseValveOpenedEvent && wasClosed && openedNow)
        {
            bool canRaise = !raiseOncePerScene || !hasRaisedOpenInScene;

            if (canRaise)
            {
                GameplayEventBus.Raise(
                    GameplayEventType.ValveOpened,
                    actorId: actorId,
                    targetId: gameObject.name
                );

                if (raiseOncePerScene)
                    hasRaisedOpenInScene = true;
            }
        }
    }

    private void PlayValveClosedVoForEveryone()
    {
        if (fusionSpawned)
        {
            if (Object.HasStateAuthority)
                RPC_PlayValveClosedVo();

            return;
        }

        PlayValveClosedVoLocal();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_PlayValveClosedVo()
    {
        PlayValveClosedVoLocal();
    }

    private void PlayValveClosedVoLocal()
    {
        if ((playValveClosedVoOnce && hasPlayedValveClosedVoLocal) ||
            AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlayOneShot(valveClosedVoKey);
        hasPlayedValveClosedVoLocal = true;
    }

    private float ReadCurrentAngle()
    {
        if (valveHandle == null)
            return currentAngle;

        return GetSignedAxisAngle(valveHandle.localEulerAngles);
    }

    private float ReadValveOpen01()
    {
        float angle = ReadCurrentAngle();
        float open01 = GetOpen01(angle);

        if (Mathf.Abs(Mathf.DeltaAngle(angle, closedAngle)) <= closedDeadZoneDeg)
            open01 = 0f;

        return Mathf.Clamp01(open01);
    }

    private void ApplyVisualFromOpen01(float open01)
    {
        if (valveHandle == null)
            return;

        open01 = Mathf.Clamp01(open01);

        float total = Mathf.DeltaAngle(closedAngle, fullyOpenAngle);
        float angle = closedAngle + total * open01;

        Vector3 euler = valveHandle.localEulerAngles;

        switch (localAxis)
        {
            case Axis.X:
                euler.x = angle;
                break;
            case Axis.Y:
                euler.y = angle;
                break;
            case Axis.Z:
                euler.z = angle;
                break;
        }

        valveHandle.localEulerAngles = euler;
        currentAngle = angle;
    }

    private float GetOpen01(float angle)
    {
        float total = Mathf.DeltaAngle(closedAngle, fullyOpenAngle);
        if (Mathf.Abs(total) < 0.001f) return 0f;

        float current = Mathf.DeltaAngle(closedAngle, angle);
        return Mathf.Clamp01(current / total);
    }

    private float GetSignedAxisAngle(Vector3 euler)
    {
        float angle = localAxis switch
        {
            Axis.X => euler.x,
            Axis.Y => euler.y,
            _ => euler.z
        };

        if (angle > 180f)
            angle -= 360f;

        return angle;
    }

    private string GetLocalActorId()
    {
        if (Runner == null)
            return "Player";

        PlayerRef player = Runner.LocalPlayer;
        if (player == PlayerRef.None)
            return "Host";

        return $"Player_{player.PlayerId}";
    }

    private string PlayerToActorId(PlayerRef player)
    {
        if (player == PlayerRef.None)
            return "Host";

        return $"Player_{player.PlayerId}";
    }
}
