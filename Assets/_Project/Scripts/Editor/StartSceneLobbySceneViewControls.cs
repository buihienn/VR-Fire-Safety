using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StartSceneLobbySceneViewControls
{
    private const float PanelWidth = 180f;
    private const string EndGameSceneName = "EndGameScene";
    private static StartSceneLobbyUI activeLobbyUI;
    private static string debugPlayerName = string.Empty;

    static StartSceneLobbySceneViewControls()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        StartSceneLobbyUI lobbyUI = Object.FindFirstObjectByType<StartSceneLobbyUI>(FindObjectsInactive.Include);
        if (lobbyUI == null)
        {
            return;
        }

        if (activeLobbyUI != lobbyUI)
        {
            activeLobbyUI = lobbyUI;
            debugPlayerName = StartSceneLobbyUI.CurrentPlayerName;
        }

        Handles.BeginGUI();

        Rect area = new Rect(12f, 12f, PanelWidth, 250f);
        GUILayout.BeginArea(area, GUI.skin.window);
        GUILayout.Label("Start Scene Debug", EditorStyles.boldLabel);

        if (GUILayout.Button("Single Player", GUILayout.Height(26f)))
        {
            lobbyUI.StartGameAsSingleplayer();
        }

        if (GUILayout.Button("Multiplayer", GUILayout.Height(26f)))
        {
            ShowMultiplayerMenu();
            lobbyUI.CreateRoom();
        }

        GUILayout.Space(4f);
        GUILayout.Label("Player Name");
        using (new EditorGUI.DisabledScope(!lobbyUI.IsMultiplayerSession))
        {
            debugPlayerName = GUILayout.TextField(debugPlayerName, 24);
            if (GUILayout.Button("Apply Player Name", GUILayout.Height(26f)) &&
                lobbyUI.SetPlayerName(debugPlayerName))
            {
                debugPlayerName = StartSceneLobbyUI.CurrentPlayerName;
            }
        }

        if (GUILayout.Button("Start Game", GUILayout.Height(26f)))
        {
            lobbyUI.StartGame();
        }

        if (GUILayout.Button("Open End Game", GUILayout.Height(26f)))
        {
            EditorApplication.delayCall += OpenEndGameScene;
        }

        GUILayout.EndArea();

        Handles.EndGUI();
    }

    private static void ShowMultiplayerMenu()
    {
        StartMenuUIManager menuUI = Object.FindFirstObjectByType<StartMenuUIManager>(FindObjectsInactive.Include);
        if (menuUI != null)
        {
            menuUI.ShowMultiplayer();
            return;
        }

        Debug.LogWarning("Start Scene Debug: Cannot find StartMenuUIManager.");
    }

    private static void OpenEndGameScene()
    {
        if (EditorApplication.isPlaying)
        {
            SceneManager.LoadScene(EndGameSceneName, LoadSceneMode.Single);
        }
    }
}
