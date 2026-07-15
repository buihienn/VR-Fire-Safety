using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using System.Collections;
using MetaCharacterController = Oculus.Interaction.Locomotion.CharacterController;
using MetaFirstPersonLocomotor = Oculus.Interaction.Locomotion.FirstPersonLocomotor;
using UnityCharacterController = UnityEngine.CharacterController;

public class PersistentLocalPlayer : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "StartScene";
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private string endGameSceneName = "EndGameScene";
    [SerializeField] private string[] scenePlayerMarkerNames =
    {
        "MainScenePlayerSpawn",
        "PlayerSpawn",
        "SpawnPoint"
    };
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

    private static PersistentLocalPlayer instance;
    private Coroutine sceneReadyRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
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
        bool shouldPlacePlayer =
            scene.name == gameSceneName ||
            scene.name == endGameSceneName;

        if (!shouldPlacePlayer)
        {
            return;
        }

        SetMetaLocomotorsMovement(GetComponentsInChildren<MetaFirstPersonLocomotor>(true), false);

        if (sceneReadyRoutine != null)
        {
            StopCoroutine(sceneReadyRoutine);
        }

        sceneReadyRoutine = StartCoroutine(PlacePlayerWhenSceneIsReady(scene));
    }

    private IEnumerator PlacePlayerWhenSceneIsReady(Scene scene)
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

        Transform marker = FindSceneTransform(scene, scenePlayerMarkerNames);
        if (marker == null || marker == transform)
        {
            Debug.LogWarning(
                $"[{nameof(PersistentLocalPlayer)}] Could not find a player spawn marker in {scene.name}. " +
                "Create an empty GameObject named MainScenePlayerSpawn, PlayerSpawn, or SpawnPoint in the target scene.");
        }
        else
        {
            MovePlayerTo(marker);
        }

        yield return UnloadLobbySceneAfterFrame();
        sceneReadyRoutine = null;
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

        transform.SetPositionAndRotation(spawnPosition, marker.rotation);
        Physics.SyncTransforms();

        MetaCharacterController[] metaCharacterControllers = GetComponentsInChildren<MetaCharacterController>(true);
        ResetMetaCharacterControllers(metaCharacterControllers, spawnPosition, marker.rotation);

        StabilizePlayerRigidbodies();
        ResetHeadBlockingAfterTeleport();

        StartCoroutine(ReenableControllersAfterTeleport(characterControllers, metaLocomotors, metaCharacterControllers, spawnPosition));
    }

    private Vector3 SnapPositionToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundRaycastHeight;
        float maxDistance = groundRaycastHeight + groundRaycastDistance;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            float minimumSafeY = hit.point.y + groundOffset;
            position.y = Mathf.Max(position.y, minimumSafeY);
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
                    "Check the MainScene floor collider and the PlayerController locomotion Layer Mask.");
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

    private IEnumerator ReenableControllersAfterTeleport(
        UnityCharacterController[] characterControllers,
        MetaFirstPersonLocomotor[] metaLocomotors,
        MetaCharacterController[] metaCharacterControllers,
        Vector3 feetPosition)
    {
        yield return null;

        Physics.SyncTransforms();
        ResetMetaCharacterControllers(metaCharacterControllers, feetPosition, transform.rotation);

        yield return new WaitForFixedUpdate();

        Physics.SyncTransforms();
        SetCharacterControllersEnabled(characterControllers, true);
        ResetMetaCharacterControllers(metaCharacterControllers, feetPosition, transform.rotation);
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

    private static Transform FindSceneTransform(Scene scene, string[] objectNames)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (string objectName in objectNames)
        {
            foreach (GameObject root in roots)
            {
                Transform found = FindTransformRecursive(root.transform, objectName);
                if (found != null)
                {
                    return found;
                }
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
}
