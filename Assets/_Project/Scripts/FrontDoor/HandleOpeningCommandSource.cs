using System.Collections;
using System.Reflection;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Local VR interaction for a rotary handle. The handle only emits an open
/// command; it never moves either leaf directly.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Grabbable))]
[RequireComponent(typeof(OneGrabRotateTransformer))]
public sealed class HandleOpeningCommandSource : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }

    [Header("Opening Command")]
    [SerializeField] private NetworkDoubleLeafOpening openingController;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
    [SerializeField] private bool useAbsoluteAngle = true;
    [Range(0f, 180f)] [SerializeField] private float triggerAngle = 89f;
    [SerializeField] private bool lockInteractionAfterTrigger = true;

    [Header("Return Animation")]
    [Min(0.01f)] [SerializeField] private float returnSpeed = 10f;

    [Header("Audio")]
    [SerializeField] private string grabAudioKey = "HandleIn";
    [SerializeField] private string releaseAudioKey = "HandleOut";

    [Header("Debug")]
    [SerializeField] private bool isGrabbed;
    [SerializeField] private bool isLocked;
    [SerializeField] private float currentHandleAngle;

    private Grabbable grabbable;
    private OneGrabRotateTransformer rotateTransformer;
    private Quaternion initialLocalRotation;
    private Coroutine returnRoutine;
    private bool commandSent;

    private FieldInfo relativeAngleField;
    private FieldInfo constrainedRelativeAngleField;

    private void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        rotateTransformer = GetComponent<OneGrabRotateTransformer>();
        initialLocalRotation = transform.localRotation;

        if (rotateTransformer != null)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            System.Type type = rotateTransformer.GetType();
            relativeAngleField = type.GetField("_relativeAngle", flags);
            constrainedRelativeAngleField = type.GetField("_constrainedRelativeAngle", flags);
        }
    }

    private void OnEnable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void Update()
    {
        currentHandleAngle = ReadSignedRelativeAngle();

        if (!isGrabbed || commandSent || isLocked || openingController == null)
            return;

        float comparableAngle = useAbsoluteAngle
            ? Mathf.Abs(currentHandleAngle)
            : currentHandleAngle;

        if (comparableAngle + 0.01f < triggerAngle)
            return;

        commandSent = true;
        openingController.RequestOpen(Mathf.Abs(currentHandleAngle));

        if (lockInteractionAfterTrigger)
            LockInteractionAfterTrigger();
    }

    public void LockInteractionAfterTrigger()
    {
        if (isLocked)
            return;

        bool wasGrabbed = isGrabbed;
        isLocked = true;
        isGrabbed = false;
        commandSent = true;

        if (grabbable != null)
            grabbable.enabled = false;

        StartReturnAnimation();

        if (wasGrabbed)
            PlayAudio(releaseAudioKey);
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                if (isLocked)
                    return;

                StopReturnAnimation();

                if (rotateTransformer != null)
                    rotateTransformer.Initialize(grabbable);

                transform.localRotation = initialLocalRotation;
                ResetTransformerAngle();
                currentHandleAngle = 0f;
                commandSent = false;
                isGrabbed = true;
                PlayAudio(grabAudioKey);
                break;

            case PointerEventType.Unselect:
                if (!isGrabbed)
                    return;

                isGrabbed = false;
                PlayAudio(releaseAudioKey);
                StartReturnAnimation();
                break;
        }
    }

    private float ReadSignedRelativeAngle()
    {
        Quaternion relative = Quaternion.Inverse(initialLocalRotation) * transform.localRotation;

        if (relative.w < 0f)
        {
            relative.x = -relative.x;
            relative.y = -relative.y;
            relative.z = -relative.z;
            relative.w = -relative.w;
        }

        Vector3 axis = rotationAxis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward
        };

        float projectedSinHalfAngle = Vector3.Dot(
            new Vector3(relative.x, relative.y, relative.z),
            axis);

        float signedAngle = 2f * Mathf.Atan2(projectedSinHalfAngle, relative.w) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, signedAngle);
    }

    private void StartReturnAnimation()
    {
        StopReturnAnimation();
        returnRoutine = StartCoroutine(ReturnToInitialRotation());
    }

    private void StopReturnAnimation()
    {
        if (returnRoutine == null)
            return;

        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    private IEnumerator ReturnToInitialRotation()
    {
        while (Quaternion.Angle(transform.localRotation, initialLocalRotation) > 0.1f)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                initialLocalRotation,
                Time.deltaTime * returnSpeed);

            yield return null;
        }

        transform.localRotation = initialLocalRotation;
        currentHandleAngle = 0f;
        ResetTransformerAngle();
        returnRoutine = null;
    }

    private void ResetTransformerAngle()
    {
        if (rotateTransformer == null)
            return;

        relativeAngleField?.SetValue(rotateTransformer, 0f);
        constrainedRelativeAngleField?.SetValue(rotateTransformer, 0f);
    }

    private static void PlayAudio(string audioKey)
    {
        if (!string.IsNullOrWhiteSpace(audioKey) && AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShot(audioKey);
    }

    private void OnValidate()
    {
        triggerAngle = Mathf.Clamp(triggerAngle, 0f, 180f);
        returnSpeed = Mathf.Max(0.01f, returnSpeed);
    }
}
