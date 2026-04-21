using System.Collections;
using UnityEngine;
using Oculus.Interaction;

public class SafetyPinDetachOnPull : MonoBehaviour
{
    [Header("Reference on extinguisher")]
    [SerializeField] private Transform socketReference;

    [Header("Release Settings")]
    [SerializeField] private float releaseDistance = 0.01f;
    [SerializeField] private float reenableGrabDelay = 0.15f;

    [Header("Detach / Hide Mode")]
    [SerializeField] private bool hideInsteadOfDrop = true;
    [SerializeField] private Transform droppedParent;

    [Header("Physics")]
    [SerializeField] private Rigidbody pinRigidbody;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Grabbable grabbable;
    private bool isGrabbed;
    private bool isRemoved;

    private Collider[] allColliders;
    private Renderer[] allRenderers;

    public bool IsRemoved => isRemoved;

    [Header("Outlines")]
    [SerializeField] private Outline extinguisherBodyOutline;
    [SerializeField] private Outline nozzleOutline;
    [SerializeField] private Outline safetyPinOutline;

    private void Awake()
    {
        if (pinRigidbody == null)
            pinRigidbody = GetComponent<Rigidbody>();

        if (grabbable == null)
            grabbable = GetComponentInChildren<Grabbable>(true);

        allColliders = GetComponentsInChildren<Collider>(true);
        allRenderers = GetComponentsInChildren<Renderer>(true);

        if (pinRigidbody != null)
        {
            pinRigidbody.isKinematic = true;
            pinRigidbody.useGravity = false;
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
        if (isRemoved) return;

        if (evt.Type == PointerEventType.Select)
        {
            isGrabbed = true;
            if (debugLog) Debug.Log("[SafetyPin] Grabbed");
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            isGrabbed = false;
            if (debugLog) Debug.Log("[SafetyPin] Released by hand");
        }
    }

    private void Update()
    {
        if (isRemoved) return;
        if (!isGrabbed) return;
        if (socketReference == null) return;

        float dist = Vector3.Distance(transform.position, socketReference.position);

        if (debugLog)
            Debug.Log($"[SafetyPin] Distance to socket = {dist}");

        if (dist >= releaseDistance)
        {
            DetachAndRemove();
        }
    }

    [ContextMenu("Detach And Remove")]
    public void DetachAndRemove()
    {
        if (isRemoved) return;

        isRemoved = true;
        isGrabbed = false;

        if (debugLog) Debug.Log("[SafetyPin] DETACHED");

        // Update outline
        updateOutlineVisibility();
        // Sfx
        AudioManager.Instance.PlayOneShot("FEPinPull");

        if (hideInsteadOfDrop)
        {
            StartCoroutine(HidePin());
            return;
        }

        StartCoroutine(DetachAndDropPhysics());
    }

    private void updateOutlineVisibility()
    {
        if (extinguisherBodyOutline != null)
            extinguisherBodyOutline.enabled = true;
        if (nozzleOutline != null)
            nozzleOutline.enabled = true;
        if (safetyPinOutline != null)
            safetyPinOutline.enabled = false;
    }

    private IEnumerator HidePin()
    {
        if (grabbable != null)
            grabbable.enabled = false;

        yield return null;

        foreach (var c in allColliders)
        {
            if (c != null) c.enabled = false;
        }

        foreach (var r in allRenderers)
        {
            if (r != null) r.enabled = false;
        }

        // nếu muốn ẩn hẳn object:
        gameObject.SetActive(false);
    }

    private IEnumerator DetachAndDropPhysics()
    {
        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;
        Vector3 worldScale = transform.lossyScale;

        if (droppedParent != null)
            transform.SetParent(droppedParent, true);
        else
            transform.SetParent(null, true);

        transform.position = worldPos;
        transform.rotation = worldRot;
        SetWorldScale(transform, worldScale);

        if (pinRigidbody != null)
        {
            pinRigidbody.isKinematic = false;
            pinRigidbody.useGravity = true;
            pinRigidbody.linearVelocity = Vector3.zero;
            pinRigidbody.angularVelocity = Vector3.zero;
            pinRigidbody.WakeUp();
        }

        if (grabbable != null)
            grabbable.enabled = false;

        yield return new WaitForSeconds(reenableGrabDelay);

        if (grabbable != null)
            grabbable.enabled = true;
    }

    private void SetWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        if (target.parent == null)
        {
            target.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentScale = target.parent.lossyScale;

        target.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z)
        );
    }

    private float SafeDivide(float a, float b)
    {
        if (Mathf.Abs(b) < 0.000001f) return a;
        return a / b;
    }
}