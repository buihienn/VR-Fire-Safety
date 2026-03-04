using UnityEngine;

public class DoorLeftOpenByHandle2 : MonoBehaviour
{
     [Header("Refs")]
    public HingeJoint doorHinge;
    public Transform handlePivot;    

    [Header("Handle Angle")]
    public float unlockAtDeg = 30f;
    public bool handleDownIsNegative = true; 
    public enum Axis { X, Y, Z }
    public Axis handleAxis = Axis.Z;

    [Header("Door Limits")]
    public float lockedAngle = 0f;  
    public float openAngle = 170f;   

    public bool debug = false;

    bool _unlocked;

    void Reset()
    {
        doorHinge = GetComponent<HingeJoint>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (doorHinge == null || handlePivot == null) return;

        bool shouldUnlock = IsHandleUnlocked();

        if (shouldUnlock != _unlocked)
        {
            _unlocked = shouldUnlock;
            ApplyLimits(_unlocked);

            if (debug) Debug.Log($"[DoorLatch] unlocked={_unlocked}");
        }
    }

    bool IsHandleUnlocked()
    {
        float raw = NormalizeAngle(ReadHandleAngle());
        float mag = handleDownIsNegative ? -raw : raw;
        return mag >= unlockAtDeg;
    }

    float ReadHandleAngle()
    {
        Vector3 e = handlePivot.localEulerAngles;
        switch (handleAxis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default:     return e.z;
        }
    }

    void ApplyLimits(bool unlocked)
    {
        doorHinge.useLimits = true;

        JointLimits limits = doorHinge.limits;

        if (!unlocked)
        {
            limits.min = lockedAngle;
            limits.max = lockedAngle;
        }
        else
        {
            float min = Mathf.Min(lockedAngle, openAngle);
            float max = Mathf.Max(lockedAngle, openAngle);
            limits.min = min;
            limits.max = max;
        }

        doorHinge.limits = limits;
    }

    float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }
}
