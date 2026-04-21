using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverFlow : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject hubGas;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Scene Flow")]
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private float returnDelay = 5f;

    [Header("Optional - Disable When Game Over")]
    [SerializeField] private GameObject[] objectsToDisable;

    private bool gameOverStarted;

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void HandleGameOver()
    {
        if (gameOverStarted) return;
        gameOverStarted = true;

        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        // Tat HUD gas truoc
        if (hubGas != null)
            hubGas.SetActive(false);

        // Tat them cac object khac neu can
        if (objectsToDisable != null)
        {
            for (int i = 0; i < objectsToDisable.Length; i++)
            {
                if (objectsToDisable[i] != null)
                    objectsToDisable[i].SetActive(false);
            }
        }

        // Hien panel Game Over
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        yield return new WaitForSeconds(returnDelay);

        SceneManager.LoadScene(startSceneName);
    }
}