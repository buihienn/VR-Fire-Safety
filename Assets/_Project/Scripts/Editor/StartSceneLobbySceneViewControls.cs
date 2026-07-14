using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class StartSceneLobbySceneViewControls
{
    private const float PanelWidth = 180f;

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

        Rect area = new Rect(12f, 12f, PanelWidth, 98f);
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

        GUILayout.EndArea();

        Handles.EndGUI();
    }
}
