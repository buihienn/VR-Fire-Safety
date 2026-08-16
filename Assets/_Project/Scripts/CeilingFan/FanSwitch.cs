using Fusion;
using Oculus.Interaction;
using UnityEngine;

[RequireComponent(typeof(Grabbable))]
public class FanSwitch : NetworkBehaviour
{
    private const int Steps = 6;
    private const float StepAngle = 60f;

    [Header("Rotation Settings")]
    [SerializeField] private Vector3 localAxis = Vector3.up;

    [Header("Fan Output")]
    [SerializeField] private FanRotator fanRotator;
    [SerializeField] private float[] stepSpeeds = { 0f, 100f, 200f, 300f, 400f, 500f };

    [Header("Ignition")]
    [SerializeField] private SparkIgnitionTrigger sparkIgnitionTrigger;

    [Header("Gas Rule")]
    [Tooltip("Quạt chỉ được phép chạy khi gas level không vượt quá giá trị này.")]
    [Range(0, 3)]
    [SerializeField] private int maximumOperatingGasLevel = 1;

    [Header("Debug")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool isGrabbed;
    [SerializeField] private int currentStepIndex;

    [Networked, OnChangedRender(nameof(OnStepNetworkChanged))]
    private int CurrentStepNet { get; set; }

    private Grabbable grabbable;
    private Quaternion initialLocalRotation;
    private int lastRequestedStep = -1;
    private bool unsafeOperationRaisedThisGrab;

    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        initialLocalRotation = transform.localRotation;

        currentStepIndex = 0;
        lastRequestedStep = 0;
        ApplyFanOutput(0);
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
            CurrentStepNet = 0;

        ApplyAcceptedStep(CurrentStepNet, snapKnob: true);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentStepNet <= 0) return;
        if (CanOperateAtCurrentGasLevel()) return;

        SetStepOnStateAuthority(0);
    }

    private void OnEnable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void Update()
    {
        if (fusionSpawned) return;
        if (currentStepIndex <= 0) return;
        if (CanOperateAtCurrentGasLevel()) return;

        ApplyAcceptedStep(0, snapKnob: !isGrabbed);
    }

    private void LateUpdate()
    {
        if (isGrabbed)
            UpdateStepFromRotation();
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                isGrabbed = true;
                lastRequestedStep = GetAcceptedStep();
                unsafeOperationRaisedThisGrab = false;
                break;

            case PointerEventType.Unselect:
                isGrabbed = false;
                lastRequestedStep = GetAcceptedStep();
                unsafeOperationRaisedThisGrab = false;
                SnapKnobToStep(lastRequestedStep);
                break;
        }
    }

    private void UpdateStepFromRotation()
    {
        Vector3 axis = localAxis.normalized;
        if (axis == Vector3.zero) return;

        Vector3 refDir = Vector3.Cross(axis, Vector3.up);
        if (refDir.sqrMagnitude < 1e-4f)
            refDir = Vector3.Cross(axis, Vector3.right);

        refDir.Normalize();

        Quaternion currentLocalRotation = transform.localRotation;
        Vector3 rotationAxis = initialLocalRotation * axis;
        Vector3 refStart = initialLocalRotation * refDir;
        Vector3 refNow = currentLocalRotation * refDir;

        float angle = Vector3.SignedAngle(refStart, refNow, rotationAxis);
        int requestedStep = GetStepIndexFromAngle(angle);

        if (requestedStep == lastRequestedStep)
            return;

        int gasLevel = GasSystem.Instance != null
            ? GasSystem.Instance.GasLevel()
            : 0;

        if (gasLevel >= 1 && !unsafeOperationRaisedThisGrab)
        {
            GameplayEventBus.Raise(
                GameplayEventType.FanControlOperated,
                actorId: GameplayEventActorId.FromRunner(Runner),
                targetId: gameObject.name,
                payload: gasLevel);

            unsafeOperationRaisedThisGrab = true;
        }

        bool attemptingToTurnOn = lastRequestedStep <= 0 && requestedStep > 0;
        if (attemptingToTurnOn)
        {
            if (sparkIgnitionTrigger != null)
                sparkIgnitionTrigger.TriggerSpark();
        }

        lastRequestedStep = requestedStep;
        RequestSetStep(requestedStep);
    }

    private int GetStepIndexFromAngle(float angle)
    {
        float wrapped = Mathf.Repeat(angle + 360f, 360f);
        if (wrapped <= 29f) return 0;
        if (wrapped <= 89f) return 1;
        if (wrapped <= 149f) return 2;
        if (wrapped <= 209f) return 3;
        if (wrapped <= 269f) return 4;
        return 5;
    }

    private void RequestSetStep(int requestedStep)
    {
        requestedStep = Mathf.Clamp(requestedStep, 0, Steps - 1);

        if (!fusionSpawned)
        {
            int acceptedStep = CanAcceptStep(requestedStep) ? requestedStep : 0;
            ApplyAcceptedStep(acceptedStep, snapKnob: false);
            return;
        }

        if (Object.HasStateAuthority)
            SetStepOnStateAuthority(requestedStep);
        else
            RPC_RequestSetStep(requestedStep);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    private void RPC_RequestSetStep(int requestedStep)
    {
        SetStepOnStateAuthority(requestedStep);
    }

    private void SetStepOnStateAuthority(int requestedStep)
    {
        if (!Object.HasStateAuthority)
            return;

        requestedStep = Mathf.Clamp(requestedStep, 0, Steps - 1);
        int acceptedStep = CanAcceptStep(requestedStep) ? requestedStep : 0;

        CurrentStepNet = acceptedStep;
        ApplyAcceptedStep(acceptedStep, snapKnob: !isGrabbed);
        RPC_ApplyAcceptedStep(acceptedStep);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_ApplyAcceptedStep(int acceptedStep)
    {
        ApplyAcceptedStep(acceptedStep, snapKnob: !isGrabbed);
    }

    private void OnStepNetworkChanged()
    {
        ApplyAcceptedStep(CurrentStepNet, snapKnob: !isGrabbed);
    }

    private bool CanAcceptStep(int requestedStep)
    {
        return requestedStep <= 0 || CanOperateAtCurrentGasLevel();
    }

    private bool CanOperateAtCurrentGasLevel()
    {
        return GasSystem.Instance != null &&
               GasSystem.Instance.GasLevel() <= maximumOperatingGasLevel;
    }

    private int GetAcceptedStep()
    {
        return fusionSpawned ? CurrentStepNet : currentStepIndex;
    }

    private void ApplyAcceptedStep(int stepIndex, bool snapKnob)
    {
        stepIndex = Mathf.Clamp(stepIndex, 0, Steps - 1);
        currentStepIndex = stepIndex;

        ApplyFanOutput(stepIndex);

        if (snapKnob)
            SnapKnobToStep(stepIndex);
    }

    private void ApplyFanOutput(int stepIndex)
    {
        if (fanRotator == null) return;

        float speed = GetStepSpeed(stepIndex);
        fanRotator.speed = speed;
        fanRotator.SetOn(speed > 0f);
    }

    private void SnapKnobToStep(int stepIndex)
    {
        Vector3 axis = localAxis.normalized;
        if (axis == Vector3.zero) return;

        transform.localRotation =
            initialLocalRotation *
            Quaternion.AngleAxis(stepIndex * StepAngle, axis);
    }

    private float GetStepSpeed(int stepIndex)
    {
        if (stepSpeeds == null || stepSpeeds.Length == 0)
            return 0f;

        int clamped = Mathf.Clamp(stepIndex, 0, stepSpeeds.Length - 1);
        return stepSpeeds[clamped];
    }
}
