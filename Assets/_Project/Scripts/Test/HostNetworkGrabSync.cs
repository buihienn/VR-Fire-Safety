using System.Collections;
using Fusion;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Host-authoritative grab bridge for Meta Interaction SDK + Photon Fusion Host Mode.
///
/// Meta HandGrab still moves the object locally for responsiveness.
/// A client temporarily disables its local NetworkTransform while holding the object,
/// streams the resulting pose to the Host, and the Host applies that pose as the
/// authoritative state. The Host's NetworkTransform then replicates it to everyone.
///
/// The component listens to the root Meta Grabbable automatically.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class HostNetworkGrabSync : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Rigidbody on the same root that Meta Grabbable moves.")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Fusion NetworkTransform on this same root.")]
    [SerializeField] private NetworkTransform networkTransform;

    [Tooltip("Meta Grabbable that moves this object. It is found automatically when empty.")]
    [SerializeField] private Grabbable targetGrabbable;

    [Header("Pose Streaming")]
    [Tooltip("How many pose packets per second a grabbing client sends to the Host.")]
    [SerializeField, Min(1f)] private float poseSendRate = 30f;

    [Tooltip("Delay before a client re-enables NetworkTransform after release, allowing the final Host pose to arrive.")]
    [SerializeField, Min(0f)] private float reenableNetworkTransformDelay = 0.12f;

    [Header("Physics")]
    [Tooltip("Restore the Host Rigidbody's original Is Kinematic value after release.")]
    [SerializeField] private bool restoreOriginalKinematic = true;

    [Tooltip("This first version does not network throwing. Clear velocity after release for stability.")]
    [SerializeField] private bool zeroVelocityOnRelease = true;

    [Header("Debug - Runtime")]
    [SerializeField] private bool fusionSpawned;
    [SerializeField] private bool localGrabActive;
    [SerializeField] private int localSelectCount;
    [SerializeField] private bool hasStateAuthority;
    [SerializeField] private bool isGrabbedNetworked;
    [SerializeField] private string grabberDebug = "None";

    [Networked] public NetworkBool IsGrabbedNet { get; private set; }
    [Networked] public PlayerRef GrabberNet { get; private set; }

    private Vector3 authorityTargetPosition;
    private Quaternion authorityTargetRotation;
    private bool authorityHasTarget;

    private bool authorityOriginalKinematic;
    private bool authorityKinematicWasCached;

    private float nextPoseSendTime;
    private Coroutine reenableRoutine;
    private bool pointerEventsSubscribed;

    private void Reset()
    {
        targetRigidbody = GetComponent<Rigidbody>();
        networkTransform = GetComponent<NetworkTransform>();
        targetGrabbable = GetComponent<Grabbable>();
    }

    private void Awake()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();

        if (networkTransform == null)
            networkTransform = GetComponent<NetworkTransform>();

        if (targetGrabbable == null)
            targetGrabbable = GetComponent<Grabbable>();
    }

    private void OnEnable()
    {
        SubscribeToPointerEvents();
    }

    private void Start()
    {
        // Some Meta building blocks finish wiring their references after Awake.
        SubscribeToPointerEvents();
    }

    private void SubscribeToPointerEvents()
    {
        if (pointerEventsSubscribed)
            return;

        if (targetGrabbable == null)
            targetGrabbable = GetComponent<Grabbable>();

        if (targetGrabbable == null)
        {
            Debug.LogError("[HostNetworkGrabSync] No Meta Grabbable was found on the network object root.", this);
            return;
        }

        targetGrabbable.WhenPointerEventRaised += OnPointerEvent;
        pointerEventsSubscribed = true;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
            BeginLocalGrab();
        else if (evt.Type == PointerEventType.Unselect)
            EndLocalGrab();
    }

    public override void Spawned()
    {
        fusionSpawned = true;

        if (Object.HasStateAuthority)
        {
            IsGrabbedNet = false;
            GrabberNet = PlayerRef.None;
            authorityHasTarget = false;
        }

        RefreshDebug();
    }

    private void LateUpdate()
    {
        if (!fusionSpawned || !localGrabActive)
            return;

        // Host is already State Authority. Meta can move the root directly,
        // and NetworkTransform will replicate it. Cache the pose only for debug/fallback.
        if (Object.HasStateAuthority)
        {
            authorityTargetPosition = transform.position;
            authorityTargetRotation = transform.rotation;
            authorityHasTarget = true;
            return;
        }

        if (Time.unscaledTime < nextPoseSendTime)
            return;

        nextPoseSendTime = Time.unscaledTime + 1f / Mathf.Max(1f, poseSendRate);
        RPC_SendGrabPose(transform.position, transform.rotation);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !IsGrabbedNet || !authorityHasTarget)
            return;

        // When the Host itself is grabbing, Meta already drives this transform.
        // Do not fight Meta with a second movement write.
        bool hostIsTheLocalGrabber = localGrabActive &&
                                     Runner != null &&
                                     GrabberNet == Runner.LocalPlayer;

        if (hostIsTheLocalGrabber)
            return;

        if (targetRigidbody != null)
        {
            targetRigidbody.MovePosition(authorityTargetPosition);
            targetRigidbody.MoveRotation(authorityTargetRotation);
        }
        else
        {
            transform.SetPositionAndRotation(authorityTargetPosition, authorityTargetRotation);
        }
    }

    private void Update()
    {
        RefreshDebug();
    }

    /// <summary>
    /// Starts local grab streaming. This is called automatically from the Meta Grabbable.
    /// </summary>
    public void BeginLocalGrab()
    {
        localSelectCount++;

        // Two wrappers can point to this script (left/right). Only start once.
        if (localSelectCount > 1)
            return;

        localGrabActive = true;
        nextPoseSendTime = 0f;

        StopReenableRoutine();

        if (!fusionSpawned)
            return;

        // On a non-authority client, Meta must be allowed to move the object locally.
        // Otherwise NetworkTransform immediately overwrites the local hand movement.
        if (!Object.HasStateAuthority && networkTransform != null)
            networkTransform.enabled = false;

        RPC_RequestGrab(transform.position, transform.rotation);
    }

    /// <summary>
    /// Stops local grab streaming. This is called automatically from the Meta Grabbable.
    /// </summary>
    public void EndLocalGrab()
    {
        localSelectCount = Mathf.Max(0, localSelectCount - 1);

        // Do not release until all local selecting interactables have released.
        if (localSelectCount > 0)
            return;

        if (!localGrabActive)
            return;

        localGrabActive = false;

        if (!fusionSpawned)
            return;

        RPC_RequestRelease(transform.position, transform.rotation);

        if (!Object.HasStateAuthority && networkTransform != null)
        {
            StopReenableRoutine();
            reenableRoutine = StartCoroutine(ReenableNetworkTransformAfterDelay());
        }
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority,
        Channel = RpcChannel.Reliable,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_RequestGrab(
        Vector3 startPosition,
        Quaternion startRotation,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority)
            return;

        PlayerRef requester = ResolveRequester(info.Source);

        // First requester wins. The same requester may safely repeat the request.
        if (IsGrabbedNet && GrabberNet != requester)
            return;

        IsGrabbedNet = true;
        GrabberNet = requester;

        authorityTargetPosition = startPosition;
        authorityTargetRotation = startRotation;
        authorityHasTarget = true;

        bool requesterIsHostLocal = Runner != null && requester == Runner.LocalPlayer;
        if (!requesterIsHostLocal)
            SetAuthorityGrabPhysics(true);
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority,
        Channel = RpcChannel.Unreliable,
        TickAligned = false,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_SendGrabPose(
        Vector3 position,
        Quaternion rotation,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !IsGrabbedNet)
            return;

        PlayerRef sender = ResolveRequester(info.Source);
        if (sender != GrabberNet)
            return;

        authorityTargetPosition = position;
        authorityTargetRotation = rotation;
        authorityHasTarget = true;
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority,
        Channel = RpcChannel.Reliable,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_RequestRelease(
        Vector3 finalPosition,
        Quaternion finalRotation,
        RpcInfo info = default)
    {
        if (!Object.HasStateAuthority || !IsGrabbedNet)
            return;

        PlayerRef sender = ResolveRequester(info.Source);
        if (sender != GrabberNet)
            return;

        authorityTargetPosition = finalPosition;
        authorityTargetRotation = finalRotation;
        authorityHasTarget = true;

        // Apply the final pose immediately before releasing physics.
        if (targetRigidbody != null)
        {
            targetRigidbody.position = finalPosition;
            targetRigidbody.rotation = finalRotation;
        }
        else
        {
            transform.SetPositionAndRotation(finalPosition, finalRotation);
        }

        IsGrabbedNet = false;
        GrabberNet = PlayerRef.None;
        authorityHasTarget = false;

        SetAuthorityGrabPhysics(false);
    }

    private PlayerRef ResolveRequester(PlayerRef rpcSource)
    {
        if (rpcSource != PlayerRef.None)
            return rpcSource;

        // Defensive fallback for the Host's server-side invocation.
        if (Runner != null && Runner.LocalPlayer != PlayerRef.None)
            return Runner.LocalPlayer;

        return PlayerRef.None;
    }

    private void SetAuthorityGrabPhysics(bool grabbed)
    {
        if (targetRigidbody == null)
            return;

        if (grabbed)
        {
            authorityOriginalKinematic = targetRigidbody.isKinematic;
            authorityKinematicWasCached = true;
            targetRigidbody.isKinematic = true;
            return;
        }

        if (restoreOriginalKinematic && authorityKinematicWasCached)
            targetRigidbody.isKinematic = authorityOriginalKinematic;

        authorityKinematicWasCached = false;

        // Never write velocity to a kinematic Rigidbody.
        if (zeroVelocityOnRelease && !targetRigidbody.isKinematic)
        {
            targetRigidbody.linearVelocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private IEnumerator ReenableNetworkTransformAfterDelay()
    {
        if (reenableNetworkTransformDelay > 0f)
            yield return new WaitForSecondsRealtime(reenableNetworkTransformDelay);
        else
            yield return null;

        if (networkTransform != null)
            networkTransform.enabled = true;

        reenableRoutine = null;
    }

    private void StopReenableRoutine()
    {
        if (reenableRoutine == null)
            return;

        StopCoroutine(reenableRoutine);
        reenableRoutine = null;
    }

    private void OnDisable()
    {
        if (pointerEventsSubscribed && targetGrabbable != null)
        {
            targetGrabbable.WhenPointerEventRaised -= OnPointerEvent;
            pointerEventsSubscribed = false;
        }

        StopReenableRoutine();

        localSelectCount = 0;
        localGrabActive = false;

        if (networkTransform != null)
            networkTransform.enabled = true;
    }

    private void RefreshDebug()
    {
        hasStateAuthority = fusionSpawned && Object != null && Object.HasStateAuthority;
        isGrabbedNetworked = fusionSpawned && IsGrabbedNet;

        if (!fusionSpawned)
        {
            grabberDebug = "Not Spawned";
        }
        else if (GrabberNet == PlayerRef.None)
        {
            grabberDebug = "None";
        }
        else
        {
            grabberDebug = $"Player_{GrabberNet.PlayerId}";
        }
    }
}
