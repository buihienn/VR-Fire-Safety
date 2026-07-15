using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneButton : MonoBehaviour
{
    [SerializeField] private int endGameSceneIndex = 2;
    [SerializeField] private SceneTransitionManager sceneTransitionManager;

    public void LoadScene()
    {
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();

        if (sceneTransitionManager != null)
            sceneTransitionManager.GoToScene(endGameSceneIndex);
    }
}
