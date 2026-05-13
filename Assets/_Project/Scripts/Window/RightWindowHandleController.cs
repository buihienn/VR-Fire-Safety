using UnityEngine;
using System.Collections;
using System.Reflection;
using Oculus.Interaction;

/// <summary>
/// Handle controller rieng cho window: gui event grab/release + vi tri tay,
/// dong thoi reset goc handle ve ban dau.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class RightWindowHandleController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RightWindowGrabRotate windowController;

    [Header("Return Animation")]
    [SerializeField] private float returnSpeed = 10f;

    private Grabbable _grabbable;
    private OneGrabRotateTransformer _rotateTransformer;
    private Quaternion _initialRotation;
    private Coroutine _resetRotationCoroutine;
    private bool _isGrabbed;

    private FieldInfo _fiRelativeAngle;
    private FieldInfo _fiConstrainedRelativeAngle;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _rotateTransformer = GetComponent<OneGrabRotateTransformer>();
        _initialRotation = transform.localRotation;

        if (_rotateTransformer != null)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = _rotateTransformer.GetType();
            _fiRelativeAngle = type.GetField("_relativeAngle", flags);
            _fiConstrainedRelativeAngle = type.GetField("_constrainedRelativeAngle", flags);
        }
    }

    private void OnEnable()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (windowController == null) return;

        switch (evt.Type)
        {
            case PointerEventType.Select:
                if (_resetRotationCoroutine != null)
                {
                    StopCoroutine(_resetRotationCoroutine);
                    _resetRotationCoroutine = null;
                }

                if (_rotateTransformer != null)
                    _rotateTransformer.Initialize(_grabbable);

                transform.localRotation = _initialRotation;
                ResetTransformerAngle();

                _isGrabbed = true;

                if (_grabbable.GrabPoints.Count > 0)
                    windowController.OnHandleGrabbed(_grabbable.GrabPoints[0].position);
                break;

            case PointerEventType.Unselect:
                _isGrabbed = false;
                windowController.OnHandleReleased();
                _resetRotationCoroutine = StartCoroutine(ResetHandleRotation());
                break;
        }
    }

    private void Update()
    {
        if (_isGrabbed && windowController != null && _grabbable.GrabPoints.Count > 0)
            windowController.UpdateHandPosition(_grabbable.GrabPoints[0].position);
    }

    private void ResetTransformerAngle()
    {
        if (_rotateTransformer == null) return;
        _fiRelativeAngle?.SetValue(_rotateTransformer, 0f);
        _fiConstrainedRelativeAngle?.SetValue(_rotateTransformer, 0f);
    }

    private IEnumerator ResetHandleRotation()
    {
        while (Quaternion.Angle(transform.localRotation, _initialRotation) > 0.1f)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                _initialRotation,
                Time.deltaTime * returnSpeed
            );
            yield return null;
        }

        transform.localRotation = _initialRotation;
        ResetTransformerAngle();
        _resetRotationCoroutine = null;
    }
}
