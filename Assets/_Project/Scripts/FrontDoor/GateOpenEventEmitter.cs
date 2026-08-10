using Fusion;
using Oculus.Interaction;
using UnityEngine;

public class GateOpenEventEmitter : MonoBehaviour
{
    private enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Gate References")]
    [Tooltip("Assign every moving gate leaf that may count as opening the gate.")]
    [SerializeField] private Transform[] gatePivots;
    [Tooltip("Leave empty to find all Grabbable components under this object.")]
    [SerializeField] private Grabbable[] gateGrabbables;

    [Header("Open Detection")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [Min(0f)]
    [SerializeField] private float openThresholdDegrees = 20f;
    [SerializeField] private string gateId = "EntranceGate";

    [Header("Runtime Debug")]
    [SerializeField] private bool isGrabbedLocally;
    [SerializeField] private bool gateOpenedEventRaised;

    private float[] closedAngles;
    private int localGrabCount;

    private void Awake()
    {
        ResolveReferences();
        CacheClosedAngles();
    }

    private void OnEnable()
    {
        ResolveReferences();

        foreach (Grabbable grabbable in gateGrabbables)
        {
            if (grabbable != null)
                grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void OnDisable()
    {
        foreach (Grabbable grabbable in gateGrabbables)
        {
            if (grabbable != null)
                grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        localGrabCount = 0;
        isGrabbedLocally = false;
    }

    private void Update()
    {
        if (gateOpenedEventRaised || !isGrabbedLocally || !IsGateOpen())
            return;

        gateOpenedEventRaised = true;

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        GameplayEventBus.Raise(
            GameplayEventType.GateOpened,
            actorId: GameplayEventActorId.FromRunner(runner),
            targetId: string.IsNullOrWhiteSpace(gateId) ? gameObject.name : gateId);
    }

    private void HandlePointerEvent(PointerEvent pointerEvent)
    {
        if (pointerEvent.Type == PointerEventType.Select)
            localGrabCount++;
        else if (pointerEvent.Type == PointerEventType.Unselect)
            localGrabCount = Mathf.Max(0, localGrabCount - 1);

        isGrabbedLocally = localGrabCount > 0;
    }

    private bool IsGateOpen()
    {
        if (gatePivots == null || closedAngles == null)
            return false;

        int count = Mathf.Min(gatePivots.Length, closedAngles.Length);
        for (int i = 0; i < count; i++)
        {
            Transform pivot = gatePivots[i];
            if (pivot == null)
                continue;

            float delta = Mathf.Abs(Mathf.DeltaAngle(closedAngles[i], ReadAngle(pivot)));
            if (delta >= openThresholdDegrees)
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (gateGrabbables == null || gateGrabbables.Length == 0)
            gateGrabbables = GetComponentsInChildren<Grabbable>(true);
    }

    private void CacheClosedAngles()
    {
        if (gatePivots == null)
        {
            closedAngles = new float[0];
            return;
        }

        closedAngles = new float[gatePivots.Length];
        for (int i = 0; i < gatePivots.Length; i++)
            closedAngles[i] = gatePivots[i] != null ? ReadAngle(gatePivots[i]) : 0f;
    }

    private float ReadAngle(Transform target)
    {
        Vector3 euler = target.localEulerAngles;
        return rotationAxis switch
        {
            RotationAxis.X => euler.x,
            RotationAxis.Y => euler.y,
            _ => euler.z
        };
    }
}
