using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class CeilingSmokeNode : MonoBehaviour
{
    [Header("Particle")]
    [SerializeField] private ParticleSystem smokeParticles;

    [Header("Activation")]
    [Range(0f, 1f)]
    [SerializeField] private float activateAtSmoke01 = 0f;

    [Range(0.01f, 1f)]
    [SerializeField] private float fadeRange = 0.2f;

    [Header("Emission")]
    [Min(0f)]
    [SerializeField] private float maximumEmissionRate = 12f;

    private void Reset()
    {
        smokeParticles = GetComponent<ParticleSystem>();
    }

    private void Awake()
    {
        if (smokeParticles == null)
            smokeParticles = GetComponent<ParticleSystem>();

        SetIntensity(0f);
    }

    public void ApplyGlobalSmoke(float globalSmoke01)
    {
        float intensity01 = Mathf.Clamp01(
            (globalSmoke01 - activateAtSmoke01) / fadeRange
        );

        SetIntensity(intensity01);
    }

    private void SetIntensity(float intensity01)
    {
        if (smokeParticles == null)
            return;

        intensity01 = Mathf.Clamp01(intensity01);

        var emission = smokeParticles.emission;
        emission.rateOverTime =
            maximumEmissionRate * intensity01;

        if (intensity01 > 0.001f)
        {
            if (!smokeParticles.isPlaying)
                smokeParticles.Play();
        }
        else
        {
            if (smokeParticles.isPlaying)
            {
                smokeParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting
                );
            }
        }
    }

    public void ClearImmediately()
    {
        if (smokeParticles == null)
            return;

        var emission = smokeParticles.emission;
        emission.rateOverTime = 0f;

        smokeParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }
}