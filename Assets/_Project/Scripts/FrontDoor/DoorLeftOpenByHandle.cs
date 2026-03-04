using UnityEngine;

public class DoorLeftOpenByHandle : MonoBehaviour
{
    public enum DoorAxis { LocalX, LocalY, LocalZ }

    public enum HandleAxis { X, Y, Z }

    [Header("Refs")]
    public Transform doorHingePivot;
    public Transform handlePivot;
    public Transform handTransform;

    [Header("Handle Rotation Reading")]
    public HandleAxis handleAxis = HandleAxis.Z;

    public bool handleDownIsNegative = true;

    [Header("Unlock by Handle Angle")]
    public float unlockAtDeg = 30f;

    [Header("Door Rotation")]
    public DoorAxis hingeAxis = DoorAxis.LocalY;

    public float closedAngle = 0f;

    public float openAngle = 170f;

    [Header("Optional Tuning")]
    public bool invertDoorDirection = false;

    public bool debugLogs = false;

    [Header("Smoothing")]
    public float doorSmoothTime = 0.06f;
    public float maxDoorSpeed = 999f;

    private float _doorVel; // velocity cho SmoothDampAngle

    // Trạng thái grab
    private bool _isGrabbed;

    // Góc cửa hiện tại (được apply lên pivot)
    private float _doorAngle;

    // Mốc lúc bắt đầu grab để tránh snap:
    // - _handYawStart: yaw của tay quanh hinge tại thời điểm bắt đầu grab
    // - _doorAngleStart: góc cửa tại thời điểm bắt đầu grab
    private float _handYawStart;
    private float _doorAngleStart;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Set cửa về góc đóng ngay từ đầu
        _doorAngle = closedAngle;
        ApplyDoorRotation(_doorAngle);
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isGrabbed) return;
        if (!IsUnlocked()) return;

        // Lấy yaw hiện tại của tay quanh bản lề
        float handYawNow = GetHandYawAroundHinge();

        // Tính delta yaw
        float deltaYaw = Mathf.DeltaAngle(_handYawStart, handYawNow);

        // Nếu muốn đảo chiều mở cửa
        if (invertDoorDirection) deltaYaw = -deltaYaw;

        // Tính góc cửa mục tiêu dựa trên mốc lúc bắt đầu grab
        float targetAngle = _doorAngleStart + deltaYaw;

        // Clamp để cửa chỉ mở 1 phía (closedAngle -> openAngle)
        float min = Mathf.Min(closedAngle, openAngle);
        float max = Mathf.Max(closedAngle, openAngle);


        float clamped = Mathf.Clamp(targetAngle, min, max);
        _doorAngle = Mathf.SmoothDampAngle(_doorAngle, clamped, ref _doorVel, doorSmoothTime, maxDoorSpeed);
        ApplyDoorRotation(_doorAngle);

        if (debugLogs && Time.frameCount % 15 == 0)
        {
            Debug.Log($"[Door] handYawNow={handYawNow:F1}, deltaYaw={deltaYaw:F1}, doorAngle={_doorAngle:F1}");
        }
    }

    bool IsUnlocked()
    {
        if (handlePivot == null) return false;

        float raw = NormalizeAngle(ReadHandleAxisAngle());
        float mag = handleDownIsNegative ? -raw : raw;

        bool unlocked = mag >= unlockAtDeg;

        if (debugLogs && Time.frameCount % 15 == 0)
        {
            Debug.Log($"[Door] rawHandle={raw:F1}, mag={mag:F1}, unlocked={unlocked}");
        }

        return unlocked;
    }

    private float ReadHandleAxisAngle()
    {
        Vector3 e = handlePivot.localEulerAngles;

        switch (handleAxis)
        {
            case HandleAxis.X: return e.x;
            case HandleAxis.Y: return e.y;
            default: return e.z;
        }
    }

    /// <summary>
    /// Tính "yaw" của tay quanh hinge:
    /// - Lấy vector từ hinge -> hand
    /// - Chiếu xuống mặt phẳng ngang (bỏ Y)
    /// - Tính SignedAngle so với doorHingePivot.forward quanh trục Up (Vector3.up)
    ///
    /// Kết quả là một góc (độ) biểu diễn tay đang ở đâu quanh bản lề.
    /// </summary>
    private float GetHandYawAroundHinge()
    {
        Vector3 v = handTransform.position - doorHingePivot.position;

        // Bỏ thành phần Y để chỉ xét mặt phẳng ngang (XZ)
        v.y = 0f;

        // Nếu tay quá gần hinge, tránh chia 0
        if (v.sqrMagnitude < 0.000001f)
            return 0f;

        v.Normalize();

        // Hướng reference (forward của hinge), cũng chiếu xuống XZ
        Vector3 f = doorHingePivot.forward;
        f.y = 0f;

        // Nếu f bị lệch/zero (hiếm), fallback
        if (f.sqrMagnitude < 0.000001f)
            f = Vector3.forward;
        else
            f.Normalize();

        // Góc có dấu quanh Vector3.up (trục đứng thế giới)
        // Nếu bạn muốn xoay theo local-up của hinge, có thể đổi Vector3.up -> doorHingePivot.up
        float signed = Vector3.SignedAngle(f, v, Vector3.up);
        return signed;
    }

    /// <summary>
    /// Apply rotation lên pivot cửa theo trục hingeAxis.
    /// </summary>
    private void ApplyDoorRotation(float angle)
    {
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

    private float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }

    public void OnHandleGrabbed()
    {
        _isGrabbed = true;
        _doorAngleStart = _doorAngle;

        if (handTransform != null && doorHingePivot != null)
            _handYawStart = GetHandYawAroundHinge();
        else
            _handYawStart = 0f;

        if (debugLogs)
            Debug.Log($"[Door] Grabbed. doorAngleStart={_doorAngleStart:F1}, handYawStart={_handYawStart:F1}");
    }

    public void OnHandleReleased()
    {
        _isGrabbed = false;

        if (debugLogs)
            Debug.Log("[Door] Released");
    }
}