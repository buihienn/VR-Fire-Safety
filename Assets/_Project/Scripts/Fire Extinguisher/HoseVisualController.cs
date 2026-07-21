using Fusion;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Replaces WireBuilder's Rigidbody chain with a visual-only hose. EndAnchor is
/// the authoritative pose; its Rigidbody is retained only for Meta Interaction
/// references and is always kinematic with trigger-only grab colliders.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class HoseVisualController : MonoBehaviour
{
    [Header("WireBuilder References")]
    [SerializeField] private WireController wireController;
    [SerializeField] private TubeRenderer tubeRenderer;
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;
    [SerializeField] private Rigidbody nozzleRigidbody;
    [SerializeField] private Grabbable nozzleGrabbable;
    [SerializeField] private BoxCollider wireProxy;

    [Header("Fixed Hose Length")]
    [Min(0.05f)] [SerializeField] private float maxLength = 0.46f;

    [Header("Automatic Nozzle Return")]
    [Min(0.1f)] [SerializeField] private float maximumReturnSpeed = 3f;
    [Min(0f)] [SerializeField] private float returnRotationSpeed = 10f;
    [Tooltip("Inside this distance the released nozzle docks exactly to the stored rest pose, removing residual jitter.")]
    [Min(0f)] [SerializeField] private float dockingDistance = 0.015f;

    [Header("Visual Hose")]
    [Range(8, 48)] [SerializeField] private int visualPointCount = 24;
    [Min(0f)] [SerializeField] private float sagPerMetreOfSlack = 0.55f;
    [Min(0f)] [SerializeField] private float maximumSag = 0.1f;
    [Min(0f)] [SerializeField] private float proxyPadding = 0.012f;
    [Range(0, 3)] [SerializeField] private int smoothingPasses = 1;

    private NetworkObject networkObject;
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Vector3[] visualWorldPoints;
    private Vector3[] visualLocalPoints;
    private bool nozzleSelected;

    private void Awake()
    {
        ResolveReferences();

        if (endAnchor != null)
        {
            restLocalPosition = transform.InverseTransformPoint(endAnchor.position);
            restLocalRotation = Quaternion.Inverse(transform.rotation) * endAnchor.rotation;
        }

        DisableLegacyWirePhysics();
        DisableWireProxyPhysics();
        ConfigureNozzleAsInteractionOnly();
        IgnoreNozzleAgainstOwnExtinguisher();
        EnsurePointBuffers();
        RenderVisualHose();
    }

    private void OnEnable()
    {
        if (nozzleGrabbable != null)
            nozzleGrabbable.WhenPointerEventRaised += OnNozzlePointerEvent;
    }

    private void OnDisable()
    {
        if (nozzleGrabbable != null)
            nozzleGrabbable.WhenPointerEventRaised -= OnNozzlePointerEvent;
    }

    private void Update()
    {
        if (!CanSimulateNozzle() || nozzleSelected || endAnchor == null)
            return;

        Vector3 restWorldPosition = transform.TransformPoint(restLocalPosition);
        Quaternion restWorldRotation = transform.rotation * restLocalRotation;

        EnforceMaximumDistance();

        Vector3 positionError = restWorldPosition - endAnchor.position;
        if (positionError.sqrMagnitude <= dockingDistance * dockingDistance)
        {
            endAnchor.SetPositionAndRotation(restWorldPosition, restWorldRotation);
            return;
        }

        endAnchor.position = Vector3.MoveTowards(
            endAnchor.position,
            restWorldPosition,
            maximumReturnSpeed * Time.deltaTime);

        float rotation01 = 1f - Mathf.Exp(-returnRotationSpeed * Time.deltaTime);
        endAnchor.rotation = Quaternion.Slerp(
            endAnchor.rotation,
            restWorldRotation,
            rotation01);
    }

    private void LateUpdate()
    {
        RenderVisualHose();
    }

    private void ResolveReferences()
    {
        networkObject = GetComponentInParent<NetworkObject>();

        if (wireController == null)
            wireController = GetComponent<WireController>();

        if (wireController != null)
        {
            if (tubeRenderer == null)
                tubeRenderer = wireController.ropeMesh;
            if (startAnchor == null)
                startAnchor = wireController.starAnchorTemp;
            if (endAnchor == null)
                endAnchor = wireController.endAnchorTemp;
        }

        if (tubeRenderer == null)
            tubeRenderer = GetComponentInChildren<TubeRenderer>(true);

        if (endAnchor != null)
        {
            if (nozzleRigidbody == null)
                nozzleRigidbody = endAnchor.GetComponent<Rigidbody>();
            if (nozzleGrabbable == null)
                nozzleGrabbable = endAnchor.GetComponent<Grabbable>();
        }

        if (wireProxy == null)
        {
            Transform root = networkObject != null ? networkObject.transform : transform.root;
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.gameObject.name == "WireProxy")
                {
                    wireProxy = box;
                    break;
                }
            }
        }
    }

    private void DisableLegacyWirePhysics()
    {
        if (wireController == null)
            return;

        // The old controller sampled the 21 physical segments every Update.
        // The visual hose below is now the only writer to TubeRenderer.
        wireController.enabled = false;

        if (wireController.segments != null)
        {
            foreach (Transform segment in wireController.segments)
            {
                if (segment != null)
                    segment.gameObject.SetActive(false);
            }
        }

        if (endAnchor == null)
            return;

        // EndAnchor used to be constrained to the final segment. Make every axis
        // free immediately, then remove the obsolete joint after this frame.
        foreach (ConfigurableJoint joint in endAnchor.GetComponents<ConfigurableJoint>())
        {
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.connectedBody = null;
            Destroy(joint);
        }
    }

    private void ConfigureNozzleAsInteractionOnly()
    {
        if (nozzleRigidbody != null)
        {
            // Meta HandGrabInteractable references this Rigidbody, so keep the
            // component but never allow it to enter dynamic simulation.
            nozzleRigidbody.useGravity = false;
            if (!nozzleRigidbody.isKinematic)
            {
                nozzleRigidbody.linearVelocity = Vector3.zero;
                nozzleRigidbody.angularVelocity = Vector3.zero;
            }
            nozzleRigidbody.isKinematic = true;
            nozzleRigidbody.interpolation = RigidbodyInterpolation.None;
            nozzleRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        if (endAnchor == null)
            return;

        // These colliders are interaction volumes only. Trigger colliders remain
        // usable by Meta hand-grab detection without producing collision forces.
        foreach (Collider nozzleCollider in endAnchor.GetComponentsInChildren<Collider>(true))
            nozzleCollider.isTrigger = true;
    }

    private void DisableWireProxyPhysics()
    {
        if (wireProxy == null)
            return;

        // WireProxy overlaps the extinguisher body by design. Leaving this as a
        // solid kinematic collider makes PhysX separate it from the dynamic body
        // on the first simulation step, which can launch the whole extinguisher.
        // Its BoxCollider bounds are still used below to shape the visual hose.
        wireProxy.enabled = false;

        Rigidbody proxyRigidbody = wireProxy.attachedRigidbody;
        if (proxyRigidbody != null)
        {
            proxyRigidbody.useGravity = false;
            if (!proxyRigidbody.isKinematic)
            {
                proxyRigidbody.linearVelocity = Vector3.zero;
                proxyRigidbody.angularVelocity = Vector3.zero;
            }
            proxyRigidbody.isKinematic = true;
            proxyRigidbody.detectCollisions = false;
        }
    }

    private void IgnoreNozzleAgainstOwnExtinguisher()
    {
        if (endAnchor == null)
            return;

        Transform extinguisherRoot = networkObject != null
            ? networkObject.transform
            : transform.root;
        Collider[] nozzleColliders = endAnchor.GetComponentsInChildren<Collider>(true);
        Collider[] allColliders = extinguisherRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider nozzleCollider in nozzleColliders)
        {
            if (nozzleCollider == null)
                continue;

            foreach (Collider bodyCollider in allColliders)
            {
                if (bodyCollider == null ||
                    bodyCollider == nozzleCollider ||
                    bodyCollider.transform.IsChildOf(endAnchor))
                {
                    continue;
                }

                Physics.IgnoreCollision(nozzleCollider, bodyCollider, true);
            }
        }
    }

    private bool CanSimulateNozzle()
    {
        if (networkObject == null)
            return true;
        if (networkObject.Runner == null)
            return false;
        return networkObject.HasStateAuthority;
    }

    private void EnforceMaximumDistance()
    {
        if (startAnchor == null || endAnchor == null)
            return;

        Vector3 delta = endAnchor.position - startAnchor.position;
        float distance = delta.magnitude;
        if (distance <= maxLength || distance <= 0.0001f)
            return;

        Vector3 radialDirection = delta / distance;
        endAnchor.position = startAnchor.position + radialDirection * maxLength;
    }

    private void OnNozzlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
            nozzleSelected = true;
        else if (evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
            nozzleSelected = false;
    }

    private void EnsurePointBuffers()
    {
        int count = Mathf.Clamp(visualPointCount, 8, 48);
        if (visualWorldPoints == null || visualWorldPoints.Length != count)
        {
            visualWorldPoints = new Vector3[count];
            visualLocalPoints = new Vector3[count];
        }
    }

    private void RenderVisualHose()
    {
        if (tubeRenderer == null || startAnchor == null || endAnchor == null)
            return;

        EnsurePointBuffers();

        Vector3 start = startAnchor.position;
        Vector3 end = endAnchor.position;
        float endpointDistance = Vector3.Distance(start, end);
        float slack = Mathf.Max(0f, maxLength - Mathf.Min(endpointDistance, maxLength));
        float sag = Mathf.Min(maximumSag, slack * sagPerMetreOfSlack);
        Vector3 control = (start + end) * 0.5f + Vector3.down * sag;

        int last = visualWorldPoints.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            float t = i / (float)last;
            float oneMinusT = 1f - t;
            Vector3 point =
                oneMinusT * oneMinusT * start +
                2f * oneMinusT * t * control +
                t * t * end;

            if (i != 0 && i != last)
                point = PushOutsideWireProxy(point);

            visualWorldPoints[i] = point;
        }

        for (int pass = 0; pass < smoothingPasses; pass++)
        {
            for (int i = 1; i < last; i++)
            {
                Vector3 smoothed =
                    visualWorldPoints[i - 1] * 0.25f +
                    visualWorldPoints[i] * 0.5f +
                    visualWorldPoints[i + 1] * 0.25f;
                visualWorldPoints[i] = PushOutsideWireProxy(smoothed);
            }
        }

        for (int i = 0; i < visualWorldPoints.Length; i++)
            visualLocalPoints[i] = tubeRenderer.transform.InverseTransformPoint(visualWorldPoints[i]);

        tubeRenderer.SetPositions(visualLocalPoints);
    }

    private Vector3 PushOutsideWireProxy(Vector3 worldPoint)
    {
        // The collider is intentionally disabled for PhysX. We only read its
        // authored center and size as a geometric volume for the visual curve.
        if (wireProxy == null || !wireProxy.gameObject.activeInHierarchy)
            return worldPoint;

        Transform proxyTransform = wireProxy.transform;
        Vector3 point = proxyTransform.InverseTransformPoint(worldPoint) - wireProxy.center;
        Vector3 scale = proxyTransform.lossyScale;
        Vector3 padding = new Vector3(
            SafeDivide(proxyPadding, Mathf.Abs(scale.x)),
            SafeDivide(proxyPadding, Mathf.Abs(scale.y)),
            SafeDivide(proxyPadding, Mathf.Abs(scale.z)));
        Vector3 halfSize = wireProxy.size * 0.5f + padding;

        if (Mathf.Abs(point.x) > halfSize.x ||
            Mathf.Abs(point.y) > halfSize.y ||
            Mathf.Abs(point.z) > halfSize.z)
        {
            return worldPoint;
        }

        float distanceX = halfSize.x - Mathf.Abs(point.x);
        float distanceY = halfSize.y - Mathf.Abs(point.y);
        float distanceZ = halfSize.z - Mathf.Abs(point.z);

        if (distanceX <= distanceY && distanceX <= distanceZ)
            point.x = SignedFace(point.x, halfSize.x);
        else if (distanceY <= distanceZ)
            point.y = SignedFace(point.y, halfSize.y);
        else
            point.z = SignedFace(point.z, halfSize.z);

        return proxyTransform.TransformPoint(point + wireProxy.center);
    }

    private static float SignedFace(float value, float extent)
    {
        return (value < 0f ? -1f : 1f) * extent;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return denominator > 0.000001f ? numerator / denominator : numerator;
    }

    private void OnValidate()
    {
        maxLength = Mathf.Max(0.05f, maxLength);
        maximumReturnSpeed = Mathf.Max(0.1f, maximumReturnSpeed);
        returnRotationSpeed = Mathf.Max(0f, returnRotationSpeed);
        dockingDistance = Mathf.Max(0f, dockingDistance);
        visualPointCount = Mathf.Clamp(visualPointCount, 8, 48);
        sagPerMetreOfSlack = Mathf.Max(0f, sagPerMetreOfSlack);
        maximumSag = Mathf.Max(0f, maximumSag);
        proxyPadding = Mathf.Max(0f, proxyPadding);
        smoothingPasses = Mathf.Clamp(smoothingPasses, 0, 3);
    }
}
