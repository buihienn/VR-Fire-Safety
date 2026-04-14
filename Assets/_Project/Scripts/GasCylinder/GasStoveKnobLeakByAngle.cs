using UnityEngine;

public class GasStoveKnobLeakByAngle : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Knob Read")]
    [SerializeField] private Axis localAxis = Axis.Y;

    [Tooltip("Goc OFF / dong kin")]
    [SerializeField] private float offAngle = 0f;

    [Tooltip("Lech trong khoang nay thi xem nhu da tat kin")]
    [SerializeField] private float offToleranceDeg = 5f;

    [Header("Debug")]
    [SerializeField] private float currentAngle;
    [SerializeField] private bool isLeaking;

    public bool IsLeaking => isLeaking;

    private void Update()
    {
        currentAngle = GetSignedAxisAngle(transform.localEulerAngles);

        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, offAngle));
        isLeaking = delta > offToleranceDeg;
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