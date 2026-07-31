using Fusion;
using UnityEngine;

[RequireComponent(typeof(RandomDayNight))]
public class EnvironmentNetworkSync : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnEnvironmentSelectionChanged))]
    private int EnvironmentSelectionNet { get; set; }

    private RandomDayNight randomDayNight;

    private void Awake()
    {
        randomDayNight = GetComponent<RandomDayNight>();
        randomDayNight.SuppressAutomaticInitialization();
    }

    public override void Spawned()
    {
        if (randomDayNight == null)
            randomDayNight = GetComponent<RandomDayNight>();

        randomDayNight.SuppressAutomaticInitialization();

        if (Object.HasStateAuthority)
        {
            randomDayNight.InitializeNetworkAuthority(
                out bool isNight,
                out int skyboxIndex);

            EnvironmentSelectionNet = EncodeSelection(isNight, skyboxIndex);
            return;
        }

        ApplyNetworkSelection();
    }

    private void OnEnvironmentSelectionChanged()
    {
        ApplyNetworkSelection();
    }

    private void ApplyNetworkSelection()
    {
        int encodedSelection = EnvironmentSelectionNet;
        if (encodedSelection == 0 || randomDayNight == null)
            return;

        DecodeSelection(encodedSelection, out bool isNight, out int skyboxIndex);
        randomDayNight.ApplySynchronizedDayNight(isNight, skyboxIndex);
    }

    private static int EncodeSelection(bool isNight, int skyboxIndex)
    {
        int safeIndex = Mathf.Max(0, skyboxIndex);
        return 1 + (safeIndex << 1) + (isNight ? 1 : 0);
    }

    private static void DecodeSelection(
        int encodedSelection,
        out bool isNight,
        out int skyboxIndex)
    {
        int selection = Mathf.Max(0, encodedSelection - 1);
        isNight = (selection & 1) != 0;
        skyboxIndex = selection >> 1;
    }
}
