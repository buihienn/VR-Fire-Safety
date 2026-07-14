using UnityEngine;

public class StartMenuUIManager : MonoBehaviour
{
    [Header("Layouts")]
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject settingsLayout;
    [SerializeField] private GameObject aboutLayout;
    [SerializeField] private GameObject reviewLayout;
    [SerializeField] private GameObject reviewVideoListLayout;
    [SerializeField] private GameObject hubGas;
    [SerializeField] private GameObject timeLabel;

    private void Start()
    {
        ShowStartMenu();
    }

    private void Awake()
    {
        if (hubGas != null)
            hubGas.SetActive(false);
        if (timeLabel != null)
            timeLabel.SetActive(false);
    }

    public void ShowStartMenu()
    {
        SetActiveLayout(startMenuLayout);
    }

    public void ShowSettings()
    {
        SetActiveLayout(settingsLayout);
    }

    public void ShowAbout()
    {
        SetActiveLayout(aboutLayout);
    }

    public void ShowReview()
    {
        SetActiveLayout(reviewLayout);
    }

    public void ShowReviewVideoList()
    {
        SetActiveLayout(reviewVideoListLayout);
    }

    private void SetActiveLayout(GameObject activeLayout)
    {
        if (startMenuLayout != null)
            startMenuLayout.SetActive(activeLayout == startMenuLayout);

        if (settingsLayout != null)
            settingsLayout.SetActive(activeLayout == settingsLayout);

        if (aboutLayout != null)
            aboutLayout.SetActive(activeLayout == aboutLayout);

        if (reviewLayout != null)
            reviewLayout.SetActive(activeLayout == reviewLayout);

        if (reviewVideoListLayout != null)
            reviewVideoListLayout.SetActive(activeLayout == reviewVideoListLayout);
    }
}
