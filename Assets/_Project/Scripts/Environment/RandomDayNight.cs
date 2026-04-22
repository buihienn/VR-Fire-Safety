using UnityEngine;

public class RandomDayNight : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material[] daySkyboxes;
    [SerializeField] private Material[] nightSkyboxes;

    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool updateEnvironment = true;

    [Header("Day Settings")]
    [SerializeField] private Color dayAmbientColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private float dayAmbientIntensity = 0.002f;
    [SerializeField] private bool dayFog = false;
    [SerializeField] private Color dayFogColor = new Color(0.85f, 0.88f, 0.92f);
    [SerializeField] private float dayFogDensity = 0f;

    [Header("Night Settings")]
    [SerializeField] private Color nightAmbientColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private float nightAmbientIntensity = 0.2f;
    [SerializeField] private bool nightFog = true;
    [SerializeField] private Color nightFogColor = new Color(0.08f, 0.09f, 0.11f);
    [SerializeField] private float nightFogDensity = 0.01f;

    private void Start()
    {
        ApplyRandomDayNight();
    }

    public void ApplyRandomDayNight()
    {
        bool isNight = Random.value > 0.5f;

        if (isNight)
            ApplyRandomSkyboxFromArray(nightSkyboxes, true);
        else
            ApplyRandomSkyboxFromArray(daySkyboxes, false);
    }

    private void ApplyRandomSkyboxFromArray(Material[] skyboxes, bool isNight)
    {
        if (skyboxes == null || skyboxes.Length == 0)
        {
            Debug.LogWarning($"RandomDayNight: Không có skybox cho mode {(isNight ? "Night" : "Day")}.");
            return;
        }

        int index = Random.Range(0, skyboxes.Length);
        Material selectedSkybox = skyboxes[index];

        if (selectedSkybox == null)
        {
            Debug.LogWarning($"RandomDayNight: Skybox tại index {index} đang null.");
            return;
        }

        RenderSettings.skybox = selectedSkybox;

        if (isNight)
        {
            RenderSettings.ambientLight = nightAmbientColor;
            RenderSettings.ambientIntensity = nightAmbientIntensity;
            RenderSettings.fog = nightFog;
            RenderSettings.fogColor = nightFogColor;
            RenderSettings.fogDensity = nightFogDensity;
        }
        else
        {
            RenderSettings.ambientLight = dayAmbientColor;
            RenderSettings.ambientIntensity = dayAmbientIntensity;
            RenderSettings.fog = dayFog;
            RenderSettings.fogColor = dayFogColor;
            RenderSettings.fogDensity = dayFogDensity;
        }

        if (directionalLight != null)
            directionalLight.enabled = !isNight;

        if (updateEnvironment)
            DynamicGI.UpdateEnvironment();

        Debug.Log($"RandomDayNight: {(isNight ? "Night" : "Day")} - {selectedSkybox.name}");
    }
}