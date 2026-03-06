using UnityEngine;

public class LatchController : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("References")]
    public Transform handlePivot;
    public Transform latch;

    [Header("Handle Rotation Settings")]
    public Axis handleAxis = Axis.Z;
    public float handleClosedAngle = 0f;
    public float handleOpenAngle = -60f;

    [Header("Latch Movement Settings")]
    public Axis latchMoveAxis = Axis.X;
    public float latchMaxDistance = 0.03f;

    private Vector3 _latchStartLocalPos;

    void Start()
    {
        _latchStartLocalPos = latch.localPosition;
    }

    void Update()
    {
        if (handlePivot == null || latch == null) return;

        float handleAngle = Normalize(ReadHandleAxis());

        // Convert angle -> 0..1
        float t = Mathf.InverseLerp(handleClosedAngle, handleOpenAngle, handleAngle);
        t = Mathf.Clamp01(t);

        Vector3 newPos = _latchStartLocalPos;

        switch (latchMoveAxis)
        {
            case Axis.X:
                newPos.x += latchMaxDistance * t;
                break;

            case Axis.Y:
                newPos.y += latchMaxDistance * t;
                break;

            case Axis.Z:
                newPos.z += latchMaxDistance * t;
                break;
        }

        latch.localPosition = newPos;
    }

    float ReadHandleAxis()
    {
        Vector3 e = handlePivot.localEulerAngles;

        switch (handleAxis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default: return e.z;
        }
    }

    float Normalize(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}