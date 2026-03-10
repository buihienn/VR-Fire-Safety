using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class GasFogController : MonoBehaviour
{
    [SerializeField] private GasSystem gas;

    [Header("At Gas = 0")]
    public float emissionMin = 0f;
    public int maxParticlesMin = 0;
    [Range(0f, 1f)] public float alphaMin = 0f;
    public float sizeMin = 0.8f;

    [Header("At Gas = 1")]
    public float emissionMax = 32f;
    public int maxParticlesMax = 300;
    [Range(0f, 1f)] public float alphaMax = 0.18f;
    public float sizeMax = 1.4f;

    private ParticleSystem ps;
    private ParticleSystem.MainModule main;
    private ParticleSystem.EmissionModule emission;
    private Color baseColor = Color.white;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        main = ps.main;
        emission = ps.emission;

        if (!gas)
            gas = FindFirstObjectByType<GasSystem>();

        if (main.startColor.mode == ParticleSystemGradientMode.Color)
            baseColor = main.startColor.color;
    }

    private void LateUpdate()
    {
        if (!gas) return;

        float t = gas.gas01;

        emission.rateOverTime = Mathf.Lerp(emissionMin, emissionMax, t);
        main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(maxParticlesMin, maxParticlesMax, t));
        main.startSize = Mathf.Lerp(sizeMin, sizeMax, t);

        Color c = baseColor;
        c.a = Mathf.Lerp(alphaMin, alphaMax, t);
        main.startColor = c;

        if (t <= 0.01f)
        {
            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else
        {
            if (!ps.isPlaying)
                ps.Play();
        }
    }
}