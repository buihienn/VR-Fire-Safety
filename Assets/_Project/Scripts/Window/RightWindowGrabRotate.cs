using UnityEngine;

/// <summary>
/// Xoay cua so theo vi tri tay khi user grab handle.
/// Chi xoay trong khoang [closedAngle, openAngle] va tat physics khi grab.
/// </summary>
public class RightWindowGrabRotate : MonoBehaviour
{
    public enum DoorAxis { LocalX, LocalY, LocalZ }

    [Header("Refs")]
    public Transform doorHingePivot;
    public Rigidbody doorRigidbody;
    public HingeJoint doorHingeJoint;

    [Header("Door Rotation")]
    public DoorAxis hingeAxis = DoorAxis.LocalY;
    public float closedAngle = 0f;
    public float openAngle = 170f;
    public bool invertDoorDirection = false;

    [Header("Smoothing")]
    public float doorSmoothTime = 0.06f;
    public float maxDoorSpeed = 999f;

    private Vector3 _handPosition;
    private bool _isGrabbed;
    private float _doorAngle;
    private float _doorVel;
    private float _handYawStart;
    private float _doorAngleStart;

    private void Start()
    {
        _doorAngle = closedAngle;
        ApplyDoorRotation(_doorAngle);
        SetPhysicsEnabled(false);
    }

    private void Update()
    {
        if (!_isGrabbed) return;

        float handYawNow = GetHandYawAroundHinge();
        float deltaYaw = Mathf.DeltaAngle(_handYawStart, handYawNow);
        if (invertDoorDirection) deltaYaw = -deltaYaw;

        float targetAngle = _doorAngleStart + deltaYaw;
        float min = Mathf.Min(closedAngle, openAngle);
        float max = Mathf.Max(closedAngle, openAngle);
        float clamped = Mathf.Clamp(targetAngle, min, max);

        _doorAngle = Mathf.SmoothDampAngle(_doorAngle, clamped, ref _doorVel, doorSmoothTime, maxDoorSpeed);
        ApplyDoorRotation(_doorAngle);

        SetPhysicsEnabled(false);
    }

    private void SetPhysicsEnabled(bool enabled)
    {
        Rigidbody rb = doorRigidbody;
        if (rb == null && doorHingeJoint != null)
            rb = doorHingeJoint.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.isKinematic = !enabled;
    }

    private void ApplyDoorRotation(float angle)
    {
        if (doorHingePivot == null) return;

        switch (hingeAxis)
        {
            case DoorAxis.LocalX:
                doorHingePivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
                break;
            case DoorAxis.LocalY:
                doorHingePivot.localRotation = Quaternion.Euler(0f, angle, 0f);
                break;
            case DoorAxis.LocalZ:
                doorHingePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                break;
        }
    }

    private float GetHandYawAroundHinge()
    {
        if (doorHingePivot == null) return 0f;

        Vector3 v = _handPosition - doorHingePivot.position;
        v.y = 0f;
        if (v.sqrMagnitude < 0.000001f) return 0f;
        v.Normalize();

        Vector3 f = doorHingePivot.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.000001f)
            f = Vector3.forward;
        else
            f.Normalize();

        return Vector3.SignedAngle(f, v, Vector3.up);
    }

    public void UpdateHandPosition(Vector3 worldPos)
    {
        _handPosition = worldPos;
    }

    public void OnHandleGrabbed(Vector3 handWorldPos)
    {
        _isGrabbed = true;
        SetPhysicsEnabled(false);

        _doorAngleStart = _doorAngle;
        _handPosition = handWorldPos;
        _handYawStart = GetHandYawAroundHinge();
    }

    public void OnHandleReleased()
    {
        _isGrabbed = false;
    }
}
