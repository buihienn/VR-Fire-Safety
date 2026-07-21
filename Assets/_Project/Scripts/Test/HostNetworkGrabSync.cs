using Fusion;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Keeps a Meta grabbable Rigidbody compatible with Fusion Shared Mode.
///
/// The legacy class name is intentionally preserved so existing prefab/script GUID
/// references remain valid. Pose replication and ownership transfer are handled by
/// Fusion's NetworkTransform and Meta's TransferOwnershipFusion respectively.
/// This component only prevents non-authority peers from simulating the same
/// Rigidbody locally while NetworkTransform is rendering the replicated pose.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(Rigidbody))]
public sealed class HostNetworkGrabSync : NetworkBehaviour
{
    [Header("Shared Mode References")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private Grabbable targetGrabbable;

    [Header("Remote Physics")]
    [Tooltip("Non-authority peers stay kinematic so local physics cannot fight NetworkTransform interpolation.")]
    [SerializeField] private bool disablePhysicsWithoutStateAuthority = true;

    [Tooltip("Clear velocity when this peer loses State Authority.")]
    [SerializeField] private bool zeroVelocityOnAuthorityLoss = true;

    [Header("Optional Secondary Grab - Hose Nozzle")]
    [Tooltip("Sync a child Rigidbody such as WireBuilder/EndAnchor without adding a second NetworkObject.")]
    [SerializeField] private bool syncSecondaryTransform;
    [SerializeField] private Transform secondaryTransform;
    [SerializeField] private Rigidbody secondaryRigidbody;
    [SerializeField] private Grabbable secondaryGrabbable;

    [Tooltip("The client hovering or selecting the nozzle requests authority of the extinguisher root.")]
    [SerializeField] private bool requestAuthorityOnSecondarySelect = true;

    [Min(0f)]
    [SerializeField] private float secondaryRemoteFollowSpeed = 25f;

    [Networked] private Vector3 SecondaryLocalPositionNet { get; set; }
    [Networked] private Quaternion SecondaryLocalRotationNet { get; set; }
    [Networked] private NetworkBool SecondaryPoseInitializedNet { get; set; }

    [Header("Debug - Runtime")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool hasStateAuthority;
    [SerializeField] private bool rigidbodyIsKinematic;

    private bool originalIsKinematic;
    private bool originalUseGravity;
    private bool originalSettingsCached;
    private bool authorityStateInitialized;
    private bool previousHasStateAuthority;
    private bool secondaryLocallySelected;
    private bool secondaryOriginalIsKinematic;
    private bool secondaryOriginalUseGravity;
    private bool secondarySettingsCached;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalRigidbodySettings();
        CacheSecondaryRigidbodySettings();

        // A previous version disabled NetworkTransform while a client was grabbing.
        // Shared Mode transfers State Authority instead, so NetworkTransform must
        // remain enabled for the new authority to publish its pose.
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }
    }

    private void OnEnable()
    {
        if (secondaryGrabbable != null)
            secondaryGrabbable.WhenPointerEventRaised += OnSecondaryPointerEvent;
    }

    private void OnDisable()
    {
        if (secondaryGrabbable != null)
            secondaryGrabbable.WhenPointerEventRaised -= OnSecondaryPointerEvent;
    }

    public override void Spawned()
    {
        fusionSpawned = true;
        authorityStateInitialized = false;

        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }

        ApplyAuthorityPhysics(force: true);

        if (syncSecondaryTransform && Object.HasStateAuthority)
            WriteSecondaryPose();

        ApplySecondaryAuthorityPhysics();
        RefreshDebug();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
        authorityStateInitialized = false;
        RestoreOriginalRigidbodySettings();
        RestoreSecondaryRigidbodySettings();
        RefreshDebug();
    }

    public override void FixedUpdateNetwork()
    {
        if (!syncSecondaryTransform || !Object.HasStateAuthority)
            return;

        WriteSecondaryPose();
    }

    public override void Render()
    {
        if (!syncSecondaryTransform || secondaryTransform == null)
            return;

        if (Object == null || Object.HasStateAuthority || secondaryLocallySelected)
            return;

        if (!SecondaryPoseInitializedNet)
            return;

        // A kinematic Rigidbody is followed from FixedUpdate so Unity can
        // interpolate it together with the physical hose chain. Keep this direct
        // Transform path only as a fallback when no secondary Rigidbody exists.
        if (secondaryRigidbody != null)
            return;

        float follow01 = 1f - Mathf.Exp(-secondaryRemoteFollowSpeed * Time.deltaTime);
        secondaryTransform.localPosition = Vector3.Lerp(
            secondaryTransform.localPosition,
            SecondaryLocalPositionNet,
            follow01);
        secondaryTransform.localRotation = Quaternion.Slerp(
            secondaryTransform.localRotation,
            SecondaryLocalRotationNet,
            follow01);
    }

    private void FixedUpdate()
    {
        if (!syncSecondaryTransform ||
            !fusionSpawned ||
            Object == null ||
            Object.HasStateAuthority ||
            secondaryLocallySelected ||
            secondaryTransform == null ||
            secondaryRigidbody == null ||
            !SecondaryPoseInitializedNet)
        {
            return;
        }

        secondaryRigidbody.useGravity = false;
        secondaryRigidbody.isKinematic = true;

        Transform parent = secondaryTransform.parent;
        Vector3 targetPosition = parent != null
            ? parent.TransformPoint(SecondaryLocalPositionNet)
            : SecondaryLocalPositionNet;
        Quaternion targetRotation = parent != null
            ? parent.rotation * SecondaryLocalRotationNet
            : SecondaryLocalRotationNet;

        float follow01 = 1f - Mathf.Exp(-secondaryRemoteFollowSpeed * Time.fixedDeltaTime);
        secondaryRigidbody.MovePosition(Vector3.Lerp(
            secondaryRigidbody.position,
            targetPosition,
            follow01));
        secondaryRigidbody.MoveRotation(Quaternion.Slerp(
            secondaryRigidbody.rotation,
            targetRotation,
            follow01));
    }

    private void LateUpdate()
    {
        if (!fusionSpawned || Object == null)
        {
            RefreshDebug();
            return;
        }

        ApplyAuthorityPhysics(force: false);
        ApplySecondaryAuthorityPhysics();
        RefreshDebug();
    }

    private void OnSecondaryPointerEvent(PointerEvent evt)
    {
        // Request on Hover as well as Select. Requesting only on Select creates a
        // deadlock on remote peers: their nozzle Rigidbody is kinematic until they
        // own the root, but ownership was previously requested only after the grab
        // had already begun.
        if (evt.Type == PointerEventType.Hover)
        {
            TryRequestSecondaryAuthority();
        }
        else if (evt.Type == PointerEventType.Select)
        {
            secondaryLocallySelected = true;
            TryRequestSecondaryAuthority();
        }
        else if (evt.Type == PointerEventType.Unselect ||
                 evt.Type == PointerEventType.Cancel)
        {
            secondaryLocallySelected = false;
        }
    }

    private void TryRequestSecondaryAuthority()
    {
        if (!requestAuthorityOnSecondarySelect ||
            !fusionSpawned ||
            Object == null ||
            Object.HasStateAuthority)
        {
            return;
        }

        Object.RequestStateAuthority();
    }

    private void WriteSecondaryPose()
    {
        if (secondaryTransform == null)
            return;

        SecondaryLocalPositionNet = secondaryTransform.localPosition;
        SecondaryLocalRotationNet = secondaryTransform.localRotation;
        SecondaryPoseInitializedNet = true;
    }

    private void ApplySecondaryAuthorityPhysics()
    {
        if (!syncSecondaryTransform || secondaryRigidbody == null || Object == null)
            return;

        if (Object.HasStateAuthority)
        {
            secondaryRigidbody.useGravity = secondaryOriginalUseGravity;
            secondaryRigidbody.isKinematic =
                secondaryLocallySelected || secondaryOriginalIsKinematic;
            return;
        }

        secondaryRigidbody.useGravity = false;
        secondaryRigidbody.isKinematic = true;
    }

    private void ApplyAuthorityPhysics(bool force)
    {
        if (targetRigidbody == null || Object == null)
        {
            return;
        }

        bool ownsState = Object.HasStateAuthority;
        if (!force && authorityStateInitialized && ownsState == previousHasStateAuthority)
        {
            return;
        }

        authorityStateInitialized = true;
        previousHasStateAuthority = ownsState;

        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }

        if (ownsState || !disablePhysicsWithoutStateAuthority)
        {
            targetRigidbody.useGravity = originalUseGravity;

            // Meta's Grabbable expects a selected object to remain kinematic.
            // TransferOwnershipOnSelect will maintain the same state afterwards.
            bool isLocallySelected = targetGrabbable != null &&
                                     targetGrabbable.SelectingPointsCount > 0;
            targetRigidbody.isKinematic = isLocallySelected || originalIsKinematic;
            return;
        }

        if (zeroVelocityOnAuthorityLoss && !targetRigidbody.isKinematic)
        {
            targetRigidbody.linearVelocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
        }

        targetRigidbody.useGravity = false;
        targetRigidbody.isKinematic = true;
    }

    private void ResolveReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        if (networkTransform == null)
        {
            networkTransform = GetComponent<NetworkTransform>();
        }

        if (targetGrabbable == null)
        {
            targetGrabbable = GetComponentInChildren<Grabbable>(true);
        }

        if (secondaryTransform != null)
        {
            if (secondaryRigidbody == null)
                secondaryRigidbody = secondaryTransform.GetComponent<Rigidbody>();

            if (secondaryGrabbable == null)
                secondaryGrabbable = secondaryTransform.GetComponent<Grabbable>();
        }
    }

    private void CacheSecondaryRigidbodySettings()
    {
        if (secondaryRigidbody == null || secondarySettingsCached)
            return;

        secondaryOriginalIsKinematic = secondaryRigidbody.isKinematic;
        secondaryOriginalUseGravity = secondaryRigidbody.useGravity;
        secondarySettingsCached = true;
    }

    private void CacheOriginalRigidbodySettings()
    {
        if (targetRigidbody == null || originalSettingsCached)
        {
            return;
        }

        originalIsKinematic = targetRigidbody.isKinematic;
        originalUseGravity = targetRigidbody.useGravity;
        originalSettingsCached = true;
    }

    private void RestoreOriginalRigidbodySettings()
    {
        if (targetRigidbody == null || !originalSettingsCached)
        {
            return;
        }

        targetRigidbody.useGravity = originalUseGravity;
        targetRigidbody.isKinematic = originalIsKinematic;
    }

    private void RestoreSecondaryRigidbodySettings()
    {
        if (secondaryRigidbody == null || !secondarySettingsCached)
            return;

        secondaryRigidbody.useGravity = secondaryOriginalUseGravity;
        secondaryRigidbody.isKinematic = secondaryOriginalIsKinematic;
    }

    private void OnValidate()
    {
        secondaryRemoteFollowSpeed = Mathf.Max(0f, secondaryRemoteFollowSpeed);
    }

    private void RefreshDebug()
    {
        hasStateAuthority = fusionSpawned && Object != null && Object.HasStateAuthority;
        rigidbodyIsKinematic = targetRigidbody != null && targetRigidbody.isKinematic;
    }
}
