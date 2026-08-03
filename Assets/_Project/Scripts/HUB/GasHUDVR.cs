using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GasHUDVR : MonoBehaviour
{
    [SerializeField] private GasSystem gas;
    [SerializeField] private TMP_Text hudText;

    [Header("Gas System Lookup")]
    [SerializeField, Min(1)] private int resolveRetryFrames = 120;

    [Header("VO")]
    [SerializeField] private bool playVoOnLevelChange = true;
    [SerializeField] private string voLevel1 = "VO_GasLevel1";
    [SerializeField] private string voLevel2 = "VO_GasLevel2";
    [SerializeField] private string voLevel3 = "VO_GasLevel3";

    private int lastLevel = -1;
    private GasSystem subscribedGas;
    private Coroutine resolveRoutine;

    private void Awake()
    {
        TryBindGasSystem();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartResolveGasSystem();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (resolveRoutine != null)
        {
            StopCoroutine(resolveRoutine);
            resolveRoutine = null;
        }

        UnbindGasSystem();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isActiveAndEnabled)
            return;

        StartResolveGasSystem();
    }

    private void StartResolveGasSystem()
    {
        if (!isActiveAndEnabled)
            return;

        if (resolveRoutine != null)
            StopCoroutine(resolveRoutine);

        if (TryBindGasSystem())
        {
            resolveRoutine = null;
            return;
        }

        resolveRoutine = StartCoroutine(ResolveGasSystemRoutine());
    }

    private IEnumerator ResolveGasSystemRoutine()
    {
        for (int i = 0; i < resolveRetryFrames; i++)
        {
            yield return null;

            if (TryBindGasSystem())
            {
                resolveRoutine = null;
                yield break;
            }
        }

        resolveRoutine = null;
        Debug.LogWarning(
            $"[{nameof(GasHUDVR)}] Could not find an active {nameof(GasSystem)} " +
            $"after {resolveRetryFrames} frames.",
            this);
    }

    private bool TryBindGasSystem()
    {
        GasSystem target = gas;
        if (!target)
            target = FindFirstObjectByType<GasSystem>();

        if (!target)
            return false;

        if (subscribedGas == target)
            return true;

        UnbindGasSystem();

        gas = target;
        subscribedGas = target;
        subscribedGas.GasLevelChanged += HandleGasLevelChanged;

        int level = subscribedGas.GasLevel();
        lastLevel = level;
        UpdateHud(level);
        return true;
    }

    private void UnbindGasSystem()
    {
        if (subscribedGas)
            subscribedGas.GasLevelChanged -= HandleGasLevelChanged;

        subscribedGas = null;
        lastLevel = -1;
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
