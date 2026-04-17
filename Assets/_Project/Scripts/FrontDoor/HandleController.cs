using UnityEngine;
using System.Collections;
using System.Reflection;
using Oculus.Interaction;

/// <summary>
/// Điều khiển hành vi của tay nắm cửa (handle) trong VR.
/// 
/// Chức năng chính:
/// 1. Khi user grab handle → thông báo cho DoorLeftOpenByHandle để bắt đầu theo dõi cửa
/// 2. Khi user thả handle → handle tự xoay về vị trí ban đầu (animation)
/// 3. Reset góc tích lũy nội bộ của OneGrabRotateTransformer để tránh lỗi constraint bị trôi
/// 
/// Yêu cầu: GameObject phải có component Grabbable và OneGrabRotateTransformer
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class HandleController : MonoBehaviour
{
    [Header("Door Settings")]
    // Reference đến script điều khiển cửa — nhận sự kiện grab/release từ handle
    [SerializeField] private DoorLeftOpenByHandle doorController;

    [Header("Return Animation")]
    // Tốc độ handle xoay về vị trí ban đầu khi thả (giá trị càng lớn → càng nhanh)
    [SerializeField] private float returnSpeed = 10f; 
    
    // Component Grabbable của Meta SDK — xử lý việc grab/release bằng tay VR
    private Grabbable _grabbable;
    
    // Component xoay 1 tay của Meta SDK — giới hạn handle chỉ xoay trên 1 trục (Forward: -90° → 0°)
    private OneGrabRotateTransformer _rotateTransformer;
    
    // Lưu rotation ban đầu của handle để biết cần reset về đâu
    private Quaternion _initialRotation; 
    
    // Reference đến coroutine đang chạy — để có thể dừng nó khi cần
    private Coroutine _resetRotationCoroutine;
    
    // Đánh dấu handle đang bị grab hay không — dùng trong Update() để cập nhật vị trí tay
    private bool _isGrabbed;

    // ---- Reflection cache ----
    // OneGrabRotateTransformer có 2 field private lưu góc tích lũy:
    // - _relativeAngle: góc thô (chưa clamp)
    // - _constrainedRelativeAngle: góc đã clamp theo min/max
    // SDK KHÔNG cung cấp cách reset chúng, nên phải dùng reflection
    private FieldInfo _fiRelativeAngle;
    private FieldInfo _fiConstrainedRelativeAngle;

    /// <summary>
    /// Awake chạy đầu tiên khi script được tạo.
    /// Lấy references và cache reflection fields.
    /// </summary>
    private void Awake()
    {
        // Lấy component Grabbable trên cùng GameObject
        _grabbable = GetComponent<Grabbable>();
        
        // Lấy component OneGrabRotateTransformer trên cùng GameObject
        _rotateTransformer = GetComponent<OneGrabRotateTransformer>();
        
        // Ghi nhớ rotation ban đầu (vị trí handle khi chưa bị xoay)
        _initialRotation = transform.localRotation;

        // Cache reflection một lần duy nhất (tránh tìm field mỗi frame → tốn hiệu năng)
        if (_rotateTransformer != null)
        {
            // Tìm field private trong class OneGrabRotateTransformer
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = _rotateTransformer.GetType();
            _fiRelativeAngle = type.GetField("_relativeAngle", flags);
            _fiConstrainedRelativeAngle = type.GetField("_constrainedRelativeAngle", flags);
        }
    }

    /// <summary>
    /// Đăng ký lắng nghe sự kiện pointer (grab/release) từ Grabbable khi script được bật.
    /// </summary>
    private void OnEnable()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    /// <summary>
    /// Hủy đăng ký khi script bị tắt — tránh memory leak và lỗi null.
    /// </summary>
    private void OnDisable()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    /// <summary>
    /// Callback khi có sự kiện pointer (Select = grab, Unselect = release).
    /// Được gọi bởi SDK, TRƯỚC khi BeginTransform() chạy.
    /// 
    /// Thứ tự trong SDK: EndTransform() → WhenPointerEventRaised (ở đây) → BeginTransform()
    /// </summary>
    private void HandlePointerEvent(PointerEvent evt)
    {
        // Nếu chưa gán door controller trong Inspector → bỏ qua
        if (doorController == null) return;

        switch (evt.Type)
        {
            // === USER BẮT ĐẦU GRAB HANDLE ===
            case PointerEventType.Select:
                // Nếu coroutine reset đang chạy (handle đang xoay về) → dừng nó lại
                if (_resetRotationCoroutine != null)
                {
                    StopCoroutine(_resetRotationCoroutine);
                    _resetRotationCoroutine = null;
                }

                // Đảm bảo transformer đã được init — phòng trường hợp
                // Grabbable.Start() chưa chạy kịp do lifecycle của Meta SDK
                if (_rotateTransformer != null)
                {
                    _rotateTransformer.Initialize(_grabbable);
                }

                // Snap handle về vị trí ban đầu (0°)
                transform.localRotation = _initialRotation;
                
                // Reset góc tích lũy nội bộ của transformer về 0
                // → constraint sẽ đúng [-90, 0] thay vì bị trôi
                ResetTransformerAngle();

                // Đánh dấu đang grab
                _isGrabbed = true;

                // Sound effect
                AudioManager.Instance.PlayOneShot("HandleIn");
                
                // Truyền vị trí tay THẬT (grab point từ SDK) cho door controller
                // GrabPoints[0].position = vị trí tay tracked, KHÔNG bị snap bởi HandGrabPose
                if (_grabbable.GrabPoints.Count > 0)
                {
                    doorController.OnHandleGrabbed(_grabbable.GrabPoints[0].position);
                }
                break;

            // === USER THẢ HANDLE ===
            case PointerEventType.Unselect:
                // Đánh dấu không còn grab
                _isGrabbed = false;

                // Sound effect
                AudioManager.Instance.PlayOneShot("HandleOut");
                
                // Thông báo cho door controller rằng handle đã được thả
                doorController.OnHandleReleased();
                
                // Bắt đầu animation xoay handle về vị trí ban đầu
                _resetRotationCoroutine = StartCoroutine(ResetHandleRotation());
                break;
        }
    }

    /// <summary>
    /// Mỗi frame, nếu đang grab → gửi vị trí tay thật cho door controller.
    /// Door controller dùng vị trí này để tính góc cửa cần xoay.
    /// </summary>
    private void Update()
    {
        // Chỉ cập nhật khi: đang grab + có door controller + có grab point
        if (_isGrabbed && doorController != null && _grabbable.GrabPoints.Count > 0)
        {
            // Gửi vị trí tay thật (world space) — door controller dùng để tính yaw quanh bản lề
            doorController.UpdateHandPosition(_grabbable.GrabPoints[0].position);
        }
    }

    /// <summary>
    /// Reset 2 field private trong OneGrabRotateTransformer về 0 bằng reflection.
    /// 
    /// Tại sao cần: Khi coroutine reset localRotation "sau lưng" transformer,
    /// transformer vẫn nghĩ handle đang ở góc cũ (ví dụ -90°).
    /// Lần grab sau, constraint bị tính từ -90° → window lệch thành [0°, +90°] thay vì [-90°, 0°].
    /// Reset về 0 = "transformer nghĩ handle đang ở vị trí ban đầu" → constraint đúng.
    /// </summary>
    private void ResetTransformerAngle()
    {
        if (_rotateTransformer == null) return;
        // Set _relativeAngle = 0 (góc thô chưa clamp)
        _fiRelativeAngle?.SetValue(_rotateTransformer, 0f);
        // Set _constrainedRelativeAngle = 0 (góc đã clamp — quyết định _startAngle ở lần grab sau)
        _fiConstrainedRelativeAngle?.SetValue(_rotateTransformer, 0f);
    }

    /// <summary>
    /// Coroutine: Từ từ xoay handle về vị trí ban đầu sau khi thả.
    /// Dùng Slerp để animation mượt (nhanh đầu, chậm cuối).
    /// </summary>
    private IEnumerator ResetHandleRotation()
    {
        // Lặp cho đến khi handle gần đúng vị trí ban đầu (sai số < 0.1°)
        while (Quaternion.Angle(transform.localRotation, _initialRotation) > 0.1f)
        {
            // Nội suy cầu (Slerp) từ vị trí hiện tại → vị trí ban đầu
            // Time.deltaTime * returnSpeed = tỉ lệ nội suy mỗi frame
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, 
                _initialRotation, 
                Time.deltaTime * returnSpeed
            );
            // Đợi đến frame tiếp theo
            yield return null;
        }

        // Snap chính xác về vị trí ban đầu (tránh sai số nhỏ tích lũy)
        transform.localRotation = _initialRotation;

        // Reset góc tích lũy để lần grab tiếp theo bắt đầu từ đúng 0°
        ResetTransformerAngle();

        // Xóa reference → biết coroutine đã chạy xong
        _resetRotationCoroutine = null;
    }
}