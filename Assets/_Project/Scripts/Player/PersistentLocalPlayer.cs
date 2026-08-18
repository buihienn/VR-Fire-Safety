using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using System.Collections;
using MetaCharacterController = Oculus.Interaction.Locomotion.CharacterController;
using MetaFirstPersonLocomotor = Oculus.Interaction.Locomotion.FirstPersonLocomotor;
using UnityCharacterController = UnityEngine.CharacterController;

public class PersistentLocalPlayer : MonoBehaviour
{
    private const string TutorialQuickMenuObjectName = "TutorialQuickMenu";

    [SerializeField] private string lobbySceneName = "StartScene";
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private string endGameSceneName = "EndGameScene";
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    [Header("Scene Player Spawn Markers")]
    [SerializeField] private string lobbyPlayerMarkerName = "StartScenePlayerSpawn";
    [SerializeField] private string gamePlayerMarkerName = "MainScenePlayerSpawn";
    [SerializeField] private string endGamePlayerMarkerName = "EndGameScenePlayerSpawn";
    [SerializeField] private string tutorialPlayerMarkerName = "TutorialScenePlayerSpawn";
    [SerializeField] private Vector3 spawnPositionOffset;
    [SerializeField, Min(0)] private int sceneReadyDelayFrames = 3;
    [SerializeField, Min(0f)] private float sceneReadyDelaySeconds = 0.5f;
    [SerializeField] private bool snapSpawnToGround = true;
    [SerializeField, Min(0f)] private float groundRaycastHeight = 2f;
    [SerializeField, Min(0f)] private float groundRaycastDistance = 8f;
    [SerializeField, Min(0f)] private float groundOffset = 0.05f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private bool disableGravityForPlayerRigidbodies = true;
    [SerializeField] private bool makePlayerRigidbodiesKinematic = true;
    [SerializeField, Min(0f)] private float headBlockingResumeDelaySeconds = 0.25f;

    [Header("VR Rig Reset")]
    [Tooltip("Root transform moved by the Meta locomotion system. If empty, the OVRCameraRig is found automatically.")]
    [SerializeField] private Transform playerRigRoot;

    private static PersistentLocalPlayer instance;
    private Coroutine sceneReadyRoutine;
    private Vector3 initialPlayerRigLocalPosition;
    private Quaternion initialPlayerRigLocalRotation;
    private Vector3 initialPlayerRigLocalScale;
    private bool hasInitialPlayerRigPose;
    private GameObject tutorialQuickMenu;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveTutorialQuickMenu();
        UpdateTutorialQuickMenuVisibility(SceneManager.GetActiveScene());
        CacheInitialPlayerRigPose();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateTutorialQuickMenuVisibility(scene);

        if (!TryGetScenePlayerMarkerName(scene.name, out string markerName))
        {
            return;
        }

        SetMetaLocomotorsMovement(GetComponentsInChildren<MetaFirstPersonLocomotor>(true), false);

        if (sceneReadyRoutine != null)
        {
            StopCoroutine(sceneReadyRoutine);
        }

        sceneReadyRoutine = StartCoroutine(PlacePlayerWhenSceneIsReady(scene, markerName));
    }

    private IEnumerator PlacePlayerWhenSceneIsReady(Scene scene, string markerName)
    {
        PreserveNetworkRunners();
        SceneManager.SetActiveScene(scene);

        for (int i = 0; i < sceneReadyDelayFrames; i++)
        {
            yield return null;
        }

        if (sceneReadyDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(sceneReadyDelaySeconds);
        }

        Physics.SyncTransforms();

        Transform marker = FindSceneTransform(scene, markerName);
        if (marker == null || marker == transform)
        {
            Debug.LogWarning(
                $"[{nameof(PersistentLocalPlayer)}] Could not find player spawn marker " +
                $"'{markerName}' in scene '{scene.name}'.");
        }
        else
        {
            MovePlayerTo(marker);
            Debug.Log(
                $"[{nameof(PersistentLocalPlayer)}] Placed player in {scene.name} at marker " +
                $"{marker.name}: {transform.position}.");
        }

        if (scene.name != lobbySceneName)
        {
            yield return UnloadLobbySceneAfterFrame();
        }

        sceneReadyRoutine = null;
    }

    private bool TryGetScenePlayerMarkerName(string sceneName, out string markerName)
    {
        if (sceneName == lobbySceneName)
        {
            markerName = lobbyPlayerMarkerName;
            return true;
        }

        if (sceneName == gameSceneName)
        {
            markerName = gamePlayerMarkerName;
            return true;
        }

        if (sceneName == endGameSceneName)
        {
            markerName = endGamePlayerMarkerName;
            return true;
        }

        if (sceneName == tutorialSceneName)
        {
            markerName = tutorialPlayerMarkerName;
            return true;
        }

        markerName = null;
        return false;
    }

    private void MovePlayerTo(Transform marker)
    {
        Vector3 spawnPosition = marker.position + spawnPositionOffset;
        if (snapSpawnToGround)
        {
            spawnPosition = SnapPositionToGround(spawnPosition);
        }

        MetaFirstPersonLocomotor[] metaLocomotors = GetComponentsInChildren<MetaFirstPersonLocomotor>(true);
        SetMetaLocomotorsMovement(metaLocomotors, false);

        UnityCharacterController[] characterControllers = GetComponentsInChildren<UnityCharacterController>(true);
        SetCharacterControllersEnabled(characterControllers, false);

        MetaCharacterController[] metaCharacterControllers = GetComponentsInChildren<MetaCharacterController>(true);
        ApplySpawnPose(spawnPosition, marker.rotation, metaCharacterControllers, metaLocomotors);

        StabilizePlayerRigidbodies();
        ResetHeadBlockingAfterTeleport();

        StartCoroutine(ReenableControllersAfterTeleport(
            characterControllers,
            metaLocomotors,
            metaCharacterControllers,
            spawnPosition,
            marker.rotation));
    }

    private void CacheInitialPlayerRigPose()
    {
        if (playerRigRoot == null)
        {
            OVRCameraRig cameraRig = GetComponentInChildren<OVRCameraRig>(true);
            if (cameraRig != null)
            {
                playerRigRoot = cameraRig.transform;
            }
        }

        if (playerRigRoot == null || playerRigRoot == transform)
        {
            return;
        }

        initialPlayerRigLocalPosition = playerRigRoot.localPosition;
        initialPlayerRigLocalRotation = playerRigRoot.localRotation;
        initialPlayerRigLocalScale = playerRigRoot.localScale;
        hasInitialPlayerRigPose = true;
    }

    private void RestoreInitialPlayerRigPose()
    {
        if (!hasInitialPlayerRigPose || playerRigRoot == null)
        {
            return;
        }

        playerRigRoot.SetLocalPositionAndRotation(initialPlayerRigLocalPosition, initialPlayerRigLocalRotation);
        playerRigRoot.localScale = initialPlayerRigLocalScale;
    }

    private void ApplySpawnPose(
        Vector3 feetPosition,
        Quaternion rotation,
        MetaCharacterController[] metaCharacterControllers,
        MetaFirstPersonLocomotor[] metaLocomotors)
    {
        RestoreInitialPlayerRigPose();
        transform.SetPositionAndRotation(feetPosition, rotation);
        Physics.SyncTransforms();

        ResetMetaCharacterControllers(metaCharacterControllers, feetPosition, rotation);
        ResetMetaLocomotorsToCharacter(metaLocomotors);
        Physics.SyncTransforms();
    }

    private Vector3 SnapPositionToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundRaycastHeight;
        float maxDistance = groundRaycastHeight + groundRaycastDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + groundOffset;
        }
        else
        {
            Debug.LogWarning($"[{nameof(PersistentLocalPlayer)}] No ground collider found below spawn position {position}.");
        }

        return position;
    }

    private void ResetHeadBlockingAfterTeleport()
    {
        MetaXRHeadBlocking[] blockers = GetComponentsInChildren<MetaXRHeadBlocking>(true);
        foreach (MetaXRHeadBlocking blocker in blockers)
        {
            if (blocker != null)
            {
                blocker.ResetAfterTeleport(headBlockingResumeDelaySeconds);
            }
        }
    }

    private static void SetCharacterControllersEnabled(UnityCharacterController[] characterControllers, bool enabled)
    {
        foreach (UnityCharacterController characterController in characterControllers)
        {
            if (characterController != null)
            {
                characterController.enabled = enabled;
            }
        }
    }

    private void ResetMetaCharacterControllers(MetaCharacterController[] characterControllers, Vector3 feetPosition, Quaternion rotation)
    {
        foreach (MetaCharacterController characterController in characterControllers)
        {
            if (characterController == null)
            {
                continue;
            }

            characterController.SetRotation(rotation);

            float capsuleCenterHeight = characterController.Height * 0.5f + characterController.SkinWidth;
            characterController.SetPosition(feetPosition + Vector3.up * capsuleCenterHeight);

            if (!characterController.TryGround(characterController.MaxStep))
            {
                Debug.LogWarning(
                    $"[{nameof(PersistentLocalPlayer)}] Meta locomotion controller could not find ground below {feetPosition}. " +
                    "Check the target scene floor collider and the PlayerController locomotion Layer Mask.");
            }
        }
    }

    private static void SetMetaLocomotorsMovement(MetaFirstPersonLocomotor[] locomotors, bool enabled)
    {
        foreach (MetaFirstPersonLocomotor locomotor in locomotors)
        {
            if (locomotor == null)
            {
                continue;
            }

            if (enabled)
            {
                locomotor.EnableMovement();
            }
            else
            {
                locomotor.DisableMovement();
            }
        }
    }

    private static void ResetMetaLocomotorsToCharacter(MetaFirstPersonLocomotor[] locomotors)
    {
        foreach (MetaFirstPersonLocomotor locomotor in locomotors)
        {
            if (locomotor != null)
            {
                locomotor.ResetPlayerToCharacter();
            }
        }
    }

    private IEnumerator ReenableControllersAfterTeleport(
        UnityCharacterController[] characterControllers,
        MetaFirstPersonLocomotor[] metaLocomotors,
        MetaCharacterController[] metaCharacterControllers,
        Vector3 feetPosition,
        Quaternion rotation)
    {
        yield return null;

        ApplySpawnPose(feetPosition, rotation, metaCharacterControllers, metaLocomotors);

        yield return new WaitForFixedUpdate();

        SetCharacterControllersEnabled(characterControllers, true);
        ApplySpawnPose(feetPosition, rotation, metaCharacterControllers, metaLocomotors);
        SetMetaLocomotorsMovement(metaLocomotors, true);
    }

    private void StabilizePlayerRigidbodies()
    {
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (disableGravityForPlayerRigidbodies)
            {
                rb.useGravity = false;
            }

            if (makePlayerRigidbodiesKinematic)
            {
                rb.isKinematic = true;
            }
        }
    }

    private IEnumerator UnloadLobbySceneAfterFrame()
    {
        yield return null;

        Scene lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
        if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
        {
            yield break;
        }

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(lobbyScene);
        if (unloadOperation == null)
        {
            yield break;
        }

        while (!unloadOperation.isDone)
        {
            yield return null;
        }
    }

    private static void PreserveNetworkRunners()
    {
        for (int i = NetworkRunner.Instances.Count - 1; i >= 0; i--)
        {
            NetworkRunner runner = NetworkRunner.Instances[i];
            if (runner == null)
            {
                continue;
            }

            Transform root = runner.transform.root;
            DontDestroyOnLoad(root.gameObject);
        }
    }

    private static Transform FindSceneTransform(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            Transform found = FindTransformRecursive(root.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindTransformRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindTransformRecursive(current.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ResolveTutorialQuickMenu()
    {
        if (tutorialQuickMenu != null)
        {
            return;
        }

        Transform quickMenuTransform = FindTransformRecursive(transform, TutorialQuickMenuObjectName);
        tutorialQuickMenu = quickMenuTransform != null ? quickMenuTransform.gameObject : null;
    }

    private void UpdateTutorialQuickMenuVisibility(Scene scene)
    {
        ResolveTutorialQuickMenu();
        if (tutorialQuickMenu != null)
        {
            tutorialQuickMenu.SetActive(scene.IsValid() && scene.name == tutorialSceneName);
        }
    }
}
