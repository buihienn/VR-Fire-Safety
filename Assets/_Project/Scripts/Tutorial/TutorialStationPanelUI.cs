using TMPro;
using UnityEngine;

public sealed class TutorialStationPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text contentLabel;

    public void SetContent(string title, string content)
    {
        if (titleLabel != null)
        {
            titleLabel.text = title;
        }

        if (contentLabel != null)
        {
            contentLabel.text = content;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
