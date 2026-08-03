using UnityEngine;

public class GasFlareBurstEffect : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifetime = 1.2f;
    [SerializeField] private Color hotColor = new Color(1f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color flameColor = new Color(1f, 0.2f, 0.02f, 1f);
    [SerializeField] private Material particleMaterial;

    private Material runtimeMaterial;

    private void Awake()
    {
        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.55f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(hotColor, flameColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 30)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(flameColor, 0.35f),
                new GradientColorKey(new Color(0.25f, 0.02f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.4f;

        ParticleSystemRenderer particleRenderer =
            particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (particleMaterial != null)
        {
            particleRenderer.sharedMaterial = particleMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");

            if (shader != null)
            {
                runtimeMaterial = new Material(shader);
                particleRenderer.sharedMaterial = runtimeMaterial;
            }
        }

        particles.Play(true);
        Destroy(gameObject, lifetime);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
