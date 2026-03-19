using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public FadeScreen fadeScreen;                        // Tham chiếu đến FadeScreen để thực hiện hiệu ứng mờ dần
    public static SceneTransitionManager singleton;      // Biến static Singleton đảm bảo chỉ có 1 instance duy nhất

    // Awake() được gọi khi script được khởi tạo, trước cả Start()
    private void Awake()
    {
        // Nếu đã tồn tại singleton khác, hủy bản cũ để tránh trùng lặp
        if (singleton && singleton != this)
            Destroy(singleton);

        // Gán instance hiện tại làm singleton
        singleton = this;
    }

    // Phương thức công khai để chuyển scene theo chỉ số (đồng bộ - có chờ fade)
    public void GoToScene(int sceneIndex)
    {
        // Bắt đầu Coroutine chuyển scene đồng bộ
        StartCoroutine(GoToSceneRoutine(sceneIndex));
    }

    // Coroutine chuyển scene đồng bộ (chờ hiệu ứng fade xong rồi mới load)
    IEnumerator GoToSceneRoutine(int sceneIndex)
    {
        fadeScreen.FadeOut();                                      // Bắt đầu hiệu ứng mờ dần (fade out) màn hình
        yield return new WaitForSeconds(fadeScreen.fadeDuration);   // Chờ cho đến khi hiệu ứng fade hoàn tất

        // Tải scene mới theo chỉ số (đồng bộ - game sẽ đứng lại cho đến khi load xong)
        SceneManager.LoadScene(sceneIndex);
    }

    // Phương thức công khai để chuyển scene theo chỉ số (bất đồng bộ - load nền)
    public void GoToSceneAsync(int sceneIndex)
    {
        // Bắt đầu Coroutine chuyển scene bất đồng bộ
        StartCoroutine(GoToSceneAsyncRoutine(sceneIndex));
    }

    // Coroutine chuyển scene bất đồng bộ (load scene ngầm trong khi fade)
    IEnumerator GoToSceneAsyncRoutine(int sceneIndex)
    {
        fadeScreen.FadeOut();                                                // Bắt đầu hiệu ứng fade out

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);  // Bắt đầu load scene bất đồng bộ (chạy ngầm)
        operation.allowSceneActivation = false;                              // Tạm chưa cho scene mới kích hoạt (chờ fade xong)

        float timer = 0;                                                     // Bộ đếm thời gian để theo dõi fade
        // Vòng lặp chờ: tiếp tục chờ cho đến khi fade xong HOẶC scene đã load xong
        while(timer <= fadeScreen.fadeDuration && !operation.isDone)
        {
            timer += Time.deltaTime;   // Cộng dồn thời gian mỗi frame
            yield return null;         // Chờ đến frame tiếp theo
        }

        operation.allowSceneActivation = true;   // Cho phép scene mới kích hoạt (chuyển sang scene mới)
    }
}
