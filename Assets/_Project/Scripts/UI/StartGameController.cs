using UnityEngine;

public class StartButton : MonoBehaviour
{
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject loadingLayout;
    [SerializeField] private SceneTransitionManager sceneTransitionManager;

    public void StartGame()
    {
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