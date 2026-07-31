using UnityEngine;
using UnityEngine.UI;

public class ShowTimeCheckbox : MonoBehaviour
{
    [SerializeField] private Toggle checkbox;

    private void Awake()
    {
        if (checkbox == null)
            return;

        checkbox.SetIsOnWithoutNotify(GameSettings.ShowTime);
        checkbox.onValueChanged.AddListener(HandleCheckboxChanged);
    }

    private void OnDestroy()
    {
        if (checkbox != null)
            checkbox.onValueChanged.RemoveListener(HandleCheckboxChanged);
    }

    private void HandleCheckboxChanged(bool isOn)
    {
        GameSettings.ShowTime = isOn;
        GameSettings.Save();
        FindFirstObjectByType<HeadCanvasController>()?.RefreshFromSettings();

        Debug.Log($"ShowTime checkbox set to {isOn}");
    }
}
