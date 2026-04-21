using UnityEngine;

public class GasVentByAngle : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Read From This Pivot")]
    public Transform pivot;
    public Axis localAxis = Axis.Y;

    [Header("Angle Setup")]
    public float closedAngle = 0f;

    [Tooltip("Mo qua goc nay thi bat dau tinh la dang thong gio")]
    public float activeAtAngle = 30f;

    [Tooltip("Goc nay xem nhu mo het")]
    public float fullOpenAngle = 100f;

    [Tooltip("Neu cua mo theo chieu am, cu de true de lay tri tuyet doi")]
    public bool useAbsoluteDelta = true;

    private void Reset()
    {
        pivot = transform;
    }

    public float GetOpen01()
    {
        if (!pivot) return 0f;

        float current = GetAxisLocalEuler(pivot.localEulerAngles);
        float delta = Mathf.DeltaAngle(closedAngle, current);

        if (useAbsoluteDelta)
            delta = Mathf.Abs(delta);

        return Mathf.InverseLerp(activeAtAngle, fullOpenAngle, delta);
    }

    public bool IsOpenEnough()
    {
        if (!pivot) return false;

        float current = GetAxisLocalEuler(pivot.localEulerAngles);
        float delta = Mathf.DeltaAngle(closedAngle, current);

        if (useAbsoluteDelta)
            delta = Mathf.Abs(delta);

        return delta >= activeAtAngle;
    }

    private float GetAxisLocalEuler(Vector3 euler)
    {
        return localAxis switch
        {
            Axis.X => euler.x,
            Axis.Y => euler.y,
            _ => euler.z
        };
    }
}