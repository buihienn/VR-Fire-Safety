using UnityEngine;

/// <summary>
/// Điều khiển chốt cửa (latch) di chuyển theo góc xoay của tay nắm (handle).
/// 
/// Khi user xoay handle → latch trượt ra/vào theo tỉ lệ tuyến tính.
/// Ví dụ: handle ở 0° → latch ở vị trí ban đầu (chốt cài)
///         handle ở -60° → latch trượt ra 3cm (chốt mở hoàn toàn)
/// </summary>
public class LatchController : MonoBehaviour
{
    // Enum chung cho trục — dùng cho cả handle và latch
    public enum Axis { X, Y, Z }

    [Header("References")]
    // Transform của handle — đọc góc xoay để biết handle đang ở đâu
    public Transform handlePivot;
    
    // Transform của chốt cửa — sẽ bị di chuyển (trượt) theo handle
    public Transform latch;

    [Header("Handle Rotation Settings")]
    // Trục đọc góc xoay của handle (thường Z — handle xoay kiểu bật xuống)
    public Axis handleAxis = Axis.Z;
    
    // Góc handle khi đóng (chốt cài) — thường = 0°
    public float handleClosedAngle = 0f;
    
    // Góc handle khi mở hoàn toàn (chốt rút ra hết) — ví dụ: -60°
    public float handleOpenAngle = -60f;

    [Header("Latch Movement Settings")]
    // Trục mà latch sẽ trượt theo (X = trượt ngang, Y = trượt dọc, Z = trượt sâu)
    public Axis latchMoveAxis = Axis.X;
    
    // Khoảng cách tối đa latch trượt (mét) — 0.03 = 3cm
    public float latchMaxDistance = 0.03f;

    // Vị trí ban đầu của latch (local space) — dùng làm điểm gốc để tính offset
    private Vector3 _latchStartLocalPos;

    /// <summary>
    /// Lưu vị trí ban đầu của latch khi game bắt đầu.
    /// </summary>
    void Start()
    {
        _latchStartLocalPos = latch.localPosition;
    }

    /// <summary>
    /// Mỗi frame: đọc góc handle → tính tỉ lệ mở → di chuyển latch tương ứng.
    /// </summary>
    void Update()
    {
        // Không có reference → bỏ qua
        if (handlePivot == null || latch == null) return;

        // Đọc góc hiện tại của handle trên trục đã chọn
        // Normalize: chuyển [0, 360) → [-180, 180)
        float handleAngle = Normalize(ReadHandleAxis());

        // InverseLerp: chuyển góc handle thành tỉ lệ 0→1
        // closedAngle (0°) → t = 0 (chốt cài hoàn toàn)
        // openAngle (-60°) → t = 1 (chốt rút ra hoàn toàn)
        // Giữa chừng (-30°) → t ≈ 0.5 (chốt rút ra nửa)
        float t = Mathf.InverseLerp(handleClosedAngle, handleOpenAngle, handleAngle);
        
        // Clamp: đảm bảo t nằm trong [0, 1] — phòng trường hợp góc vượt quá range
        t = Mathf.Clamp01(t);

        // Bắt đầu từ vị trí gốc của latch
        Vector3 newPos = _latchStartLocalPos;

        // Cộng thêm offset trên trục đã chọn — latch trượt ra theo tỉ lệ t
        switch (latchMoveAxis)
        {
            case Axis.X:
                newPos.x += latchMaxDistance * t; // Trượt theo X
                break;

            case Axis.Y:
                newPos.y += latchMaxDistance * t; // Trượt theo Y
                break;

            case Axis.Z:
                newPos.z += latchMaxDistance * t; // Trượt theo Z
                break;
        }

        // Apply vị trí mới cho latch
        latch.localPosition = newPos;
    }

    /// <summary>
    /// Đọc góc xoay của handle trên trục đã chọn.
    /// Trả về giá trị từ localEulerAngles (0° → 360°).
    /// </summary>
    float ReadHandleAxis()
    {
        Vector3 e = handlePivot.localEulerAngles;

        switch (handleAxis)
        {
            case Axis.X: return e.x;
            case Axis.Y: return e.y;
            default: return e.z; // Z là mặc định
        }
    }

    /// <summary>
    /// Chuyển góc từ [0, 360) sang [-180, 180).
    /// Ví dụ: 300° → -60°, 270° → -90°
    /// </summary>
    float Normalize(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}