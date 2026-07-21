using System.Collections;
using Fusion;
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

    [Tooltip("The hose must remain over the limit for this long before it is released. This ignores one-frame physics spikes.")]
    [Min(0f)]
    [SerializeField] private float overstretchGraceSeconds = 0.15f;

    [Header("Cooldown")]
    [SerializeField] private float disableSeconds = 0.2f;

    private Grabbable grabbable;
    private NetworkObject networkObject;
    private bool isGrabbed;
    private bool isReleasing;
    private float overstretchedSeconds;

    private void Awake()
    {
        if (endAnchor == null) endAnchor = transform;

        grabbable = GetComponentInChildren<Grabbable>(true);
        networkObject = GetComponentInParent<NetworkObject>(true);

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
        if (evt.Type == PointerEventType.Select)
        {
            isGrabbed = true;
            overstretchedSeconds = 0f;
        }
        else if (evt.Type == PointerEventType.Unselect ||
                 evt.Type == PointerEventType.Cancel)
        {
            isGrabbed = false;
            overstretchedSeconds = 0f;
        }
    }

    private void LateUpdate()
    {
        if (isReleasing || !isGrabbed) return;
        if (startAnchor == null || endAnchor == null) return;

        // Only the peer simulating the extinguisher may clamp or force-release
        // the nozzle. A remote pose can temporarily be beyond the hose limit
        // while authority and the replicated secondary pose are converging.
        if (networkObject != null &&
            networkObject.Runner != null &&
            !networkObject.HasStateAuthority)
        {
            return;
        }

        Vector3 delta = endAnchor.position - startAnchor.position;
        float dist = delta.magnitude;

        if (dist <= maxLength)
        {
            overstretchedSeconds = 0f;
            return;
        }

        overstretchedSeconds += Time.deltaTime;
        if (overstretchedSeconds < overstretchGraceSeconds)
            return;

        if (clampToMaxLength && dist > 0.0001f)
            endAnchor.position = startAnchor.position + (delta / dist) * maxLength;

        StartCoroutine(ForceDrop());
    }

    private IEnumerator ForceDrop()
    {
        isReleasing = true;
        overstretchedSeconds = 0f;
        if (grabbable != null) grabbable.enabled = false;

        yield return new WaitForSeconds(disableSeconds);

        if (grabbable != null) grabbable.enabled = true;
        isReleasing = false;
    }

    private void OnValidate()
    {
        maxLength = Mathf.Max(0f, maxLength);
        disableSeconds = Mathf.Max(0f, disableSeconds);
        overstretchGraceSeconds = Mathf.Max(0f, overstretchGraceSeconds);
    }
}
