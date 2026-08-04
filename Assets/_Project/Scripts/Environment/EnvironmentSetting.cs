using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class EnvironmentSetting : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material[] daySkyboxes;
    [SerializeField] private Material[] nightSkyboxes;

    [Header("Lighting")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private bool updateEnvironment = true;

    [Header("Night Object Visibility")]
    [SerializeField] private bool forceDarkNightUntilRoomLightsOn = true;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 = mesh gần như không nhìn thấy khi đèn tắt; 1 = dùng đầy đủ ambient và reflection ban đêm. Outline không bị ảnh hưởng.")]
    private float nightObjectVisibility = 0f;

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

    private bool currentIsNight = true;
    private bool automaticInitializationSuppressed;

    public bool CurrentIsNight => currentIsNight;

    private void Start()
    {
        DayNightSetting setting = GameSettings.DayNight;
        Debug.Log(
            $"[{nameof(EnvironmentSetting)}] Scene initialization | " +
            $"Scene={SceneManager.GetActiveScene().name} | " +
            $"GameSettings.DayNight={setting} | IsNight={setting == DayNightSetting.Night} | " +
            $"AutomaticInitializationSuppressed={automaticInitializationSuppressed}",
            this);

        if (automaticInitializationSuppressed)
            return;

        ApplyDayNightFromSettings();
    }

    public void SuppressAutomaticInitialization()
    {
        automaticInitializationSuppressed = true;
    }

    public void InitializeNetworkAuthority(out bool isNight, out int skyboxIndex)
    {
        automaticInitializationSuppressed = true;

        DayNightSetting setting = GameSettings.DayNight;
        isNight = setting == DayNightSetting.Night;

        Debug.Log(
            $"[{nameof(EnvironmentSetting)}] Network authority read GameSettings | " +
            $"Scene={SceneManager.GetActiveScene().name} | GameSettings.DayNight={setting} | IsNight={isNight}",
            this);

        Material[] skyboxes = isNight ? nightSkyboxes : daySkyboxes;
        skyboxIndex = SelectSkyboxIndex(skyboxes);
        ApplySkyboxFromArray(skyboxes, isNight, skyboxIndex);
    }

    public void ApplySynchronizedDayNight(bool isNight, int skyboxIndex)
    {
        automaticInitializationSuppressed = true;
        DayNightSetting previousSetting = GameSettings.DayNight;
        GameSettings.DayNight = isNight
            ? DayNightSetting.Night
            : DayNightSetting.Day;

        Debug.Log(
            $"[{nameof(EnvironmentSetting)}] Network client applied synchronized environment | " +
            $"Scene={SceneManager.GetActiveScene().name} | ReceivedIsNight={isNight} | " +
            $"SkyboxIndex={skyboxIndex} | GameSettingsBefore={previousSetting} | " +
            $"GameSettingsAfter={GameSettings.DayNight}",
            this);

        Material[] skyboxes = isNight ? nightSkyboxes : daySkyboxes;
        ApplySkyboxFromArray(skyboxes, isNight, skyboxIndex);
    }

    public void ApplyDayNightFromSettings()
    {
        DayNightSetting setting = GameSettings.DayNight;
        bool isNight = setting == DayNightSetting.Night;
        currentIsNight = isNight;

        Debug.Log(
            $"[{nameof(EnvironmentSetting)}] Applying environment from GameSettings | " +
            $"Scene={SceneManager.GetActiveScene().name} | GameSettings.DayNight={setting} | IsNight={isNight}",
            this);

        if (isNight)
            ApplyRandomSkyboxFromArray(nightSkyboxes, true);
        else
            ApplyRandomSkyboxFromArray(daySkyboxes, false);
    }

    private void ApplyRandomSkyboxFromArray(Material[] skyboxes, bool isNight)
    {
        int index = SelectSkyboxIndex(skyboxes);
        ApplySkyboxFromArray(skyboxes, isNight, index);
    }

    private int SelectSkyboxIndex(Material[] skyboxes)
    {
        return skyboxes == null || skyboxes.Length == 0
            ? 0
            : Random.Range(0, skyboxes.Length);
    }

    private void ApplySkyboxFromArray(Material[] skyboxes, bool isNight, int index)
    {
        if (skyboxes == null || skyboxes.Length == 0)
        {
            Debug.LogWarning(
                $"[{nameof(EnvironmentSetting)}] No skybox configured | " +
                $"Scene={SceneManager.GetActiveScene().name} | Environment={(isNight ? "Night" : "Day")}",
                this);
            return;
        }

        index = Mathf.Clamp(index, 0, skyboxes.Length - 1);
        Material selectedSkybox = skyboxes[index];

        if (selectedSkybox == null)
        {
            Debug.LogWarning(
                $"[{nameof(EnvironmentSetting)}] Skybox is null | " +
                $"Scene={SceneManager.GetActiveScene().name} | Index={index}",
                this);
            return;
        }

        currentIsNight = isNight;
        RenderSettings.skybox = selectedSkybox;

        if (isNight)
        {
            if (forceDarkNightUntilRoomLightsOn)
            {
                float visibility = Mathf.Clamp01(nightObjectVisibility);
                Color visibleAmbientColor = ScaleRgb(nightAmbientColor, visibility);

                ApplyEnvironmentLighting(
                    AmbientMode.Flat,
                    visibleAmbientColor,
                    visibleAmbientColor,
                    visibleAmbientColor,
                    visibleAmbientColor,
                    nightAmbientIntensity * visibility);
            }
            else
            {
                ApplyEnvironmentLighting(
                    nightAmbientMode,
                    nightAmbientColor,
                    nightAmbientSkyColor,
                    nightAmbientEquatorColor,
                    nightAmbientGroundColor,
                    nightAmbientIntensity);
            }

            ApplyEnvironmentReflections(
                nightReflectionMode,
                nightCustomReflection,
                forceDarkNightUntilRoomLightsOn
                    ? nightReflectionIntensity * Mathf.Clamp01(nightObjectVisibility)
                    : nightReflectionIntensity,
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

        Debug.Log(
            $"[{nameof(EnvironmentSetting)}] Environment applied | " +
            $"Scene={SceneManager.GetActiveScene().name} | GameSettings.DayNight={GameSettings.DayNight} | " +
            $"AppliedEnvironment={(isNight ? "Night" : "Day")} | SkyboxIndex={index} | " +
            $"Skybox={selectedSkybox.name} | MatchesGameSettings={GameSettings.IsNight == isNight}",
            this);
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

    private static Color ScaleRgb(Color color, float multiplier)
    {
        return new Color(
            color.r * multiplier,
            color.g * multiplier,
            color.b * multiplier,
            color.a);
    }

    private static void ApplyEnvironmentReflections(
        DefaultReflectionMode reflectionMode,
        Cubemap customReflection,
        float reflectionIntensity,
        int reflectionBounces)
    {
        RenderSettings.defaultReflectionMode = reflectionMode;
        if (reflectionMode == DefaultReflectionMode.Custom && customReflection != null)
            RenderSettings.customReflectionTexture = customReflection;

        RenderSettings.reflectionIntensity = reflectionIntensity;
        RenderSettings.reflectionBounces = Mathf.Max(1, reflectionBounces);
    }
}
