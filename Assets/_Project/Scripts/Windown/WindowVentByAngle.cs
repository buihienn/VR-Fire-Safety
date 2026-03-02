using UnityEngine;

public class WindowVentByAngle : MonoBehaviour
{
    public GasSystem gas;
    public Transform hingePivot;

    public enum Axis { X, Y, Z }
    public Axis axis = Axis.Y;

    public float closedAngle = 0f;
    public float openAngle = 90f;

    void Awake()
    {
        if (!gas) gas = FindFirstObjectByType<GasSystem>();
        
    }

    void Update()
    {
        if (!gas || !hingePivot) return;

        float raw = axis switch
        {
            Axis.X => hingePivot.localEulerAngles.x,
            Axis.Y => hingePivot.localEulerAngles.y,
            _      => hingePivot.localEulerAngles.z,
        };

        float a = Mathf.DeltaAngle(0f, raw); // chống wrap 0/360
        float vent01 = Mathf.InverseLerp(closedAngle, openAngle, a);
        gas.SetVent01(vent01);
    }
}