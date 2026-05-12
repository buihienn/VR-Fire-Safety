using UnityEngine;
using UnityEngine.Rendering;

public class RandomDayNight : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material[] daySkyboxes;
    [SerializeField] private Material[] nightSkyboxes;

    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool updateEnvironment = true;

    [Header("Day - Environment Lighting")]
    [SerializeField] private AmbientMode dayAmbientMode = AmbientMode.Flat;
    [SerializeField] private Color dayAmbientColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private Color dayAmbientSkyColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private Color dayAmbientEquatorColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private Color dayAmbientGroundColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private float dayAmbientIntensity = 0.002f;

    [Header("Day - Environment Reflections")]
    [SerializeField] private DefaultReflectionMode dayReflectionMode = DefaultReflectionMode.Skybox;
    [SerializeField] private Cubemap dayCustomReflection;
    [SerializeField] private float dayReflectionIntensity = 1f;
    [SerializeField] private int dayReflectionBounces = 1;

    [Header("Day - Fog")]
    [SerializeField] private bool dayFog = false;
    [SerializeField] private Color dayFogColor = new Color(0.85f, 0.88f, 0.92f);
    [SerializeField] private float dayFogDensity = 0f;

    [Header("Night - Environment Lighting")]
    [SerializeField] private AmbientMode nightAmbientMode = AmbientMode.Flat;
    [SerializeField] private Color nightAmbientColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private Color nightAmbientSkyColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private Color nightAmbientEquatorColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private Color nightAmbientGroundColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private float nightAmbientIntensity = 0.2f;

    [Header("Night - Environment Reflections")]
    [SerializeField] private DefaultReflectionMode nightReflectionMode = DefaultReflectionMode.Skybox;
    [SerializeField] private Cubemap nightCustomReflection;
    [SerializeField] private float nightReflectionIntensity = 1f;
    [SerializeField] private int nightReflectionBounces = 1;

    [Header("Night - Fog")]
    [SerializeField] private bool nightFog = true;
    [SerializeField] private Color nightFogColor = new Color(0.08f, 0.09f, 0.11f);
    [SerializeField] private float nightFogDensity = 0.01f;

    private void Start()
    {
        ApplyRandomDayNight();
    }

    public void ApplyRandomDayNight()
    {
        // bool isNight = Random.value > 0.5f;
        bool isNight = false;

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
            ApplyEnvironmentLighting(
                nightAmbientMode,
                nightAmbientColor,
                nightAmbientSkyColor,
                nightAmbientEquatorColor,
                nightAmbientGroundColor,
                nightAmbientIntensity);

            ApplyEnvironmentReflections(
                nightReflectionMode,
                nightCustomReflection,
                nightReflectionIntensity,
                nightReflectionBounces);

            RenderSettings.fog = nightFog;
            RenderSettings.fogColor = nightFogColor;
            RenderSettings.fogDensity = nightFogDensity;
        }
        else
        {
            ApplyEnvironmentLighting(
                dayAmbientMode,
                dayAmbientColor,
                dayAmbientSkyColor,
                dayAmbientEquatorColor,
                dayAmbientGroundColor,
                dayAmbientIntensity);

            ApplyEnvironmentReflections(
                dayReflectionMode,
                dayCustomReflection,
                dayReflectionIntensity,
                dayReflectionBounces);

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

    private static void ApplyEnvironmentLighting(
        AmbientMode ambientMode,
        Color ambientColor,
        Color ambientSkyColor,
        Color ambientEquatorColor,
        Color ambientGroundColor,
        float ambientIntensity)
    {
        RenderSettings.ambientMode = ambientMode;

        switch (ambientMode)
        {
            case AmbientMode.Flat:
                RenderSettings.ambientLight = ambientColor;
                break;
            case AmbientMode.Trilight:
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                break;
            case AmbientMode.Skybox:
                // Skybox ambient uses RenderSettings.skybox + ambientIntensity.
                break;
        }

        RenderSettings.ambientIntensity = ambientIntensity;
    }

    private static void ApplyEnvironmentReflections(
        DefaultReflectionMode reflectionMode,
        Cubemap customReflection,
        float reflectionIntensity,
        int reflectionBounces)
    {
        RenderSettings.defaultReflectionMode = reflectionMode;
        if (reflectionMode == DefaultReflectionMode.Custom && customReflection != null)
            RenderSettings.customReflection = customReflection;

        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.reflectionBounces = Mathf.Max(1, reflectionBounces);
    }
}