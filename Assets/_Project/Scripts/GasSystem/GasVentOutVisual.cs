using UnityEngine;

[RequireComponent(typeof(GasVentByAngle))]
public class GasVentOutVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private GasVentByAngle vent;
    [SerializeField] private ParticleSystem ventOutPS;

    [Header("Show Conditions")]
    [SerializeField] private float minGasToShow = 0.03f;
    [SerializeField] private float minOpenToShow = 0.01f;

    [Header("Emission")]
    [SerializeField] private float emissionMin = 0f;
    [SerializeField] private float emissionMax = 40f;

    [Header("Particle Speed")]
    [SerializeField] private float speedMin = 0.2f;
    [SerializeField] private float speedMax = 1.5f;

    [Header("Particle Size")]
    [SerializeField] private float sizeMin = 0.1f;
    [SerializeField] private float sizeMax = 0.35f;

    [Header("Particle Alpha")]
    [Range(0f, 1f)] [SerializeField] private float alphaMin = 0f;
    [Range(0f, 1f)] [SerializeField] private float alphaMax = 0.2f;

    private ParticleSystem.MainModule main;
    private ParticleSystem.EmissionModule emission;
    private Color baseColor = Color.white;

    private void Awake()
    {
        if (!vent)
            vent = GetComponent<GasVentByAngle>();

        if (!gasSystem)
            gasSystem = FindFirstObjectByType<GasSystem>();

        if (ventOutPS == null)
        {
            Debug.LogWarning($"[{nameof(GasVentOutVisual)}] Missing VentOut ParticleSystem on {name}");
            enabled = false;
            return;
        }

        main = ventOutPS.main;
        emission = ventOutPS.emission;

        if (main.startColor.mode == ParticleSystemGradientMode.Color)
            baseColor = main.startColor.color;
    }

    private void LateUpdate()
    {
        if (gasSystem == null || vent == null || ventOutPS == null)
            return;

        float gas01 = gasSystem.gas01;
        float open01 = vent.GetOpen01();

        bool shouldShow = gas01 > minGasToShow && open01 > minOpenToShow;

        if (!shouldShow)
        {
            if (ventOutPS.isPlaying)
                ventOutPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            emission.rateOverTime = 0f;
            return;
        }

        float strength = gas01 * open01;

        emission.rateOverTime = Mathf.Lerp(emissionMin, emissionMax, strength);
        main.startSpeed = Mathf.Lerp(speedMin, speedMax, open01);
        main.startSize = Mathf.Lerp(sizeMin, sizeMax, gas01);

        Color c = baseColor;
        c.a = Mathf.Lerp(alphaMin, alphaMax, strength);
        main.startColor = c;

        if (!ventOutPS.isPlaying)
            ventOutPS.Play();
    }
}