using UnityEngine;
using UnityEngine.UI;

public class ShowGasLevelCheckbox : MonoBehaviour
{
    [SerializeField] private Toggle checkbox;

    private void Awake()
    {
        if (checkbox == null) return;

        // Đọc state hiện tại để set UI checkbox
        checkbox.isOn = GameSettings.ShowGasLevel;

        // Gắn listener
        checkbox.onValueChanged.AddListener(OnCheckboxChanged);
    }

    private void OnDestroy()
    {
        if (checkbox != null)
            checkbox.onValueChanged.RemoveListener(OnCheckboxChanged);
    }

    private void OnCheckboxChanged(bool isOn)
    {
        GameSettings.ShowGasLevel = isOn;
        GameSettings.Save();
        Debug.Log($"ShowGasLevel checkbox set to {isOn}");
    }
}
