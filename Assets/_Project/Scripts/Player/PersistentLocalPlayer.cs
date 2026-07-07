using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using System.Collections;

public class PersistentLocalPlayer : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "StartScene";
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private string[] scenePlayerMarkerNames =
    {
        "MainScenePlayerSpawn",
        "PlayerSpawn",
        "SpawnPoint",
        "Player"
    };
    [SerializeField] private Vector3 spawnPositionOffset;
    [SerializeField] private bool disableGravityForPlayerRigidbodies = true;
    [SerializeField] private bool makePlayerRigidbodiesKinematic = true;

    private static PersistentLocalPlayer instance;

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
        if (scene.name != gameSceneName)
        {
            return;
        }

        PreserveNetworkRunners();

        Transform marker = FindSceneTransform(scene, scenePlayerMarkerNames);
        if (marker == null || marker == transform)
        {
            Debug.LogWarning($"[{nameof(PersistentLocalPlayer)}] Could not find a player spawn marker in {scene.name}.");
        }
        else
        {
            MovePlayerTo(marker);
        }

        SceneManager.SetActiveScene(scene);
        StartCoroutine(UnloadLobbySceneAfterFrame());
    }

    private void MovePlayerTo(Transform marker)
    {
        transform.SetPositionAndRotation(marker.position + spawnPositionOffset, marker.rotation);
        StabilizePlayerRigidbodies();

        VrGhostSpawnerFusion ghostSpawner = GetComponentInChildren<VrGhostSpawnerFusion>(true);
        if (ghostSpawner != null)
        {
            ghostSpawner.MoveSpawnedGhostTo(transform.position, transform.rotation);
        }
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
