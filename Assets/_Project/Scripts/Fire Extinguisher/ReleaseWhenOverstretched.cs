using System.Collections;
using UnityEngine;
using Oculus.Interaction;

public class ReleaseWhenOverstretched : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform startAnchor;  
    [SerializeField] private Transform endAnchor;   

    [Header("Meters")]
    [SerializeField] private float maxLength = 0.8f;
    [SerializeField] private bool clampToMaxLength = true;

    [Header("Cooldown")]
    [SerializeField] private float disableSeconds = 0.2f;

    private Grabbable grabbable;
    private bool isGrabbed;
    private bool isReleasing;

    private void Awake()
    {
        if (endAnchor == null) endAnchor = transform;

        grabbable = GetComponentInChildren<Grabbable>(true);

        if (startAnchor == null && transform.parent != null)
        {
            foreach (Transform child in transform.parent)
            {
                if (child.name.StartsWith("StartAnchor"))
                {
                    startAnchor = child;
                    break;
                }
            }
        }
    }

    private void OnEnable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        if (grabbable != null)
            grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) isGrabbed = true;
        if (evt.Type == PointerEventType.Unselect) isGrabbed = false;
    }

    private void LateUpdate()
    {
        if (isReleasing || !isGrabbed) return;
        if (startAnchor == null || endAnchor == null) return;

        Vector3 delta = endAnchor.position - startAnchor.position;
        float dist = delta.magnitude;

        if (dist <= maxLength) return;

        if (clampToMaxLength && dist > 0.0001f)
            endAnchor.position = startAnchor.position + (delta / dist) * maxLength;

        StartCoroutine(ForceDrop());
    }

    private IEnumerator ForceDrop()
    {
        isReleasing = true;
        if (grabbable != null) grabbable.enabled = false;

        yield return new WaitForSeconds(disableSeconds);

        if (grabbable != null) grabbable.enabled = true;
        isReleasing = false;
    }
}
