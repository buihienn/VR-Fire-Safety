using System;
using Fusion;
using Oculus.Avatar2;
using Unity.Collections;
using UnityEngine;
using StreamLOD = Oculus.Avatar2.OvrAvatarEntity.StreamLOD;

[RequireComponent(typeof(NetworkObject))]
public class NetworkAvatarFusion : NetworkBehaviour
{
    private const int MaxAvatarStreamBytes = ushort.MaxValue;

    [Header("Meta Avatar Entities")]
    [SerializeField] private SampleAvatarEntity localAvatar;
    [SerializeField] private SampleAvatarEntity remoteAvatar;
    [SerializeField] private GameObject debugVisuals;

    [Header("Streaming")]
    [SerializeField] private StreamLOD streamLod = StreamLOD.Medium;
    [SerializeField, Range(5f, 30f)] private float snapshotsPerSecond = 15f;
    [SerializeField, Range(128, 400)] private int rpcChunkBytes = 384;

    [Header("Local Rig Root")]
    [SerializeField] private Transform localRigRoot;
    [SerializeField] private bool followLocalRigRoot = true;

    [Header("Debug")]
    [SerializeField] private bool isLocalAvatar;
    [SerializeField] private int lastStreamByteCount;
    [SerializeField] private int lastStreamChunkCount;

    private SampleInputManager localInputManager;
    private NativeArray<byte> captureBuffer;
    private float snapshotElapsedTime;
    private ushort sendSequence;

    private bool hasReceiveSequence;
    private bool firstPersonLodAssigned;
    private ushort receiveSequence;
    private byte[] receiveBuffer;
    private bool[] receivedChunks;
    private int receiveChunkCount;
    private int receivedChunkCount;
    private byte[] pendingPoseData;

    public void SetLocalSources(SampleInputManager inputManager, Transform rigRoot)
    {
        localInputManager = inputManager;
        localRigRoot = rigRoot;
    }

    private void Awake()
    {
        ResolveAvatarReferences();
        SetAvatarObjectsActive(false, false);
    }

    public override void Spawned()
    {
        ResolveAvatarReferences();
        isLocalAvatar = Object.HasInputAuthority;

        if (isLocalAvatar)
        {
            ResolveLocalSources();

            if (localAvatar != null && localInputManager != null)
            {
                localAvatar.SetInputManager(localInputManager);
            }

            SetAvatarObjectsActive(true, false);
        }
        else
        {
            SetAvatarObjectsActive(false, true);
        }

        if (debugVisuals != null)
        {
            debugVisuals.SetActive(true);
        }

        if (isLocalAvatar && localInputManager == null)
        {
            Debug.LogError(
                $"[{nameof(NetworkAvatarFusion)}] No SampleInputManager was found. " +
                "Add Meta's Avatar SDK Manager prefab to the scene and assign its SampleInputManager to the spawner.",
                this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ClearFirstPersonAvatarLod();
        DisposeCaptureBuffer();
        pendingPoseData = null;
        receiveBuffer = null;
        receivedChunks = null;
    }

    private void LateUpdate()
    {
        if (Object == null)
        {
            return;
        }

        if (Object.HasInputAuthority)
        {
            FollowRigRoot();
            CaptureAndSendPose();

            if (localAvatar != null && localAvatar.HasJoints)
            {
                AssignFirstPersonAvatarLod();

                if (debugVisuals != null)
                {
                    debugVisuals.SetActive(false);
                }
            }

            return;
        }

        ApplyPendingRemotePose();
    }

    private void OnDestroy()
    {
        ClearFirstPersonAvatarLod();
        DisposeCaptureBuffer();
    }

    private void AssignFirstPersonAvatarLod()
    {
        if (firstPersonLodAssigned || localAvatar == null || AvatarLODManager.Instance == null)
        {
            return;
        }

        AvatarLODManager.Instance.firstPersonAvatarLod = localAvatar.AvatarLOD;
        firstPersonLodAssigned = true;
    }

    private void ClearFirstPersonAvatarLod()
    {
        if (!firstPersonLodAssigned || localAvatar == null || AvatarLODManager.Instance == null)
        {
            return;
        }

        if (AvatarLODManager.Instance.firstPersonAvatarLod == localAvatar.AvatarLOD)
        {
            AvatarLODManager.Instance.firstPersonAvatarLod = null;
        }

        firstPersonLodAssigned = false;
    }

    private void CaptureAndSendPose()
    {
        if (localAvatar == null || !localAvatar.HasJoints || snapshotsPerSecond <= 0f)
        {
            return;
        }

        snapshotElapsedTime += Time.unscaledDeltaTime;
        float interval = 1f / snapshotsPerSecond;
        if (snapshotElapsedTime < interval)
        {
            return;
        }

        snapshotElapsedTime %= interval;

        uint byteCount = localAvatar.RecordStreamData_AutoBuffer(streamLod, ref captureBuffer);
        if (byteCount == 0 || byteCount > MaxAvatarStreamBytes)
        {
            if (byteCount > MaxAvatarStreamBytes)
            {
                Debug.LogWarning(
                    $"[{nameof(NetworkAvatarFusion)}] Avatar stream packet is too large ({byteCount} bytes). " +
                    "Use Medium or Low StreamLOD.",
                    this);
            }

            return;
        }

        int totalBytes = (int)byteCount;
        int chunkSize = Mathf.Clamp(rpcChunkBytes, 128, 400);
        int chunkCount = Mathf.CeilToInt(totalBytes / (float)chunkSize);
        if (chunkCount > byte.MaxValue)
        {
            return;
        }

        sendSequence++;
        lastStreamByteCount = totalBytes;
        lastStreamChunkCount = chunkCount;

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int offset = chunkIndex * chunkSize;
            int payloadLength = Mathf.Min(chunkSize, totalBytes - offset);
            byte[] payload = new byte[payloadLength];

            for (int i = 0; i < payloadLength; i++)
            {
                payload[i] = captureBuffer[offset + i];
            }

            RPC_ReceiveAvatarChunk(
                sendSequence,
                (ushort)totalBytes,
                (ushort)offset,
                (byte)chunkIndex,
                (byte)chunkCount,
                payload);
        }
    }

    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.All,
        Channel = RpcChannel.Unreliable,
        TickAligned = false)]
    private void RPC_ReceiveAvatarChunk(
        ushort sequence,
        ushort totalBytes,
        ushort offset,
        byte chunkIndex,
        byte chunkCount,
        byte[] payload)
    {
        if (Object.HasInputAuthority || totalBytes == 0 || chunkCount == 0 ||
            chunkIndex >= chunkCount || payload == null || payload.Length == 0 ||
            offset + payload.Length > totalBytes)
        {
            return;
        }

        if (!hasReceiveSequence || IsNewerSequence(sequence, receiveSequence))
        {
            BeginReceiving(sequence, totalBytes, chunkCount);
        }
        else if (sequence != receiveSequence)
        {
            return;
        }

        if (receiveBuffer == null || receiveBuffer.Length != totalBytes ||
            receivedChunks == null || receiveChunkCount != chunkCount ||
            receivedChunks[chunkIndex])
        {
            return;
        }

        Array.Copy(payload, 0, receiveBuffer, offset, payload.Length);
        receivedChunks[chunkIndex] = true;
        receivedChunkCount++;

        if (receivedChunkCount != receiveChunkCount)
        {
            return;
        }

        pendingPoseData = receiveBuffer;
        receiveBuffer = null;
        receivedChunks = null;
        receiveChunkCount = 0;
        receivedChunkCount = 0;
    }

    private void ApplyPendingRemotePose()
    {
        if (pendingPoseData == null || remoteAvatar == null || !remoteAvatar.IsCreated)
        {
            return;
        }

        NativeArray<byte> streamData = new NativeArray<byte>(pendingPoseData.Length, Unity.Collections.Allocator.Temp);
        try
        {
            for (int i = 0; i < pendingPoseData.Length; i++)
            {
                streamData[i] = pendingPoseData[i];
            }

            NativeSlice<byte> dataSlice = streamData.Slice(0, pendingPoseData.Length);
            remoteAvatar.ApplyStreamData(in dataSlice);
            lastStreamByteCount = pendingPoseData.Length;

            if (debugVisuals != null)
            {
                debugVisuals.SetActive(false);
            }
        }
        finally
        {
            streamData.Dispose();
            pendingPoseData = null;
        }
    }

    private void BeginReceiving(ushort sequence, int totalBytes, int chunkCount)
    {
        hasReceiveSequence = true;
        receiveSequence = sequence;
        receiveBuffer = new byte[totalBytes];
        receivedChunks = new bool[chunkCount];
        receiveChunkCount = chunkCount;
        receivedChunkCount = 0;
    }

    private static bool IsNewerSequence(ushort candidate, ushort current)
    {
        ushort difference = (ushort)(candidate - current);
        return difference != 0 && difference < 32768;
    }

    private void FollowRigRoot()
    {
        if (!followLocalRigRoot || localRigRoot == null || !Object.HasStateAuthority)
        {
            return;
        }

        transform.SetPositionAndRotation(localRigRoot.position, localRigRoot.rotation);
    }

    private void ResolveLocalSources()
    {
        if (localInputManager == null)
        {
            localInputManager = FindFirstObjectByType<SampleInputManager>(FindObjectsInactive.Include);
        }

        if (localRigRoot == null)
        {
            OVRCameraRig cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig != null)
            {
                localRigRoot = cameraRig.transform;
            }
            else if (Camera.main != null)
            {
                localRigRoot = Camera.main.transform.root;
            }
        }
    }

    private void ResolveAvatarReferences()
    {
        SampleAvatarEntity[] avatars = GetComponentsInChildren<SampleAvatarEntity>(true);
        foreach (SampleAvatarEntity avatar in avatars)
        {
            if (avatar == null)
            {
                continue;
            }

            if (localAvatar == null && avatar.name == "AvatarLocal")
            {
                localAvatar = avatar;
            }
            else if (remoteAvatar == null && avatar.name == "AvatarRemote")
            {
                remoteAvatar = avatar;
            }
        }
    }

    private void SetAvatarObjectsActive(bool localActive, bool remoteActive)
    {
        if (localAvatar != null)
        {
            localAvatar.gameObject.SetActive(localActive);
        }

        if (remoteAvatar != null)
        {
            remoteAvatar.gameObject.SetActive(remoteActive);
        }
    }

    private void DisposeCaptureBuffer()
    {
        if (captureBuffer.IsCreated)
        {
            captureBuffer.Dispose();
        }

        captureBuffer = default;
    }
}
