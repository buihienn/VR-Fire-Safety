using TMPro;
using UnityEngine;

public class GasHUDVR : MonoBehaviour
{
    [SerializeField] private GasSystem gas;
    [SerializeField] private TMP_Text hudText;

    [Header("VO")]
    [SerializeField] private bool playVoOnLevelChange = true;
    [SerializeField] private string voLevel1 = "VO_GasLevel1";
    [SerializeField] private string voLevel2 = "VO_GasLevel2";
    [SerializeField] private string voLevel3 = "VO_GasLevel3";

    private int lastLevel = -1;

    private void Awake()
    {
        if (!gas)
            gas = FindFirstObjectByType<GasSystem>();
    }

    private void OnEnable()
    {
        if (!gas)
            gas = FindFirstObjectByType<GasSystem>();

        if (gas)
        {
            gas.GasLevelChanged += HandleGasLevelChanged;
            int level = gas.GasLevel();
            lastLevel = level;
            UpdateHud(level);
        }
    }

    private void OnDisable()
    {
        if (gas)
            gas.GasLevelChanged -= HandleGasLevelChanged;
    }

    private void HandleGasLevelChanged(int level)
    {
        bool isIncreasing = lastLevel >= 0 && level > lastLevel;
        bool isDecreasing = lastLevel >= 0 && level < lastLevel;

        UpdateHud(level);

        if (playVoOnLevelChange)
        {
            if (isIncreasing)
                PlayLevelVo(level);
            else if (isDecreasing)
                PlayLevelVoOnDecrease(level);
        }

        lastLevel = level;
    }

    private void UpdateHud(int level)
    {
        if (!gas || !hudText) return;

        hudText.text =
            $"GAS LEVEL: {level}\n" +
            $"Trang thai: {gas.GasLevelText()}\n";
    }

    private void PlayLevelVo(int level)
    {
        if (AudioManager.Instance == null) return;

        switch (level)
        {
            case 1:
                AudioManager.Instance.PlayOneShot(voLevel1);
                break;
            case 2:
                AudioManager.Instance.PlayOneShot(voLevel2);
                break;
            case 3:
                AudioManager.Instance.PlayOneShot(voLevel3);
                break;
        }
    }

    private void PlayLevelVoOnDecrease(int level)
    {
        // TODO: Tuỳ chỉnh VO khi giảm level gas.
        // Ví dụ: bạn có thể play key riêng cho giảm level.
    //     if (AudioManager.Instance == null) return;

    //     switch (level)
    //     {
    //         case 2:
    //             AudioManager.Instance.PlayOneShot(voLevel1);
    //             break;
    //         case 1:
    //             AudioManager.Instance.PlayOneShot(voLevel2);
    //             break;
    //         case 0:
    //             AudioManager.Instance.PlayOneShot(voLevel3);
    //             break;
    //     }
    }
}