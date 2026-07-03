using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedVrGhost : NetworkBehaviour
{
    [Header("Local Sources")]
    [SerializeField] private Transform localHead;
    [SerializeField] private Transform localLeftHand;
    [SerializeField] private Transform localRightHand;

    [Header("Remote Visuals")]
    [SerializeField] private Transform headVisual;
    [SerializeField] private Transform leftHandVisual;
    [SerializeField] private Transform rightHandVisual;
    [SerializeField] private GameObject[] localOnlyHiddenObjects;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float remotePositionLerp = 24f;
    [SerializeField, Min(0f)] private float remoteRotationLerp = 24f;
    [SerializeField] private bool hideVisualsForLocalPlayer = true;

    [Networked] private Vector3 HeadPosition { get; set; }
    [Networked] private Quaternion HeadRotation { get; set; }
    [Networked] private Vector3 LeftHandPosition { get; set; }
    [Networked] private Quaternion LeftHandRotation { get; set; }
    [Networked] private Vector3 RightHandPosition { get; set; }
    [Networked] private Quaternion RightHandRotation { get; set; }

    private bool visualsConfigured;

    public void SetLocalSources(Transform head, Transform leftHand, Transform rightHand)
    {
        localHead = head;
        localLeftHand = leftHand;
        localRightHand = rightHand;
    }

    public override void Spawned()
    {
        ConfigureVisuals();

        if (Object.HasStateAuthority)
        {
            WriteLocalPoseToNetworkState();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        WriteLocalPoseToNetworkState();
    }

    public override void Render()
    {
        ConfigureVisuals();

        if (Object.HasStateAuthority)
        {
            return;
        }

        ApplyRemotePose();
    }

    private void ConfigureVisuals()
    {
        if (visualsConfigured)
        {
            return;
        }

        bool isLocalGhost = Object != null && Object.HasStateAuthority;
        bool shouldHide = isLocalGhost && hideVisualsForLocalPlayer;

        if (localOnlyHiddenObjects != null)
        {
            foreach (GameObject target in localOnlyHiddenObjects)
            {
                if (target != null)
                {
                    target.SetActive(!shouldHide);
                }
            }
        }

        visualsConfigured = true;
    }

    private void WriteLocalPoseToNetworkState()
    {
        if (localHead != null)
        {
            HeadPosition = localHead.position;
            HeadRotation = localHead.rotation;
        }

        if (localLeftHand != null)
        {
            LeftHandPosition = localLeftHand.position;
            LeftHandRotation = localLeftHand.rotation;
        }

        if (localRightHand != null)
        {
            RightHandPosition = localRightHand.position;
            RightHandRotation = localRightHand.rotation;
        }
    }

    private void ApplyRemotePose()
    {
        float positionT = remotePositionLerp <= 0f ? 1f : 1f - Mathf.Exp(-remotePositionLerp * Time.deltaTime);
        float rotationT = remoteRotationLerp <= 0f ? 1f : 1f - Mathf.Exp(-remoteRotationLerp * Time.deltaTime);

        ApplyPose(headVisual, HeadPosition, SafeRotation(HeadRotation), positionT, rotationT);
        ApplyPose(leftHandVisual, LeftHandPosition, SafeRotation(LeftHandRotation), positionT, rotationT);
        ApplyPose(rightHandVisual, RightHandPosition, SafeRotation(RightHandRotation), positionT, rotationT);
    }

    private static void ApplyPose(Transform target, Vector3 position, Quaternion rotation, float positionT, float rotationT)
    {
        if (target == null)
        {
            return;
        }

        target.position = Vector3.Lerp(target.position, position, positionT);
        target.rotation = Quaternion.Slerp(target.rotation, rotation, rotationT);
    }

    private static Quaternion SafeRotation(Quaternion rotation)
    {
        float length = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        return length < 0.0001f ? Quaternion.identity : rotation;
    }
}
