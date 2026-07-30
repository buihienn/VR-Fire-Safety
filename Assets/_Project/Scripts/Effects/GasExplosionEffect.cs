using System.Collections;
using UnityEngine;

public class GasExplosionEffect : MonoBehaviour
{
    [Header("Effect References")]
    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private Light explosionLight;

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0.01f)] private float lightFadeDuration = 0.3f;
    [SerializeField] private bool deactivateAfterPlay;

    private Coroutine playRoutine;
    private float baseLightIntensity;
    private float baseLightRange;

    private void Awake()
    {
        CacheReferences();
        CacheLightSettings();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    [ContextMenu("Play Explosion")]
    public void Play()
    {
        CacheReferences();
        CacheLightSettings();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float effectDuration = 0f;

        if (particleSystems != null)
        {
            foreach (ParticleSystem system in particleSystems)
            {
                if (!system)
                    continue;

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(true);

                ParticleSystem.MainModule main = system.main;
                float lifetime = main.startLifetime.constantMax;
                float delay = main.startDelay.constantMax;
                effectDuration = Mathf.Max(effectDuration, main.duration + lifetime + delay);
            }
        }

        if (explosionLight)
        {
            explosionLight.enabled = true;
            explosionLight.intensity = baseLightIntensity;
            explosionLight.range = baseLightRange;
        }

        float elapsed = 0f;
        while (elapsed < lightFadeDuration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / lightFadeDuration);

            if (explosionLight)
            {
                explosionLight.intensity = baseLightIntensity * fade;
                explosionLight.range = baseLightRange * Mathf.Lerp(0.4f, 1f, fade);
            }

            yield return null;
        }

        if (explosionLight)
            explosionLight.enabled = false;

        if (deactivateAfterPlay)
        {
            float remainingTime = Mathf.Max(0f, effectDuration - lightFadeDuration);
            if (remainingTime > 0f)
                yield return new WaitForSeconds(remainingTime);

            gameObject.SetActive(false);
        }

        playRoutine = null;
    }

    private void CacheReferences()
    {
        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        if (!explosionLight)
            explosionLight = GetComponentInChildren<Light>(true);
    }

    private void CacheLightSettings()
    {
        if (!explosionLight)
            return;

        if (baseLightIntensity <= 0f)
            baseLightIntensity = explosionLight.intensity;

        if (baseLightRange <= 0f)
            baseLightRange = explosionLight.range;
    }
}
