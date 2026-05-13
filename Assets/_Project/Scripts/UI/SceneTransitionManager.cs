using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    public FadeScreen fadeScreen;

    [Header("Loading UI")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private float fillDuration = 2f;
    [SerializeField] private bool resetProgressOnStart = true;
    [SerializeField] private bool useLoadingBar = true;

    public void GoToScene(int sceneIndex)
    {
        StartCoroutine(GoToSceneRoutine(sceneIndex));
    }

    private IEnumerator GoToSceneRoutine(int sceneIndex)
    {
        if (useLoadingBar && progressBar != null && resetProgressOnStart)
            progressBar.value = 0f;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
        if (operation == null)
            yield break;

        operation.allowSceneActivation = false;

        float displayProgress = 0f;
        float fillSpeed = 1f / Mathf.Max(0.01f, fillDuration);

        while (operation.progress < 0.9f)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime * fillSpeed);

            if (useLoadingBar && progressBar != null)
                progressBar.value = displayProgress;

            yield return null;
        }

        displayProgress = 1f;
        if (useLoadingBar && progressBar != null)
            progressBar.value = displayProgress;

        if (fadeScreen != null)
        {
            fadeScreen.FadeOut();
            yield return new WaitForSeconds(fadeScreen.fadeDuration);
        }

        operation.allowSceneActivation = true;
    }
}
