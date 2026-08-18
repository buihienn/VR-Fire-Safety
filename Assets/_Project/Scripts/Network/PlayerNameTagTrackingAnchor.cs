using System.Reflection;
using Meta.XR.MultiplayerBlocks.Fusion;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
internal sealed class PlayerNameTagTrackingAnchor : MonoBehaviour
{
    private const float RebindInterval = 0.25f;

    private static readonly FieldInfo CenterEyeField = typeof(PlayerNameTagFusion).GetField(
        "_centerEye",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static PlayerNameTagTrackingAnchor instance;
    private float nextRebindTime;
    private bool missingFieldReported;

    public static void Bind(PlayerNameTagFusion nameTag)
    {
        if (nameTag == null)
        {
            return;
        }

        PlayerNameTagTrackingAnchor anchor = GetOrCreate();
        anchor.BindInternal(nameTag);
    }

    private static PlayerNameTagTrackingAnchor GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject anchorObject = new GameObject("[Runtime] Player Name Tag Tracking Anchor");
        instance = anchorObject.AddComponent<PlayerNameTagTrackingAnchor>();
        DontDestroyOnLoad(anchorObject);
        instance.FollowLocalView();
        return instance;
    }

    private void Update()
    {
        FollowLocalView();

        if (Time.unscaledTime < nextRebindTime)
        {
            return;
        }

        nextRebindTime = Time.unscaledTime + RebindInterval;
        PlayerNameTagFusion[] nameTags = FindObjectsByType<PlayerNameTagFusion>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PlayerNameTagFusion nameTag in nameTags)
        {
            BindInternal(nameTag);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void BindInternal(PlayerNameTagFusion nameTag)
    {
        if (CenterEyeField != null)
        {
            CenterEyeField.SetValue(nameTag, transform);
            return;
        }

        if (!missingFieldReported)
        {
            missingFieldReported = true;
            Debug.LogError("Player Name Tag: Meta's _centerEye field could not be found.", this);
        }
    }

    private void FollowLocalView()
    {
        Transform view = FindLocalView();
        if (view != null && view != transform)
        {
            transform.SetPositionAndRotation(view.position, view.rotation);
        }
    }

    private static Transform FindLocalView()
    {
        if (OVRManager.instance != null)
        {
            OVRCameraRig cameraRig = OVRManager.instance.GetComponentInChildren<OVRCameraRig>(true);
            if (cameraRig != null && cameraRig.centerEyeAnchor != null)
            {
                return cameraRig.centerEyeAnchor;
            }
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }
}
