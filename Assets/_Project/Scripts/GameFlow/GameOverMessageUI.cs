using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverMessageUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private string defaultTitle = "GAME OVER";
    [SerializeField] private string defaultBody = "";
    [SerializeField] private GameObject flameObject;
    [SerializeField] private string flameObjectName = "Flame";

    private void Start()
    {
        FindFlameObjectIfNeeded();

        if (!GameOverPayload.HasData)
        {
            ApplyText(defaultTitle, defaultBody);
            return;
        }

        SetFlameActive(!GameOverPayload.PlayerWon);

        ApplyText(GameOverPayload.Title, GameOverPayload.Body);
    }

    private void FindFlameObjectIfNeeded()
    {
        if (flameObject != null || string.IsNullOrWhiteSpace(flameObjectName))
        {
            return;
        }

        Scene targetScene = gameObject.scene;
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            targetScene = SceneManager.GetActiveScene();
        }

        foreach (GameObject rootObject in targetScene.GetRootGameObjects())
        {
            Transform flameTransform = FindTransformRecursive(rootObject.transform, flameObjectName);
            if (flameTransform == null)
            {
                continue;
            }

            flameObject = flameTransform.gameObject;
            return;
        }

        Debug.LogWarning(
            $"[{nameof(GameOverMessageUI)}] Could not find GameObject '{flameObjectName}' in scene " +
            $"'{targetScene.name}'.",
            this);
    }

    private void SetFlameActive(bool active)
    {
        if (flameObject != null)
        {
            flameObject.SetActive(active);
        }
    }

    private static Transform FindTransformRecursive(Transform current, string objectName)
    {
        if (current.name == objectName)
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindTransformRecursive(current.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ApplyText(string title, string body)
    {
        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;
    }
}
