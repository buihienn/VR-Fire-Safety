using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    [SerializeField] private bool useFadeScreen = true;
    [SerializeField] private string preferredFadeScreenName = "FadeScreen";

    private FadeScreen fadeScreen;

    private void Awake()
    {
        ResolveFadeScreen();
    }

    public void GoToScene(int sceneIndex)
    {
        StartCoroutine(GoToSceneRoutine(sceneIndex));
    }

    private IEnumerator GoToSceneRoutine(int sceneIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        if (operation == null)
            yield break;

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
            yield return null;

        FadeScreen activeFadeScreen = ResolveFadeScreen();
        if (useFadeScreen && activeFadeScreen != null)
        {
            activeFadeScreen.FadeOut();
            yield return new WaitForSeconds(activeFadeScreen.fadeDuration);
        }

        operation.allowSceneActivation = true;
    }

    private FadeScreen ResolveFadeScreen()
    {
        if (!useFadeScreen)
        {
            return null;
        }

        if (fadeScreen != null)
        {
            return fadeScreen;
        }

        FadeScreen[] fadeScreens = FindObjectsByType<FadeScreen>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (FadeScreen candidate in fadeScreens)
        {
            if (candidate != null && candidate.name == preferredFadeScreenName)
            {
                fadeScreen = candidate;
                return fadeScreen;
            }
        }

        if (fadeScreens.Length > 0)
        {
            fadeScreen = fadeScreens[0];
        }

        return fadeScreen;
    }
}
