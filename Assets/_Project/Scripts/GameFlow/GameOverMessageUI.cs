using TMPro;
using UnityEngine;

public class GameOverMessageUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private string defaultTitle = "GAME OVER";
    [SerializeField] private string defaultBody = "";
    [SerializeField] private GameObject flameObject;

    private void Start()
    {
        if (!GameOverPayload.HasData)
        {
            ApplyText(defaultTitle, defaultBody);
            return;
        }

        if (GameOverPayload.PlayerWon && flameObject != null)
            flameObject.SetActive(false);

        ApplyText(GameOverPayload.Title, GameOverPayload.Body);
    }

    private void ApplyText(string title, string body)
    {
        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;
    }
}
