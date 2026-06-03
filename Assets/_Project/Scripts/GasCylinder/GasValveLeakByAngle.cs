using UnityEngine;

public class GasValveLeakByAngle : MonoBehaviour
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

    [Header("Events")]
    [SerializeField] private bool raiseValveClosedEvent = true;
    [SerializeField] private bool raiseOncePerScene = true;
    [SerializeField] private string actorId = "Player";

    [Header("Debug")]
    [SerializeField] private float currentAngle;
    [Range(0f, 1f)] [SerializeField] private float valveOpen01;

    private bool wasClosed;
    private static bool hasRaisedInScene;

    private void Awake()
    {
        if (!gasSystem)
            gasSystem = FindFirstObjectByType<GasSystem>();
    }

    private void Start()
    {
        if (valveHandle)
        {
            currentAngle = GetSignedAxisAngle(valveHandle.localEulerAngles);
            valveOpen01 = GetOpen01(currentAngle);

            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, closedAngle)) <= closedDeadZoneDeg)
                valveOpen01 = 0f;

            wasClosed = valveOpen01 <= 0f;
        }
    }

    private void Update()
    {
        if (!valveHandle || !gasSystem) return;

        currentAngle = GetSignedAxisAngle(valveHandle.localEulerAngles);
        valveOpen01 = GetOpen01(currentAngle);

        if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, closedAngle)) <= closedDeadZoneDeg)
            valveOpen01 = 0f;

        gasSystem.SetMainValveOpen01(valveOpen01);

        bool isClosed = valveOpen01 <= 0f;
        bool canRaise = raiseValveClosedEvent && !wasClosed && isClosed;
        if (raiseOncePerScene && hasRaisedInScene)
            canRaise = false;

        if (canRaise)
        {
            GameplayEventBus.Raise(
                GameplayEventType.ValveClosed,
                actorId: actorId,
                targetId: gameObject.name);

            if (raiseOncePerScene)
                hasRaisedInScene = true;
        }

        wasClosed = isClosed;
    }

    private float GetOpen01(float currentAngle)
    {
        float total = Mathf.DeltaAngle(closedAngle, fullyOpenAngle);
        if (Mathf.Abs(total) < 0.001f) return 0f;

        float current = Mathf.DeltaAngle(closedAngle, currentAngle);
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
}