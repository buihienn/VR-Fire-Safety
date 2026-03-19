using UnityEngine;

/// <summary>
/// Điều khiển việc mở cửa trái bằng tay nắm (handle).
/// 
/// Luồng hoạt động:
/// 1. User grab handle → HandleController gọi OnHandleGrabbed() 
/// 2. Mỗi frame, HandleController gọi UpdateHandPosition() với vị trí tay thật
/// 3. Script kiểm tra: handle đã xoay đủ góc chưa? (IsUnlocked)
///    - Nếu cửa đang đóng → cần xoay handle ≥ unlockAtDeg mới mở được
///    - Nếu cửa đã mở → luôn cho phép (không cần giữ handle)
/// 4. Nếu unlocked → tính góc cửa dựa trên vị trí tay quanh bản lề → xoay cửa
/// 5. User thả handle → HandleController gọi OnHandleReleased() → ngừng theo dõi
/// </summary>
public class DoorLeftOpenByHandle : MonoBehaviour
{
    // --- Enum định nghĩa trục xoay ---
    
    // Trục xoay của cửa (LocalY = bản lề đứng, thường dùng nhất)
    public enum DoorAxis { LocalX, LocalY, LocalZ }

    // Trục đọc góc của handle (Z = handle xoay quanh trục forward)
    public enum HandleAxis { X, Y, Z }

    [Header("Refs")]
    // Transform của pivot bản lề cửa — cửa xoay quanh điểm này
    public Transform doorHingePivot;
    
    // Transform của handle — dùng để đọc góc xoay handle
    public Transform handlePivot;

    // HingeJoint trên cửa (nếu có) — dùng để bật/tắt physics khi mở bằng handle
    public HingeJoint doorHingeJoint;

    // Rigidbody của cửa — dùng để bật/tắt physics (isKinematic)
    public Rigidbody doorRigidbody;

    // Vị trí tay thật trong world space (nhận từ HandleController mỗi frame)
    private Vector3 _handPosition;

    [Header("Handle Rotation Reading")]
    // Trục để đọc góc xoay của handle (thường là Z cho handle xoay kiểu bật xuống)
    public HandleAxis handleAxis = HandleAxis.Z;

    // true = handle kéo xuống cho góc âm (ví dụ: 0° → -90°)
    // Khi đọc góc, sẽ đảo dấu để so sánh với unlockAtDeg (giá trị dương)
    public bool handleDownIsNegative = true;

    [Header("Unlock by Handle Angle")]
    // Góc tối thiểu handle phải xoay (tính bằng độ) để "mở khóa" cửa
    // Ví dụ: 30° = user phải kéo handle xuống ít nhất 30° mới đẩy được cửa
    public float unlockAtDeg = 30f;

    [Header("Door Rotation")]
    // Trục xoay của cửa trên pivot (thường LocalY cho cửa xoay ngang)
    public DoorAxis hingeAxis = DoorAxis.LocalY;

    // Góc cửa khi đóng hoàn toàn (thường = 0°)
    public float closedAngle = 0f;

    // Góc cửa khi mở hoàn toàn (ví dụ: 170° = gần 180° nhưng không chạm tường)
    public float openAngle = 170f;

    [Header("Optional Tuning")]
    // Đảo chiều mở cửa (nếu cửa mở sai hướng so với mong muốn)
    public bool invertDoorDirection = false;

    // Bật/tắt debug log trong Console
    public bool debugLogs = false;

    [Header("Smoothing")]
    // Thời gian làm mượt chuyển động cửa (SmoothDamp) — giá trị nhỏ = phản hồi nhanh
    public float doorSmoothTime = 0.06f;
    
    // Tốc độ xoay tối đa của cửa (độ/giây)
    public float maxDoorSpeed = 999f;

    // Velocity nội bộ cho SmoothDampAngle — SDK dùng, không cần quan tâm
    private float _doorVel;

    // === Trạng thái nội bộ ===
    
    // true khi user đang grab handle
    private bool _isGrabbed;

    // Góc cửa hiện tại (đang được apply lên doorHingePivot)
    private float _doorAngle;

    // Mốc tham chiếu lúc bắt đầu grab — dùng để tính DELTA thay vì góc tuyệt đối
    // → tránh cửa nhảy đột ngột khi bắt đầu grab
    
    // Yaw của tay quanh bản lề tại thời điểm grab
    private float _handYawStart;
    
    // Góc cửa tại thời điểm grab
    private float _doorAngleStart;

    // Coroutine delay cho SyncHingeJointEnabled khi thả handle
    private Coroutine _syncDelayRoutine;
    
    /// <summary>
    /// Khởi tạo: đặt cửa về vị trí đóng.
    /// </summary>
    void Start()
    {
        _doorAngle = closedAngle;
        ApplyDoorRotation(_doorAngle);
        SetHingeJointEnabled(false);
    }

    /// <summary>
    /// Mỗi frame: nếu đang grab VÀ handle đã xoay đủ → tính góc cửa mới từ vị trí tay.
    /// </summary>
    void Update()
    {
        // Không grab → không làm gì
        if (!_isGrabbed) return;
        
        // Handle chưa xoay đủ góc (và cửa đang đóng) → không cho mở
        if (!IsUnlocked()) return;

        // === Tính góc cửa dựa trên vị trí tay ===

        // Lấy yaw hiện tại: góc của tay quanh bản lề trên mặt phẳng ngang (XZ)
        float handYawNow = GetHandYawAroundHinge();

        // Tính delta: tay đã xoay bao nhiêu độ SO VỚI lúc bắt đầu grab
        // DeltaAngle xử lý wrap-around (ví dụ: 350° → 10° = +20° chứ không phải -340°)
        float deltaYaw = Mathf.DeltaAngle(_handYawStart, handYawNow);

        // Đảo chiều nếu cần (tùy setup scene)
        if (invertDoorDirection) deltaYaw = -deltaYaw;

        // Góc mục tiêu = góc cửa lúc grab + delta tay
        // → cửa di chuyển TƯƠNG ĐỐI theo tay, không nhảy về 0°
        float targetAngle = _doorAngleStart + deltaYaw;

        // Clamp: giới hạn cửa trong khoảng [closedAngle, openAngle]
        // → cửa không xoay quá mức (không xuyên tường)
        float min = Mathf.Min(closedAngle, openAngle);
        float max = Mathf.Max(closedAngle, openAngle);

        float clamped = Mathf.Clamp(targetAngle, min, max);
        
        // SmoothDamp: làm mượt chuyển động cửa (tránh giật)
        // _doorAngle tiến dần về clamped thay vì nhảy trực tiếp
        _doorAngle = Mathf.SmoothDampAngle(_doorAngle, clamped, ref _doorVel, doorSmoothTime, maxDoorSpeed);
        
        // Apply góc lên transform của pivot cửa
        ApplyDoorRotation(_doorAngle);

        // Khi đang grab thì luôn tắt hinge joint để tránh physics cản trở
        SetHingeJointEnabled(false);
    }

    /// <summary>
    /// Kiểm tra handle đã "mở khóa" chưa.
    /// - Cửa đã mở (cách closedAngle > 1°) → luôn true (không cần giữ handle)
    /// - Cửa đang đóng → phải xoay handle ≥ unlockAtDeg mới true
    /// </summary>
    bool IsUnlocked()
    {
        // Kiểm tra cửa đã mở chưa: so sánh góc hiện tại với góc đóng
        float doorDelta = Mathf.Abs(Mathf.DeltaAngle(_doorAngle, closedAngle));

        // Cửa cách vị trí đóng > 1° → coi như đã mở → luôn cho phép đẩy tiếp
        if (doorDelta > 1f) return true;

        // Cửa đang đóng → cần handle để mở khóa
        if (handlePivot == null) return false;

        // === Cửa đang đóng → kiểm tra góc handle ===
        
        // Đọc góc xoay của handle trên trục đã chọn (thường là Z)
        // localEulerAngles trả về [0, 360) → NormalizeAngle chuyển về [-180, 180)
        float raw = NormalizeAngle(ReadHandleAxisAngle());
        
        // Nếu handleDownIsNegative = true: handle kéo xuống cho góc âm
        // → đảo dấu để so sánh với unlockAtDeg (giá trị dương)
        // Ví dụ: raw = -45° → mag = 45° → 45 >= 30 → unlocked!
        float mag = handleDownIsNegative ? -raw : raw;

        // So sánh: handle đã xoay đủ góc chưa?
        bool unlocked = mag >= unlockAtDeg;

        if (debugLogs && Time.frameCount % 15 == 0)
        {
            Debug.Log($"[Door] rawHandle={raw:F1}, mag={mag:F1}, unlocked={unlocked}");
        }

        return unlocked;
    }

    /// <summary>
    /// Bật/tắt physics của cửa. Khi mở bằng handle thì tắt để tránh physics cản trở.
    /// </summary>
    private void SetHingeJointEnabled(bool enabled)
    {
        Rigidbody rb = doorRigidbody;
        if (rb == null && doorHingeJoint != null)
            rb = doorHingeJoint.GetComponent<Rigidbody>();
        if (rb == null) return;

        bool shouldBeKinematic = !enabled;
        if (rb.isKinematic == shouldBeKinematic) return;
        rb.isKinematic = shouldBeKinematic;
    }

    /// <summary>
    /// Đồng bộ trạng thái physics theo unlock.
    /// Nếu cửa chưa unlock thì tắt physics để không đẩy được.
    /// </summary>
    private void SyncHingeJointEnabled()
    {
        bool unlocked = IsUnlocked();
        SetHingeJointEnabled(unlocked);

        Rigidbody rb = doorRigidbody;
            if (rb == null && doorHingeJoint != null)
                rb = doorHingeJoint.GetComponent<Rigidbody>();

            string rbState = rb == null ? "rb=null" : (rb.isKinematic ? "kinematic" : "dynamic");
            Debug.Log($"[Door] SyncHingeJointEnabled: unlocked={unlocked}, rb={rbState}");
    }

    /// <summary>
    /// Đọc góc xoay của handle trên trục đã chọn (X, Y, hoặc Z).
    /// Trả về giá trị từ localEulerAngles (0° → 360°).
    /// </summary>
    private float ReadHandleAxisAngle()
    {
        Vector3 e = handlePivot.localEulerAngles;

        switch (handleAxis)
        {
            case HandleAxis.X: return e.x;
            case HandleAxis.Y: return e.y;
            default: return e.z; // Z là mặc định
        }
    }

    /// <summary>
    /// Tính "yaw" (góc ngang) của tay quanh bản lề cửa.
    /// 
    /// Cách tính:
    /// 1. Vector từ bản lề → tay (bỏ Y → chỉ xét mặt phẳng ngang XZ)
    /// 2. So sánh với hướng forward của bản lề
    /// 3. Dùng SignedAngle quanh trục Up → ra góc có dấu
    /// 
    /// Kết quả: góc (độ) biểu diễn tay đang ở đâu quanh bản lề.
    /// Ví dụ: 0° = thẳng trước bản lề, +90° = bên phải, -90° = bên trái
    /// </summary>
    public float GetHandYawAroundHinge()
    {
        // Vector từ bản lề → vị trí tay thật
        Vector3 v = _handPosition - doorHingePivot.position;

        // Chiếu xuống mặt phẳng ngang (bỏ cao độ Y)
        // → chỉ quan tâm tay ở đâu trên mặt bằng, không quan tâm cao thấp
        v.y = 0f;

        // Tay quá gần bản lề → vector gần zero → không thể tính góc → trả về 0
        if (v.sqrMagnitude < 0.000001f)
            return 0f;

        v.Normalize();

        // Hướng tham chiếu: forward của bản lề, cũng chiếu xuống XZ
        Vector3 f = doorHingePivot.forward;
        f.y = 0f;

        // Fallback nếu forward bị zero (rất hiếm, chỉ khi pivot bị lật 90°)
        if (f.sqrMagnitude < 0.000001f)
            f = Vector3.forward;
        else
            f.Normalize();

        // SignedAngle: góc từ f → v, quay quanh Vector3.up (trục đứng)
        // Dương = tay xoay theo chiều kim đồng hồ (nhìn từ trên xuống)
        // Âm = tay xoay ngược chiều kim đồng hồ
        float signed = Vector3.SignedAngle(f, v, Vector3.up);
        return signed;
    }

    /// <summary>
    /// Gán rotation cho pivot cửa theo đúng trục đã chọn (hingeAxis).
    /// Chỉ thay đổi 1 trục, 2 trục còn lại = 0.
    /// </summary>
    private void ApplyDoorRotation(float angle)
    {
        switch (hingeAxis)
        {
            case DoorAxis.LocalX:
                doorHingePivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
                break;
            case DoorAxis.LocalY: // Phổ biến nhất: cửa xoay ngang
                doorHingePivot.localRotation = Quaternion.Euler(0f, angle, 0f);
                break;
            case DoorAxis.LocalZ:
                doorHingePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                break;
        }
    }

    /// <summary>
    /// Chuyển góc từ [0, 360) sang [-180, 180).
    /// Unity localEulerAngles trả về [0, 360) → cần chuyển để tính toán đúng.
    /// Ví dụ: 350° → -10°, 270° → -90°
    /// </summary>
    private float NormalizeAngle(float a)
    {
        if (a > 180f) a -= 360f;
        return a;
    }

    // ============================================================
    // === API công khai — được gọi bởi HandleController ===
    // ============================================================

    /// <summary>
    /// Nhận vị trí tay thật mỗi frame từ HandleController.
    /// Dùng Grabbable.GrabPoints[0].position — đây là vị trí tay TRACKED,
    /// không bị snap bởi HandGrabPose (tay visual bị snap nhưng grab point thì không).
    /// </summary>
    public void UpdateHandPosition(Vector3 worldPos)
    {
        _handPosition = worldPos;
    }

    /// <summary>
    /// Gọi khi user bắt đầu grab handle.
    /// Lưu mốc tham chiếu (góc cửa + yaw tay) để tính DELTA sau này.
    /// 
    /// Tại sao dùng delta thay vì góc tuyệt đối:
    /// Nếu cửa đang mở 45° và user grab → tay ở yaw 60°
    /// → nếu dùng tuyệt đối: cửa nhảy về 60° (giật!)
    /// → nếu dùng delta: cửa giữ 45°, chỉ thay đổi khi tay DI CHUYỂN thêm
    /// </summary>
    public void OnHandleGrabbed(Vector3 handWorldPos)
    {
        // Đánh dấu đang grab
        _isGrabbed = true;

        // Hủy delay nếu đang chờ sau khi thả handle
        if (_syncDelayRoutine != null)
        {
            StopCoroutine(_syncDelayRoutine);
            _syncDelayRoutine = null;
        }

        // Tắt hinge joint khi mở bằng handle
        SetHingeJointEnabled(false);
        
        // Lưu góc cửa hiện tại làm mốc
        _doorAngleStart = _doorAngle;
        
        // Lưu vị trí tay ban đầu
        _handPosition = handWorldPos;

        // Tính và lưu yaw ban đầu của tay quanh bản lề
        if (doorHingePivot != null)
            _handYawStart = GetHandYawAroundHinge();
        else
            _handYawStart = 0f;

        if (debugLogs)
            Debug.Log($"[Door] Grabbed. doorAngleStart={_doorAngleStart:F1}, handYawStart={_handYawStart:F1}");
    }

    /// <summary>
    /// Gọi khi user thả handle. Ngừng theo dõi vị trí tay.
    /// Cửa sẽ giữ nguyên ở vị trí hiện tại (không tự đóng).
    /// </summary>
    public void OnHandleReleased()
    {
        _isGrabbed = false;

        // Sau khi thả handle, đợi 2s rồi mới sync physics
        if (_syncDelayRoutine != null)
            StopCoroutine(_syncDelayRoutine);
        _syncDelayRoutine = StartCoroutine(DelaySyncHingeJointEnabled(2f));
        
        Debug.Log("[Door] Released");
    }

    private System.Collections.IEnumerator DelaySyncHingeJointEnabled(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        SyncHingeJointEnabled();
        _syncDelayRoutine = null;
    }
}