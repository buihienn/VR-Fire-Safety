using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnvironmentNetworkSync : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnEnvironmentSelectionChanged))]
    private int EnvironmentSelectionNet { get; set; }

    [Header("Environment Manager (resolved automatically when empty)")]
    [SerializeField] private EnvironmentSetting environmentManager;

    private bool missingManagerLogged;

    private void Awake()
    {
        if (TryResolveEnvironmentManager())
            environmentManager.SuppressAutomaticInitialization();
    }

    public override void Spawned()
    {
        if (!TryResolveEnvironmentManager())
            return;

        environmentManager.SuppressAutomaticInitialization();

        if (Object.HasStateAuthority)
        {
            environmentManager.InitializeNetworkAuthority(
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
        if (encodedSelection == 0 || !TryResolveEnvironmentManager())
            return;

        DecodeSelection(encodedSelection, out bool isNight, out int skyboxIndex);
        environmentManager.ApplySynchronizedDayNight(isNight, skyboxIndex);
    }

    private bool TryResolveEnvironmentManager()
    {
        if (environmentManager != null)
            return true;

        string resolveSource = null;

        environmentManager = GetComponent<EnvironmentSetting>();
        if (environmentManager != null)
        {
            resolveSource = "same GameObject";
        }
        else
        {
            GameObject managerObject = GameObject.Find("EnvironmentManager");
            if (managerObject == null)
                managerObject = GameObject.Find("EnviromentManager");

            if (managerObject != null)
            {
                environmentManager = managerObject.GetComponent<EnvironmentSetting>();
                if (environmentManager != null)
                    resolveSource = $"GameObject.Find({managerObject.name})";
            }
        }

        if (environmentManager == null)
        {
            environmentManager = FindFirstObjectByType<EnvironmentSetting>(FindObjectsInactive.Include);
            if (environmentManager != null)
                resolveSource = "FindFirstObjectByType (including inactive)";
        }

        if (environmentManager != null)
        {
            missingManagerLogged = false;
            Debug.Log(
                $"[{nameof(EnvironmentNetworkSync)}] EnvironmentManager resolved | " +
                $"Scene={SceneManager.GetActiveScene().name} | Source={resolveSource} | " +
                $"GameObject={environmentManager.gameObject.name}",
                this);
            return true;
        }

        if (!missingManagerLogged)
        {
            missingManagerLogged = true;
            Debug.LogError(
                $"[{nameof(EnvironmentNetworkSync)}] EnvironmentManager was not found | " +
                $"Scene={SceneManager.GetActiveScene().name}",
                this);
        }

        return false;
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
