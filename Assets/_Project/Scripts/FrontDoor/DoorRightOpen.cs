using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Điều khiển khi nào cửa phải (right door) có thể được grab để mở.
/// 
/// Logic: Cửa phải chỉ mở được khi cửa trái đã mở hoặc cửa phải đang mở.
/// → Ngăn user mở cửa phải khi cửa trái còn đóng (giống cửa đôi thực tế).
/// 
/// Cửa phải dùng OneGrabRotateTransformer (trên Inspector) để xoay trực tiếp bằng tay,
/// KHÔNG cần HandleController hay DoorLeftOpenByHandle — chỉ cần enable/disable Grabbable.
/// </summary>
public class DoorRightOpen : MonoBehaviour
{
    [Header("Refs")]
    // Transform pivot của cửa trái — đọc góc Y để biết cửa trái mở chưa
    public Transform leftDoorPivot;
    
    // Transform pivot của cửa phải — đọc góc Y để biết cửa phải đang mở không
    public Transform rightDoorPivot;
    
    // Component Grabbable trên cửa phải — bật/tắt để cho phép/ngăn grab
    public Grabbable rightGrabbable;

    // HingeJoint trên cửa phải (nếu có) — dùng để bật/tắt physics
    public HingeJoint rightHingeJoint;

    // Rigidbody của cửa phải — dùng để bật/tắt physics (isKinematic)
    public Rigidbody rightRigidbody;

    [Header("Settings")]
    // Góc tối thiểu (độ) cửa trái phải mở để unlock cửa phải
    // Ví dụ: 10° = cửa trái mở hé ít nhất 10° thì cửa phải mới grab được
    public float leftOpenThreshold = 10f;
    
    // Góc tối thiểu (độ) để coi cửa phải đang mở (tránh khóa lại khi vừa hé)
    public float rightOpenThreshold = 3f;

    /// <summary>
    /// Khởi tạo: tắt grabbable → cửa phải không thể grab lúc đầu.
    /// </summary>
    void Start()
    {
        rightGrabbable.enabled = false;
    }

    /// <summary>
    /// Mỗi frame: kiểm tra điều kiện và bật/tắt khả năng grab cửa phải.
    /// </summary>
    void Update()
    {
        // Cửa phải grab được khi: cửa trái đã mở HOẶC cửa phải đang mở
        // (điều kiện "cửa phải đang mở" để user không bị mất grab giữa chừng
        //  khi đang đẩy cửa phải mà cửa trái đã đóng lại)
        bool canOpen = IsLeftDoorOpen() || IsRightDoorOpen();
        
        // Bật/tắt Grabbable — khi disabled, SDK không cho phép grab object này
        rightGrabbable.enabled = canOpen;

        // Nếu không được mở thì tắt physics của cửa phải
        SetRightDoorPhysicsEnabled(canOpen);
    }

    /// <summary>
    /// Bật/tắt physics của cửa phải. Khi không được mở thì tắt để không đẩy được.
    /// </summary>
    private void SetRightDoorPhysicsEnabled(bool enabled)
    {
        Rigidbody rb = rightRigidbody;
        if (rb == null && rightHingeJoint != null)
            rb = rightHingeJoint.GetComponent<Rigidbody>();
        if (rb == null) return;

        bool shouldBeKinematic = !enabled;
        if (rb.isKinematic == shouldBeKinematic) return;
        rb.isKinematic = shouldBeKinematic;
    }

    /// <summary>
    /// Kiểm tra cửa trái đã mở (quá threshold) chưa.
    /// Đọc góc Y của leftDoorPivot, normalize về [-180, 180], so với threshold.
    /// </summary>
    private bool IsLeftDoorOpen()
    {
        // Đọc góc xoay Y (euler) — Unity trả về [0, 360)
        float angle = leftDoorPivot.localEulerAngles.y;
        
        // Chuyển về [-180, 180) để Abs() hoạt động đúng
        // Ví dụ: 350° → -10° → Abs = 10°
        if (angle > 180f) angle -= 360f;
        
        // So sánh giá trị tuyệt đối với threshold (mở theo hướng nào cũng tính)
        return Mathf.Abs(angle) > leftOpenThreshold;
    }

    /// <summary>
    /// Kiểm tra cửa phải đã mở (quá threshold) chưa.
    /// Tương tự IsLeftDoorOpen nhưng cho cửa phải.
    /// </summary>
    private bool IsRightDoorOpen()
    {
        float angle = rightDoorPivot.localEulerAngles.y;
        if (angle > 180f) angle -= 360f;
        return Mathf.Abs(angle) > rightOpenThreshold;
    }
}
