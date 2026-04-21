using UnityEngine;
using UnityEngine.UI;

public class ShowGasLevelToggleButton : MonoBehaviour
{
    [SerializeField] private Toggle toggle;

    private void Awake()
    {
        if (toggle == null) return;

        // Đọc state hiện tại để set UI toggle
        toggle.isOn = GameSettings.ShowGasLevel;

        // Gắn listener
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        GameSettings.ShowGasLevel = isOn;
        GameSettings.Save();
        Debug.Log($"ShowGasLevel set to {isOn}");
    }
}
