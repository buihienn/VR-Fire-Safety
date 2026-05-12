using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Manages lock/unlock and physics state for the right window.
/// - Locked by default, unlocks after handle rotation + door angle threshold.
/// - While the handle is grabbed, physics is disabled to avoid conflicts.
/// </summary>
public class RightWindowPhysicsLock : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    public Transform doorPivot;
    public Transform handlePivot;
    public Grabbable handleGrabbable;
    public HingeJoint doorHingeJoint;
    public Rigidbody doorRigidbody;

    [Header("Unlock By Handle")]
    public Axis handleAxis = Axis.Z;
    public bool handleDownIsNegative = true;
    public float handleUnlockAtDeg = 30f;

    [Header("Unlock By Door")]
    public Axis doorAxis = Axis.Y;
    public float doorUnlockAtDeg = 5f;
    public bool relockWhenClosed = false;

    private bool _isGrabbed;
    private bool _isUnlocked;

    private void OnEnable()
    {
        if (handleGrabbable != null)
            handleGrabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        if (handleGrabbable != null)
            handleGrabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void Update()
    {
        float doorDelta = Mathf.Abs(NormalizeAngle(ReadDoorAxisAngle()));

        if (!_isUnlocked && HandleUnlocked() && doorDelta >= doorUnlockAtDeg)
            _isUnlocked = true;

        if (relockWhenClosed && _isUnlocked && !_isGrabbed && doorDelta <= 0.5f)
            _isUnlocked = false;

        SyncPhysicsState();
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
            _isGrabbed = true;
        else if (evt.Type == PointerEventType.Unselect)
            _isGrabbed = false;
    }

    private void SyncPhysicsState()
    {
        Rigidbody rb = doorRigidbody;
        if (rb == null && doorHingeJoint != null)
            rb = doorHingeJoint.GetComponent<Rigidbody>();
        if (rb == null) return;

        bool allowPhysics = _isUnlocked && !_isGrabbed;
        bool shouldBeKinematic = !allowPhysics;
        if (rb.isKinematic != shouldBeKinematic)
            rb.isKinematic = shouldBeKinematic;
    }

    private bool HandleUnlocked()
    {
        if (handlePivot == null) return false;
        float raw = NormalizeAngle(ReadHandleAxisAngle());
        float mag = handleDownIsNegative ? -raw : raw;
        return mag >= handleUnlockAtDeg;
    }

    private float ReadHandleAxisAngle()
    {
        Vector3 e = handlePivot.localEulerAngles;
        switch (handleAxis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default: return e.z;
        }
    }

    private float ReadDoorAxisAngle()
    {
        if (doorPivot == null) return 0f;
        Vector3 e = doorPivot.localEulerAngles;
        switch (doorAxis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default: return e.z;
        }
    }

    private float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }
}
