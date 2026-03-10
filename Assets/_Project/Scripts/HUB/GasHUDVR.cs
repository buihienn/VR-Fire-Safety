using TMPro;
using UnityEngine;

public class GasHUDVR : MonoBehaviour
{
    [SerializeField] private GasSystem gas;
    [SerializeField] private TMP_Text hudText;

    private void Awake()
    {
        if (!gas)
            gas = FindFirstObjectByType<GasSystem>();
    }

    private void Update()
    {
        if (!gas || !hudText) return;

        int level = gas.GasLevel();

        hudText.text =
            $"GAS LEVEL: {level}\n" +
            $"Trang thai: {gas.GasLevelText()}\n";
    }
}