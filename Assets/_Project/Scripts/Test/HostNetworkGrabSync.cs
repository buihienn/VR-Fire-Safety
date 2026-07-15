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

    [Header("Debug - Runtime")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool hasStateAuthority;
    [SerializeField] private bool rigidbodyIsKinematic;

    private bool originalIsKinematic;
    private bool originalUseGravity;
    private bool originalSettingsCached;
    private bool authorityStateInitialized;
    private bool previousHasStateAuthority;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalRigidbodySettings();

        // A previous version disabled NetworkTransform while a client was grabbing.
        // Shared Mode transfers State Authority instead, so NetworkTransform must
        // remain enabled for the new authority to publish its pose.
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }
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
        RefreshDebug();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
        authorityStateInitialized = false;
        RestoreOriginalRigidbodySettings();
        RefreshDebug();
    }

    private void LateUpdate()
    {
        if (!fusionSpawned || Object == null)
        {
            RefreshDebug();
            return;
        }

        ApplyAuthorityPhysics(force: false);
        RefreshDebug();
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

    private void RefreshDebug()
    {
        hasStateAuthority = fusionSpawned && Object != null && Object.HasStateAuthority;
        rigidbodyIsKinematic = targetRigidbody != null && targetRigidbody.isKinematic;
    }
}
