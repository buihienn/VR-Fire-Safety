using TMPro;
using UnityEngine;

public class FinalScoreText : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string format = "Score: {0}";

    private void Awake()
    {
        if (scoreText == null)
            scoreText = GetComponent<TMP_Text>();

        if (scoreText != null)
            scoreText.text = string.Format(format, ScoreManager.LastScore);
    }
}