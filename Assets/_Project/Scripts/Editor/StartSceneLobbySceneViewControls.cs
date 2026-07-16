using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StartSceneLobbySceneViewControls
{
    private const float PanelWidth = 180f;
    private const string EndGameSceneName = "EndGameScene";

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

        Handles.BeginGUI();

        Rect area = new Rect(12f, 12f, PanelWidth, 130f);
        GUILayout.BeginArea(area, GUI.skin.window);
        GUILayout.Label("Start Scene Debug", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Room", GUILayout.Height(26f)))
        {
            lobbyUI.CreateRoom();
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

    private static void OpenEndGameScene()
    {
        if (EditorApplication.isPlaying)
        {
            SceneManager.LoadScene(EndGameSceneName, LoadSceneMode.Single);
        }
    }
}
