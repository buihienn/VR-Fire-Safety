using System;
using System.Reflection;
using Fusion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class LobbyNetworkSceneStart : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private int gameSceneBuildIndex = 4;
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    [Header("Authority")]
    [SerializeField] private bool requireSharedModeMasterClient = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onStartRequested;
    [SerializeField] private UnityEvent<string> onStartFailed;

    [Header("Debug")]
    [SerializeField] private string connectedRoomToken;
    [SerializeField] private int activePlayerCount;
    [SerializeField] private bool isSharedModeMasterClient;

    public void ConfigureGameScene(int buildIndex)
    {
        gameSceneBuildIndex = buildIndex;
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

        if (requireSharedModeMasterClient && !runner.IsSharedModeMasterClient)
        {
            Fail("Only the Shared Mode Master Client should start the game scene.");
            return;
        }

        if (gameSceneBuildIndex < 0 || gameSceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Fail($"Invalid game scene build index: {gameSceneBuildIndex}.");
            return;
        }

        onStartRequested?.Invoke();

        if (!TryLoadSceneWithRunner(runner, SceneRef.FromIndex(gameSceneBuildIndex)))
        {
            Fail("Could not call Fusion scene load. Check the Fusion version and NetworkRunner scene manager.");
        }
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

    private bool TryLoadSceneWithRunner(NetworkRunner runner, SceneRef sceneRef)
    {
        if (TryInvokeLoadScene(runner, sceneRef))
        {
            return true;
        }

        object sceneManager = runner.SceneManager;
        return sceneManager != null && TryInvokeLoadScene(sceneManager, sceneRef);
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
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(SceneRef))
            {
                method.Invoke(target, new object[] { sceneRef });
                return true;
            }

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
