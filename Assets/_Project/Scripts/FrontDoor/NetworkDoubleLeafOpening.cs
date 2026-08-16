using Fusion;
using UnityEngine;

/// <summary>
/// Authoritative state machine for a two-leaf door or window.
/// This is the only component that is allowed to rotate the two leaf pivots.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkDoubleLeafOpening : NetworkBehaviour
{
    public enum RotationAxis { X, Y, Z }
    public enum OpeningState : byte { Closed, Opening, Open }

    [Header("Leaf Pivots")]
    [SerializeField] private Transform leftPivot;
    [SerializeField] private Transform rightPivot;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [SerializeField] private float leftOpenAngle = -100f;
    [SerializeField] private float rightOpenAngle = 100f;
    [Min(0.05f)] [SerializeField] private float openingDuration = 0.8f;

    [Header("Handle Command")]
    [SerializeField] private HandleOpeningCommandSource handleCommandSource;
    [Range(0f, 180f)] [SerializeField] private float minimumAcceptedHandleAngle = 89f;

    [Header("Gas System Integration")]
    [SerializeField] private bool reportOpeningToGasSystem = true;
    [SerializeField] private GasSystem gasSystem;
    [Range(0, 3)] [SerializeField] private int networkSlot;
    [SerializeField] private bool isWindow = true;
    [Min(0.01f)] [SerializeField] private float gasFullOpenAngle = 100f;
    [Range(0.001f, 1f)] [SerializeField] private float gasReportThreshold01 = 0.05f;
    [Min(0.02f)] [SerializeField] private float gasReportInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private OpeningState currentState;
    [Range(0f, 1f)] [SerializeField] private float currentOpen01;

    [Networked, OnChangedRender(nameof(OnNetworkOpeningChanged))]
    private OpeningState StateNet { get; set; }

    [Networked, OnChangedRender(nameof(OnNetworkOpeningChanged))]
    private float Open01Net { get; set; }

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;
    private OpeningState localState;
    private float localOpen01;
    private bool gasOpeningRegistered;
    private float lastReportedGasOpen01 = float.NegativeInfinity;
    private float nextGasReportTime;
    private string openingActorId = "Player";

    public OpeningState State => fusionSpawned ? StateNet : localState;
    public float Open01 => fusionSpawned ? Mathf.Clamp01(Open01Net) : localOpen01;

    private void Awake()
    {
        if (leftPivot != null)
            leftClosedRotation = leftPivot.localRotation;

        if (rightPivot != null)
            rightClosedRotation = rightPivot.localRotation;

        localState = OpeningState.Closed;
        localOpen01 = 0f;
        UpdateDebugState(localState, localOpen01);
    }

    private void Start()
    {
        EnsureGasOpeningRegistered();

        if (!fusionSpawned)
        {
            ApplyPose(localOpen01);
            TryReportGasOpening(localOpen01, force: true);
        }
    }

    public override void Spawned()
    {
        fusionSpawned = true;
        EnsureGasOpeningRegistered();

        float open01 = Mathf.Clamp01(Open01Net);
        ApplyAcceptedState(StateNet, open01);

        if (Object.HasStateAuthority)
            TryReportGasOpening(open01, force: true);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
    }

    private void Update()
    {
        if (fusionSpawned || localState != OpeningState.Opening)
            return;

        localOpen01 = Mathf.MoveTowards(
            localOpen01,
            1f,
            Time.deltaTime / Mathf.Max(0.05f, openingDuration));

        if (localOpen01 >= 1f)
            localState = OpeningState.Open;

        ApplyAcceptedState(localState, localOpen01);
        TryReportGasOpening(localOpen01, force: localState == OpeningState.Open);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || StateNet != OpeningState.Opening)
            return;

        float nextOpen01 = Mathf.MoveTowards(
            Open01Net,
            1f,
            Runner.DeltaTime / Mathf.Max(0.05f, openingDuration));

        Open01Net = nextOpen01;

        if (nextOpen01 >= 1f)
            StateNet = OpeningState.Open;

        ApplyAcceptedState(StateNet, nextOpen01);
        TryReportGasOpening(nextOpen01, force: StateNet == OpeningState.Open);
    }

    public override void Render()
    {
        if (!fusionSpawned)
            return;

        ApplyAcceptedState(StateNet, Mathf.Clamp01(Open01Net));
    }

    /// <summary>
    /// Called by the local handle after it reaches the configured trigger angle.
    /// </summary>
    public void RequestOpen(float reportedHandleAngle)
    {
        reportedHandleAngle = Mathf.Abs(reportedHandleAngle);
        if (reportedHandleAngle + 0.01f < minimumAcceptedHandleAngle)
            return;

        if (!fusionSpawned)
        {
            BeginOpeningLocal("LocalPlayer");
            return;
        }

        if (Object.HasStateAuthority)
        {
            BeginOpeningOnStateAuthority(
                reportedHandleAngle,
                GameplayEventActorId.FromRunner(Runner));
        }
        else
            RPC_RequestOpen(reportedHandleAngle);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void RPC_RequestOpen(float reportedHandleAngle, RpcInfo info = default)
    {
        BeginOpeningOnStateAuthority(
            Mathf.Abs(reportedHandleAngle),
            GameplayEventActorId.FromPlayerRef(info.Source));
    }

    private void BeginOpeningOnStateAuthority(
        float reportedHandleAngle,
        string actorId)
    {
        if (!Object.HasStateAuthority || StateNet != OpeningState.Closed)
            return;

        if (reportedHandleAngle + 0.01f < minimumAcceptedHandleAngle)
            return;

        openingActorId = NormalizeActorId(actorId);
        StateNet = OpeningState.Opening;
        Open01Net = Mathf.Clamp01(Open01Net);
        ApplyAcceptedState(StateNet, Open01Net);
    }

    private void BeginOpeningLocal(string actorId)
    {
        if (localState != OpeningState.Closed)
            return;

        openingActorId = NormalizeActorId(actorId);
        localState = OpeningState.Opening;
        ApplyAcceptedState(localState, localOpen01);
    }

    private void OnNetworkOpeningChanged()
    {
        ApplyAcceptedState(StateNet, Mathf.Clamp01(Open01Net));
    }

    private void ApplyAcceptedState(OpeningState state, float open01)
    {
        open01 = Mathf.Clamp01(open01);
        ApplyPose(open01);
        UpdateDebugState(state, open01);

        if (state != OpeningState.Closed && handleCommandSource != null)
            handleCommandSource.LockInteractionAfterTrigger();
    }

    private void ApplyPose(float open01)
    {
        float easedOpen01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(open01));
        Vector3 axis = GetLocalAxis();

        if (leftPivot != null)
        {
            leftPivot.localRotation = leftClosedRotation *
                                      Quaternion.AngleAxis(leftOpenAngle * easedOpen01, axis);
        }

        if (rightPivot != null)
        {
            rightPivot.localRotation = rightClosedRotation *
                                       Quaternion.AngleAxis(rightOpenAngle * easedOpen01, axis);
        }
    }

    private Vector3 GetLocalAxis()
    {
        return rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward
        };
    }

    private void EnsureGasOpeningRegistered()
    {
        if (!reportOpeningToGasSystem || gasOpeningRegistered)
            return;

        if (gasSystem == null)
            gasSystem = GasSystem.Instance != null
                ? GasSystem.Instance
                : FindFirstObjectByType<GasSystem>();

        if (gasSystem == null)
            return;

        gasSystem.RegisterOpening(
            networkSlot,
            isWindow,
            Open01 * gasFullOpenAngle);

        gasOpeningRegistered = true;
    }

    private void TryReportGasOpening(float open01, bool force)
    {
        if (!reportOpeningToGasSystem)
            return;

        if (fusionSpawned && (Object == null || !Object.HasStateAuthority))
            return;

        EnsureGasOpeningRegistered();
        if (gasSystem == null)
            return;

        open01 = Mathf.Clamp01(open01);
        bool changedEnough = Mathf.Abs(open01 - lastReportedGasOpen01) >= gasReportThreshold01;
        bool intervalReached = Time.unscaledTime >= nextGasReportTime;

        if (!force && (!changedEnough || !intervalReached))
            return;

        gasSystem.SetOpeningAngle(
            networkSlot,
            open01 * gasFullOpenAngle,
            isWindow,
            openingActorId);

        lastReportedGasOpen01 = open01;
        nextGasReportTime = Time.unscaledTime + gasReportInterval;
    }

    private static string NormalizeActorId(string actorId)
    {
        return string.IsNullOrWhiteSpace(actorId)
            ? "Player"
            : actorId.Trim();
    }

    private void UpdateDebugState(OpeningState state, float open01)
    {
        currentState = state;
        currentOpen01 = Mathf.Clamp01(open01);
    }

    private void OnValidate()
    {
        openingDuration = Mathf.Max(0.05f, openingDuration);
        minimumAcceptedHandleAngle = Mathf.Clamp(minimumAcceptedHandleAngle, 0f, 180f);
        networkSlot = Mathf.Clamp(networkSlot, 0, 3);
        gasFullOpenAngle = Mathf.Max(0.01f, gasFullOpenAngle);
        gasReportThreshold01 = Mathf.Clamp(gasReportThreshold01, 0.001f, 1f);
        gasReportInterval = Mathf.Max(0.02f, gasReportInterval);
    }
}
