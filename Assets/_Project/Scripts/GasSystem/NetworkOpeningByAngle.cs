using UnityEngine;

public class NetworkOpeningByAngle : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Opening")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Axis localAxis = Axis.Y;
    [Range(0, 3)] [SerializeField] private int networkSlot;
    [SerializeField] private bool isWindow = true;

    [Header("Synchronization")]
    [SerializeField] private float sendThresholdDegrees = 0.25f;
    [SerializeField] private float remoteSmoothTime = 0.06f;
    [SerializeField] private float localControlHoldSeconds = 0.2f;

    private GasSystem gasSystem;
    private float lastObservedAngle;
    private float lastSentAngle;
    private float smoothVelocity;
    private float localControlUntil;
    private bool registered;
    private bool appliedNetworkLastFrame;

    private void Reset()
    {
        pivot = transform;
    }

    private void Awake()
    {
        if (pivot == null)
            pivot = transform;

        lastObservedAngle = ReadAngle();
        lastSentAngle = lastObservedAngle;
    }

    private void LateUpdate()
    {
        if (gasSystem == null)
            gasSystem = GasSystem.Instance;

        if (gasSystem == null || pivot == null)
            return;

        if (!registered)
        {
            gasSystem.RegisterOpening(networkSlot, isWindow, ReadAngle());
            registered = true;
            lastObservedAngle = ReadAngle();
            lastSentAngle = lastObservedAngle;
        }

        float currentAngle = ReadAngle();
        float localDelta = Mathf.Abs(Mathf.DeltaAngle(lastObservedAngle, currentAngle));

        if (!appliedNetworkLastFrame && localDelta >= sendThresholdDegrees)
        {
            localControlUntil = Time.unscaledTime + localControlHoldSeconds;

            if (Mathf.Abs(Mathf.DeltaAngle(lastSentAngle, currentAngle)) >= sendThresholdDegrees)
            {
                gasSystem.SetOpeningAngle(networkSlot, currentAngle, isWindow);
                lastSentAngle = currentAngle;
            }
        }

        appliedNetworkLastFrame = false;

        if (Time.unscaledTime >= localControlUntil)
        {
            float targetAngle = gasSystem.GetOpeningAngle(networkSlot);
            float smoothedAngle = Mathf.SmoothDampAngle(
                currentAngle,
                targetAngle,
                ref smoothVelocity,
                Mathf.Max(0.001f, remoteSmoothTime));

            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, smoothedAngle)) > 0.001f)
            {
                ApplyAngle(smoothedAngle);
                currentAngle = smoothedAngle;
                appliedNetworkLastFrame = true;
            }
        }

        lastObservedAngle = currentAngle;
    }

    private float ReadAngle()
    {
        Vector3 euler = pivot.localEulerAngles;
        float angle = localAxis switch
        {
            Axis.X => euler.x,
            Axis.Y => euler.y,
            _ => euler.z
        };

        return Mathf.DeltaAngle(0f, angle);
    }

    private void ApplyAngle(float angle)
    {
        Vector3 euler = pivot.localEulerAngles;

        switch (localAxis)
        {
            case Axis.X:
                euler.x = angle;
                break;
            case Axis.Y:
                euler.y = angle;
                break;
            default:
                euler.z = angle;
                break;
        }

        pivot.localRotation = Quaternion.Euler(euler);
    }

    private void OnValidate()
    {
        networkSlot = Mathf.Clamp(networkSlot, 0, 3);
        sendThresholdDegrees = Mathf.Max(0.01f, sendThresholdDegrees);
        remoteSmoothTime = Mathf.Max(0.001f, remoteSmoothTime);
        localControlHoldSeconds = Mathf.Max(0f, localControlHoldSeconds);
    }
}
