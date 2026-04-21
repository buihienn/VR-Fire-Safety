using UnityEngine;

public class StartMenuUIManager : MonoBehaviour
{
    [Header("Layouts")]
    [SerializeField] private GameObject startMenuLayout;
    [SerializeField] private GameObject settingsLayout;
    [SerializeField] private GameObject aboutLayout;
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
