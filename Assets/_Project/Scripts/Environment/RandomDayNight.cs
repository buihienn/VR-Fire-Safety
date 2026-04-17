using UnityEngine;
using UnityEngine.Rendering;

public class RandomDayNightSimple : MonoBehaviour
{
    public enum StartMode
    {
        Random,
        ForceDay,
        ForceNight
    }

    [Header("Start Mode")]
    [SerializeField] private StartMode startMode = StartMode.Random;

    [Range(0f, 1f)]
    [SerializeField] private float nightChance = 0.5f;

    [Header("Lights")]
    [SerializeField] private Light sunDay;
    [SerializeField] private Light moonNight;

    [Header("Optional Skybox")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;

    [Header("Day Settings")]
    [SerializeField] private Color dayAmbientColor = new Color(0.72f, 0.72f, 0.72f);
    [SerializeField] private bool dayFog = false;
    [SerializeField] private Color dayFogColor = new Color(0.85f, 0.88f, 0.92f);
    [SerializeField] private float dayFogDensity = 0.002f;

    [Header("Night Settings")]
    [SerializeField] private Color nightAmbientColor = new Color(0.12f, 0.14f, 0.18f);
    [SerializeField] private bool nightFog = true;
    [SerializeField] private Color nightFogColor = new Color(0.08f, 0.09f, 0.11f);
    [SerializeField] private float nightFogDensity = 0.01f;

    public bool IsNight { get; private set; }

    private void Awake()
    {
        bool useNight = DecideNightOrDay();
        ApplyLighting(useNight);
    }

    private bool DecideNightOrDay()
    {
        switch (startMode)
        {
            case StartMode.ForceDay:
                return false;

            case StartMode.ForceNight:
                return true;

            default:
                return Random.value < nightChance;
        }
    }

    public void ApplyLighting(bool night)
    {
        IsNight = night;

        if (sunDay != null)
            sunDay.gameObject.SetActive(!night);

        if (moonNight != null)
            moonNight.gameObject.SetActive(night);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = night ? nightAmbientColor : dayAmbientColor;

        RenderSettings.fog = night ? nightFog : dayFog;
        RenderSettings.fogColor = night ? nightFogColor : dayFogColor;
        RenderSettings.fogDensity = night ? nightFogDensity : dayFogDensity;

        if (night && nightSkybox != null)
            RenderSettings.skybox = nightSkybox;
        else if (!night && daySkybox != null)
            RenderSettings.skybox = daySkybox;

        DynamicGI.UpdateEnvironment();
    }
}