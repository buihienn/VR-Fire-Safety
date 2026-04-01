using UnityEngine;

public class StartMenuUIManager : MonoBehaviour
{
    [Header("Layouts")]
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject settingsLayout;
    [SerializeField] private GameObject aboutLayout;

    private void Start()
    {
        ShowStartMenu();
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

    private void SetActiveLayout(GameObject activeLayout)
    {
        if (startMenuLayout != null)
            startMenuLayout.SetActive(activeLayout == startMenuLayout);

        if (settingsLayout != null)
            settingsLayout.SetActive(activeLayout == settingsLayout);

        if (aboutLayout != null)
            aboutLayout.SetActive(activeLayout == aboutLayout);
    }
}
