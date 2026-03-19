using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartButton : MonoBehaviour
{
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private float fillDuration = 2f;
    [SerializeField] private GameObject gasLevelCanvas;

    // Tên hoặc index của scene cần load
    [SerializeField] private string sceneName = "MainScene";

    public void StartGame()
    {
        StartCoroutine(LoadWithFade());
    }

    private IEnumerator LoadWithFade()
    {
        // Lấy tham chiếu FadeScreen từ SceneTransitionManager singleton
        FadeScreen fadeScreen = SceneTransitionManager.singleton.fadeScreen;

        // Bắt đầu hiệu ứng fade out (màn hình tối dần)
        fadeScreen.FadeOut();

        // Chờ fade out hoàn tất
        yield return new WaitForSeconds(fadeScreen.fadeDuration);

        // Hiện loading panel sau khi màn hình đã tối
        loadingPanel.SetActive(true);

        // Fade in để hiện loading panel
        fadeScreen.FadeIn();
        yield return new WaitForSeconds(fadeScreen.fadeDuration);

        // Bắt đầu load scene bất đồng bộ
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float displayProgress = 0f;

        // Vòng lặp cập nhật thanh tiến trình
        while (!operation.isDone)
        {
            float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
            displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.deltaTime / fillDuration);
            progressBar.value = displayProgress;

            // Khi scene đã load xong (>=90%) và thanh progress đã đầy
            if (operation.progress >= 0.9f && displayProgress >= 1f)
            {
                // Fade out trước khi chuyển sang scene mới
                fadeScreen.FadeOut();
                yield return new WaitForSeconds(fadeScreen.fadeDuration);

                // Cho phép kích hoạt scene mới
                operation.allowSceneActivation = true;

                // Bật GasLevelCanvas khi chuyển sang MainScene
                if (gasLevelCanvas != null)
                    gasLevelCanvas.SetActive(true);
            }

            yield return null;
        }
    }
}