using UnityEngine;
using System.Reflection;
using Oculus.Interaction;

/// <summary>
/// Bám nấc cho núm vặn theo các bước cố định khi dùng OneGrabRotateTransformer.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class FanSwitch : MonoBehaviour
{
	private const int Steps = 6; // Co dinh 6 nac: 0..5.
	private const float StepAngle = 60f; // Moi nac cach nhau 60 do.

	[Header("Rotation Settings")]
	[SerializeField] private Vector3 localAxis = Vector3.up; // Trục xoay trong local của núm.

	[Header("Fan Output")]
	[SerializeField] private FanRotator fanRotator; // Đối tượng điều khiển tốc độ quạt.
	[SerializeField] private float[] stepSpeeds = new float[] { 0f, 100f, 200f, 300f, 400f, 500f }; // Tốc độ theo từng nấc.

	[Header("Ignition")]
    [SerializeField] private SparkIgnitionTrigger sparkIgnitionTrigger;

	private Grabbable _grabbable; // Nguồn sự kiện grab.
	private Quaternion _initialLocalRotation; // Góc local gốc để làm mốc.
	private bool _isGrabbed; // Trạng thái đang grab.
	private int _currentStepIndex = -1; // Nấc hiện tại.
	

	private void Awake()
	{
		_grabbable = GetComponent<Grabbable>();
		_initialLocalRotation = transform.localRotation; 

		ApplyStep(0); // Đặt về nấc 0 khi khởi tạo.
	}

	private void OnEnable()
	{
		if (_grabbable != null) _grabbable.WhenPointerEventRaised += HandlePointerEvent;
	}

	private void OnDisable()
	{
		if (_grabbable != null) _grabbable.WhenPointerEventRaised -= HandlePointerEvent; 
	}

	private void LateUpdate()
	{
		UpdateStepFromRotation(); // Cập nhật mức theo góc hiện tại.
	}

	private void HandlePointerEvent(PointerEvent evt)
	{
		switch (evt.Type)
		{
			case PointerEventType.Select:
				_isGrabbed = true; // Bắt đầu grab.
				break;

			case PointerEventType.Unselect:
				_isGrabbed = false; // Kết thúc grab.
				break;
		}
	}

	private void UpdateStepFromRotation()
	{
		if (Steps <= 1) return; // Khong cap nhat neu chi co 1 nac.

		var axis = localAxis.normalized; // Chuẩn hóa trục local.
		if (axis == Vector3.zero) return; // Trục không hợp lệ.

		Vector3 refDir = Vector3.Cross(axis, Vector3.up); // Tìm hướng tham chiếu vuông góc với trục.
		if (refDir.sqrMagnitude < 1e-4f)
		{
			refDir = Vector3.Cross(axis, Vector3.right); // Dự phòng nếu trục song song với up.
		}
		refDir.Normalize(); // Chuẩn hóa hướng tham chiếu.

		var currentLocalRotation = transform.localRotation; // Góc local hiện tại.
		var axisWorld = _initialLocalRotation * axis; // Trục xoay trong world.
		var refStart = _initialLocalRotation * refDir; // Hướng tham chiếu lúc bắt đầu.
		var refNow = currentLocalRotation * refDir; // Hướng tham chiếu hiện tại.

		float angle = Vector3.SignedAngle(refStart, refNow, axisWorld); // Góc hiện tại quanh trục.
		int stepIndex = GetStepIndexFromAngle(angle); // Xác định mức theo khoảng góc.
		ApplyStep(stepIndex); // Cập nhật tốc độ quạt theo nấc.
	}

	private int GetStepIndexFromAngle(float angle)
	{
		float wrapped = Mathf.Repeat(angle + 360f, 360f); // Dua goc ve [0, 360).
		if (wrapped <= 29f) return 0; // 0-29
		if (wrapped <= 89f) return 1; // 30-89
		if (wrapped <= 149f) return 2; // 90-149
		if (wrapped <= 209f) return 3; // 150-209
		if (wrapped <= 269f) return 4; // 210-269
		if (wrapped <= 300f) return 5; // 270-300
		return 5;
	}

	private void ApplyStep(int stepIndex)
	{	
		// Cháy nên bỏ qua việc quạt quay
		// if (fanRotator == null) return; // Không có quạt để điều khiển.
		// if (stepIndex == _currentStepIndex) return; // Không đổi nếu cùng nấc.

		// int previousStep = _currentStepIndex;
		// _currentStepIndex = stepIndex;

		// float speed = GetStepSpeed(stepIndex); 
		// fanRotator.speed = speed; 
		// fanRotator.SetOn(speed > 0f); 

		// bool wasOff = previousStep <= 0;
        // bool isNowOn = stepIndex > 0;

        if (sparkIgnitionTrigger != null)
        {
			sparkIgnitionTrigger.TriggerSpark();

        }
	}

	private float GetStepSpeed(int stepIndex)
	{
		if (stepSpeeds != null && stepSpeeds.Length > 0)
		{
			int clamped = Mathf.Clamp(stepIndex, 0, stepSpeeds.Length - 1); 
			return stepSpeeds[clamped];
		}

		return 0f; // Mặc định tắt.
	}

}