using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButton : MonoBehaviour
{
    [SerializeField] private string networkGameSceneNameOrPath = "MainScene";
    [SerializeField] private int networkGameSceneBuildIndex = 4;
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject loadingLayout;
    [SerializeField] private SceneTransitionManager sceneTransitionManager;

    public void StartGame()
    {
        if (SceneManager.GetActiveScene().name == "StartScene")
        {
            LobbyNetworkSceneStart lobbySceneStart = FindFirstObjectByType<LobbyNetworkSceneStart>();
            if (lobbySceneStart == null)
            {
                GameObject starter = new GameObject(nameof(LobbyNetworkSceneStart));
                lobbySceneStart = starter.AddComponent<LobbyNetworkSceneStart>();
            }

            lobbySceneStart.ConfigureGameScene(networkGameSceneNameOrPath);
            lobbySceneStart.ConfigureGameScene(networkGameSceneBuildIndex);
            lobbySceneStart.StartGameForRoom();
            return;
        }

        if (startMenuLayout != null)
        {
            startMenuLayout.SetActive(false);
        }

        if (loadingLayout != null)
        {
            loadingLayout.SetActive(true);
        }

        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();

        if (sceneTransitionManager == null)
            return;

        sceneTransitionManager.GoToScene(1);
    }
}
