using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject loadingLayout;
    [SerializeField] private Slider progressBar;

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

        StartCoroutine(LoadSceneAsync());
    }

    [SerializeField] private float fillDuration = 2f;

    private IEnumerator LoadSceneAsync()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync("MainScene");
        operation.allowSceneActivation = false;

        float displayProgress = 0f;

        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime / fillDuration);
            progressBar.value = displayProgress;

            if (operation.progress >= 0.9f && displayProgress >= 1f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}