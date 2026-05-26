using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.Events;

public class FireExtinguisherSmokeUse : MonoBehaviour, IHandGrabUseDelegate
{
    [Header("Lever / Trigger")]
    [SerializeField] private Transform triggerLever;

    [Tooltip("Góc xoay thêm khi bóp cò. Nếu xoay sai chiều, đổi dấu X/Y/Z.")]
    [SerializeField] private Vector3 pressedEulerOffset = new Vector3(-25f, 0f, 0f);

    [Tooltip("Dùng khi pivot của Trigger_Lever bị lệch, cần kéo/move lever thêm một chút.")]
    [SerializeField] private bool usePositionOffset = false;

    [SerializeField] private Vector3 pressedLocalPositionOffset = Vector3.zero;

    [Header("Use Settings")]
    [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.3f;
    [SerializeField, Range(0f, 1f)] private float sprayThreshold = 0.7f;
    [SerializeField] private float triggerSpeed = 10f;

    [SerializeField]
    private AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Smoke / CO2 FX")]
    [SerializeField] private ParticleSystem smokeFX;

    [Tooltip("Bật nếu object ParticleSystem đang bị SetActive(false) lúc đầu.")]
    [SerializeField] private bool enableSmokeObjectWhileSpraying = true;

    [Header("Optional Logic")]
    [SerializeField] private bool requireCanSpray = false;

    [Tooltip("Nếu requireCanSpray = true, chỉ phun khi biến này true. Có thể gọi SetCanSpray(true) từ script rút chốt.")]
    [SerializeField] private bool canSpray = true;

    [Header("Events")]
    public UnityEvent OnSprayStart;
    public UnityEvent OnSprayStop;
    public UnityEvent<float> OnUseProgressChanged;

    private Quaternion releasedLocalRotation;
    private Quaternion pressedLocalRotation;

    private Vector3 releasedLocalPosition;
    private Vector3 pressedLocalPosition;

    private float dampedUseStrength = 0f;
    private float lastUseTime;
    private bool isSpraying = false;

    private void Awake()
    {
        if (triggerLever != null)
        {
            releasedLocalRotation = triggerLever.localRotation;
            pressedLocalRotation = releasedLocalRotation * Quaternion.Euler(pressedEulerOffset);

            releasedLocalPosition = triggerLever.localPosition;
            pressedLocalPosition = releasedLocalPosition + pressedLocalPositionOffset;
        }

        StopSmokeImmediate();
        UpdateLever(0f);
    }

    private void OnDisable()
    {
        StopSpray();
        UpdateLever(0f);
    }

    public void BeginUse()
    {
        dampedUseStrength = 0f;
        lastUseTime = Time.realtimeSinceStartup;
    }

    public void EndUse()
    {
        dampedUseStrength = 0f;
        UpdateLever(0f);
        StopSpray();
    }

    public float ComputeUseStrength(float strength)
    {
        float delta = Time.realtimeSinceStartup - lastUseTime;
        lastUseTime = Time.realtimeSinceStartup;

        if (strength > dampedUseStrength)
        {
            dampedUseStrength = Mathf.Lerp(
                dampedUseStrength,
                strength,
                triggerSpeed * delta
            );
        }
        else
        {
            dampedUseStrength = strength;
        }

        float progress = strengthCurve.Evaluate(dampedUseStrength);

        UpdateLever(progress);
        UpdateSpray(progress);

        OnUseProgressChanged?.Invoke(progress);

        return progress;
    }

    private void UpdateLever(float progress)
    {
        if (triggerLever == null) return;

        triggerLever.localRotation = Quaternion.Lerp(
            releasedLocalRotation,
            pressedLocalRotation,
            progress
        );

        if (usePositionOffset)
        {
            triggerLever.localPosition = Vector3.Lerp(
                releasedLocalPosition,
                pressedLocalPosition,
                progress
            );
        }
    }

    private void UpdateSpray(float progress)
    {
        bool allowedToSpray = !requireCanSpray || canSpray;

        if (!allowedToSpray)
        {
            StopSpray();
            return;
        }

        if (progress >= sprayThreshold && !isSpraying)
        {
            StartSpray();
        }
        else if (progress <= releaseThreshold && isSpraying)
        {
            StopSpray();
        }
    }

    private void StartSpray()
    {
        isSpraying = true;

        if (smokeFX != null)
        {
            if (enableSmokeObjectWhileSpraying)
            {
                smokeFX.gameObject.SetActive(true);
            }

            if (!smokeFX.isPlaying)
            {
                smokeFX.Play(true);
            }
        }

        OnSprayStart?.Invoke();
    }

    private void StopSpray()
    {
        if (!isSpraying && smokeFX == null) return;

        isSpraying = false;

        if (smokeFX != null && smokeFX.isPlaying)
        {
            smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        OnSprayStop?.Invoke();
    }

    private void StopSmokeImmediate()
    {
        if (smokeFX == null) return;

        smokeFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (enableSmokeObjectWhileSpraying)
        {
            smokeFX.gameObject.SetActive(false);
        }
    }

    public void SetCanSpray(bool value)
    {
        canSpray = value;

        if (!canSpray)
        {
            StopSpray();
        }
    }

    public void AllowSpray()
    {
        SetCanSpray(true);
    }

    public void BlockSpray()
    {
        SetCanSpray(false);
    }
}