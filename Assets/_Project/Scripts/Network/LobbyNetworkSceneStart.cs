using System;
using System.Collections;
using System.Reflection;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyNetworkSceneStart : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneNameOrPath = "MainScene";
    [SerializeField] private int gameSceneBuildIndex = 1;
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    [Header("Authority")]
    [SerializeField] private bool requireSharedModeMasterClient = true;

    [Header("Try Again")]
    [SerializeField] private Button tryAgainButton;
    [SerializeField, Min(0f)] private float recordingStopTimeoutSeconds = 10f;

    [Header("Events")]
    [SerializeField] private UnityEvent onStartRequested;
    [SerializeField] private UnityEvent<string> onStartFailed;

    [Header("Debug")]
    [SerializeField] private string connectedRoomToken;
    [SerializeField] private int activePlayerCount;
    [SerializeField] private bool isSharedModeMasterClient;

    public bool IsTryAgainBusy => tryAgainRequested;

    private bool tryAgainRequested;

    private void Awake()
    {
        if (tryAgainButton == null && gameObject.name == "TryAgainButton")
        {
            tryAgainButton = GetComponent<Button>();
        }
    }

    private void Update()
    {
        if (tryAgainButton != null)
        {
            tryAgainButton.interactable = CanTryAgainForRoom();
        }
    }

    public void ConfigureGameScene(int buildIndex)
    {
        gameSceneBuildIndex = buildIndex;
    }

    public void ConfigureGameScene(string sceneNameOrPath)
    {
        if (!string.IsNullOrWhiteSpace(sceneNameOrPath))
        {
            gameSceneNameOrPath = sceneNameOrPath;
        }
    }

    public void StartGameForRoom()
    {
        NetworkRunner runner = GetActiveRunner();
        if (runner == null)
        {
            Fail("No active Fusion NetworkRunner. Create or join a room before starting.");
            return;
        }

        connectedRoomToken = runner.SessionInfo?.Name ?? string.Empty;
        activePlayerCount = CountActivePlayers(runner);
        isSharedModeMasterClient = runner.IsSharedModeMasterClient;

        if (requireSharedModeMasterClient && !HasSceneLoadAuthority(runner))
        {
            Fail("Only the Shared Mode Master Client should start the game scene.");
            return;
        }

        onStartRequested?.Invoke();

        SceneRef sceneRef = ResolveGameSceneRef(out string resolvedScene);
        if (sceneRef == SceneRef.None)
        {
            Fail($"Could not resolve game scene '{gameSceneNameOrPath}' or build index {gameSceneBuildIndex}.");
            return;
        }

        if (!TryLoadSceneWithRunner(runner, sceneRef))
        {
            Fail($"Could not call Fusion scene load for '{resolvedScene}'. Check the Fusion version and NetworkRunner scene manager.");
        }
    }

    public bool CanTryAgainForRoom()
    {
        if (tryAgainRequested)
        {
            return false;
        }

        NetworkRunner runner = GetActiveRunner();
        if (runner == null || !runner.IsRunning || !HasSceneLoadAuthority(runner))
        {
            return false;
        }

        QuestScreenRecordingManager recordingManager = QuestScreenRecordingManager.Instance;
        return recordingManager == null || (!recordingManager.IsRecording && !recordingManager.IsStopping);
    }

    public void TryAgain()
    {
        if (tryAgainRequested)
        {
            return;
        }

        NetworkRunner runner = GetActiveRunner();
        if (runner == null)
        {
            Fail("No active Fusion NetworkRunner. The room is no longer available.");
            return;
        }

        if (!runner.IsRunning)
        {
            Fail("The Fusion NetworkRunner is not running.");
            return;
        }

        if (!HasSceneLoadAuthority(runner))
        {
            Fail("Only the Shared Mode Master Client can start another attempt.");
            return;
        }

        tryAgainRequested = true;
        if (tryAgainButton != null)
        {
            tryAgainButton.interactable = false;
        }

        StartCoroutine(TryAgainRoutine(runner));
    }

    private IEnumerator TryAgainRoutine(NetworkRunner runner)
    {
        QuestScreenRecordingManager recordingManager = QuestScreenRecordingManager.Instance;
        if (recordingManager != null && recordingManager.IsRecording && !recordingManager.IsStopping)
        {
            recordingManager.StopRecording();
        }

        float waitStartTime = Time.realtimeSinceStartup;
        while (recordingManager != null && (recordingManager.IsRecording || recordingManager.IsStopping))
        {
            if (recordingStopTimeoutSeconds > 0f &&
                Time.realtimeSinceStartup - waitStartTime >= recordingStopTimeoutSeconds)
            {
                CancelTryAgain("Timed out while waiting for the previous recording to stop.");
                yield break;
            }

            yield return null;
        }

        if (runner == null || !runner.IsRunning)
        {
            CancelTryAgain("The Fusion NetworkRunner stopped before MainScene could be reloaded.");
            yield break;
        }

        if (!HasSceneLoadAuthority(runner))
        {
            CancelTryAgain("This player is no longer the Shared Mode Master Client.");
            yield break;
        }

        GameOverPayload.Clear();

        SceneRef sceneRef = ResolveGameSceneRef(out string resolvedScene);
        if (sceneRef == SceneRef.None)
        {
            CancelTryAgain($"Could not resolve game scene '{gameSceneNameOrPath}' or build index {gameSceneBuildIndex}.");
            yield break;
        }

        if (!TryLoadSceneWithRunner(runner, sceneRef))
        {
            CancelTryAgain($"Could not reload gameplay scene '{resolvedScene}' through Fusion.");
        }
    }

    private void CancelTryAgain(string message)
    {
        tryAgainRequested = false;
        if (tryAgainButton != null)
        {
            tryAgainButton.interactable = CanTryAgainForRoom();
        }

        Fail(message);
    }

    private SceneRef ResolveGameSceneRef(out string resolvedScene)
    {
        resolvedScene = gameSceneNameOrPath;

        int sceneIndex = FindBuildIndexByNameOrPath(gameSceneNameOrPath);
        if (sceneIndex >= 0)
        {
            resolvedScene = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            return SceneRef.FromIndex(sceneIndex);
        }

        if (gameSceneBuildIndex >= 0 && gameSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            resolvedScene = SceneUtility.GetScenePathByBuildIndex(gameSceneBuildIndex);
            return SceneRef.FromIndex(gameSceneBuildIndex);
        }

        return SceneRef.None;
    }

    private static int FindBuildIndexByNameOrPath(string sceneNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(sceneNameOrPath))
        {
            return -1;
        }

        string normalizedTarget = sceneNameOrPath.Replace('\\', '/');
        string targetName = System.IO.Path.GetFileNameWithoutExtension(normalizedTarget);

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i).Replace('\\', '/');
            if (string.Equals(scenePath, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(System.IO.Path.GetFileNameWithoutExtension(scenePath), targetName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static NetworkRunner GetActiveRunner()
    {
        for (int i = NetworkRunner.Instances.Count - 1; i >= 0; i--)
        {
            NetworkRunner runner = NetworkRunner.Instances[i];
            if (runner != null && runner.IsRunning)
            {
                return runner;
            }
        }

        return null;
    }

    private static int CountActivePlayers(NetworkRunner runner)
    {
        int count = 0;
        foreach (PlayerRef _ in runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    private static bool HasSceneLoadAuthority(NetworkRunner runner)
    {
        return runner != null && (runner.IsSinglePlayer || runner.IsSharedModeMasterClient);
    }

    private bool TryLoadSceneWithRunner(NetworkRunner runner, SceneRef sceneRef)
    {
        try
        {
            runner.LoadScene(sceneRef, loadSceneMode, LocalPhysicsMode.None, true);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{nameof(LobbyNetworkSceneStart)}] Direct runner scene load failed. Trying compatibility path. {exception.Message}");
        }

        object sceneManager = runner.SceneManager;
        if (sceneManager != null && TryInvokeLoadScene(sceneManager, sceneRef))
        {
            return true;
        }

        return TryInvokeLoadScene(runner, sceneRef);
    }

    private bool TryInvokeLoadScene(object target, SceneRef sceneRef)
    {
        MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (MethodInfo method in methods)
        {
            if (method.Name != "LoadScene")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(SceneRef))
            {
                continue;
            }

            Type secondType = parameters[1].ParameterType;
            if (secondType == typeof(LoadSceneMode))
            {
                method.Invoke(target, new object[] { sceneRef, loadSceneMode });
                return true;
            }

            if (secondType.FullName == "Fusion.NetworkLoadSceneParameters")
            {
                object sceneParameters = Activator.CreateInstance(secondType);
                SetLoadSceneMode(sceneParameters, loadSceneMode);
                method.Invoke(target, new[] { (object)sceneRef, sceneParameters });
                return true;
            }
        }

        foreach (MethodInfo method in methods)
        {
            if (method.Name != "LoadScene")
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(SceneRef))
            {
                method.Invoke(target, new object[] { sceneRef });
                return true;
            }
        }

        return false;
    }

    private static void SetLoadSceneMode(object sceneParameters, LoadSceneMode mode)
    {
        Type type = sceneParameters.GetType();

        PropertyInfo property = type.GetProperty("LoadSceneMode", BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(sceneParameters, mode);
            return;
        }

        FieldInfo field = type.GetField("LoadSceneMode", BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(sceneParameters, mode);
        }
    }

    private void Fail(string message)
    {
        Debug.LogWarning($"[{nameof(LobbyNetworkSceneStart)}] {message}");
        onStartFailed?.Invoke(message);
    }
}
