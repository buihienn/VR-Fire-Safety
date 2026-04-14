using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlameNode : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private ParticleSystem[] fireEffects;
    [SerializeField] private Light[] fireLights;
    [SerializeField] private bool autoFindEffects = true;
    [SerializeField] private bool igniteOnStart = false;

    [Header("Collision")]
    [SerializeField] private Collider extinguishCollider;
    [SerializeField] private bool autoFindCollider = true;

    [Header("Spread")]
    [SerializeField] private List<FlameNode> neighbors = new List<FlameNode>();
    [SerializeField] private bool canSpread = true;
    [SerializeField] private float spreadDelayMin = 1.2f;
    [SerializeField] private float spreadDelayMax = 2.5f;
    [SerializeField] private float neighborIgniteDelayMin = 0.05f;
    [SerializeField] private float neighborIgniteDelayMax = 0.35f;

    [Header("Growth / Fade")]
    [SerializeField] private float growSpeed = 0.35f;
    [SerializeField] private float shrinkSpeed = 1.15f;
    [Range(0f, 1f)] [SerializeField] private float burn01 = 0f;
    [Range(0f, 1f)] [SerializeField] private float igniteStartBurn01 = 0.08f;

    [Header("Visual Curves")]
    [SerializeField] private AnimationCurve sizeCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.08f, 0.16f),
        new Keyframe(0.25f, 0.42f),
        new Keyframe(0.6f, 0.82f),
        new Keyframe(1f, 1f)
    );

    [SerializeField] private AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0f, 0.35f),
        new Keyframe(1f, 1f)
    );

    [SerializeField] private AnimationCurve emissionCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.10f, 0.08f),
        new Keyframe(0.35f, 0.40f),
        new Keyframe(1f, 1f)
    );

    [SerializeField] private AnimationCurve lightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.15f, 0.10f),
        new Keyframe(1f, 1f)
    );

    private float visualDamp01 = 1f;
    private bool isBurning;
    private Coroutine igniteRoutine;
    private Coroutine spreadRoutine;

    private readonly Dictionary<ParticleSystem, float> baseSize = new();
    private readonly Dictionary<ParticleSystem, float> baseSpeed = new();
    private readonly Dictionary<ParticleSystem, float> baseRate = new();
    private readonly Dictionary<Light, float> baseLightIntensity = new();
    private readonly Dictionary<Light, float> baseLightRange = new();

    public bool IsBurning => isBurning;
    public float Burn01 => burn01;

    public static readonly List<FlameNode> All = new();

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Awake()
    {
        EnsureCurves();

        if (autoFindEffects)
        {
            if (fireEffects == null || fireEffects.Length == 0)
                fireEffects = GetComponentsInChildren<ParticleSystem>(true);

            if (fireLights == null || fireLights.Length == 0)
                fireLights = GetComponentsInChildren<Light>(true);
        }

        if (autoFindCollider && extinguishCollider == null)
            extinguishCollider = GetComponent<Collider>();

        CacheBaseVisuals();
        ForceParticlesPlayOnAwakeOff();

        if (igniteOnStart)
        {
            burn01 = 0f;
            visualDamp01 = 1f;
            Ignite();
        }
        else
        {
            isBurning = false;
            burn01 = 0f;
            visualDamp01 = 1f;
            ApplyVisual(0f, true);
        }
    }

    private void Update()
    {
        float targetBurn = isBurning ? 1f : 0f;
        float speed = isBurning ? growSpeed : shrinkSpeed;

        burn01 = Mathf.MoveTowards(burn01, targetBurn, speed * Time.deltaTime);

        float finalVisual01 = Mathf.Clamp01(burn01 * visualDamp01);
        ApplyVisual(finalVisual01, false);

        if (extinguishCollider != null)
            extinguishCollider.enabled = finalVisual01 > 0.02f;
    }

    private void CacheBaseVisuals()
    {
        baseSize.Clear();
        baseSpeed.Clear();
        baseRate.Clear();
        baseLightIntensity.Clear();
        baseLightRange.Clear();

        if (fireEffects != null)
        {
            foreach (var ps in fireEffects)
            {
                if (!ps) continue;

                var main = ps.main;
                var emission = ps.emission;

                baseSize[ps] = main.startSizeMultiplier;
                baseSpeed[ps] = main.startSpeedMultiplier;
                baseRate[ps] = emission.rateOverTimeMultiplier;
            }
        }

        if (fireLights != null)
        {
            foreach (var l in fireLights)
            {
                if (!l) continue;

                baseLightIntensity[l] = l.intensity;
                baseLightRange[l] = l.range;
            }
        }
    }

    private void ForceParticlesPlayOnAwakeOff()
    {
        if (fireEffects == null) return;

        foreach (var ps in fireEffects)
        {
            if (!ps) continue;

            var main = ps.main;
            main.playOnAwake = false;
        }
    }

    private void ApplyVisual(float t, bool clearWhenZero)
    {
        t = Mathf.Clamp01(t);

        float size01 = Mathf.Clamp01(sizeCurve.Evaluate(t));
        float speed01 = Mathf.Clamp01(speedCurve.Evaluate(t));
        float emission01 = Mathf.Clamp01(emissionCurve.Evaluate(t));
        float light01 = Mathf.Clamp01(lightCurve.Evaluate(t));

        if (fireEffects != null)
        {
            foreach (ParticleSystem ps in fireEffects)
            {
                if (ps == null) continue;

                var main = ps.main;
                var emission = ps.emission;

                float s = baseSize.TryGetValue(ps, out float bs) ? bs : 1f;
                float sp = baseSpeed.TryGetValue(ps, out float bsp) ? bsp : 1f;
                float rt = baseRate.TryGetValue(ps, out float br) ? br : 1f;

                main.startSizeMultiplier = s * size01;
                main.startSpeedMultiplier = sp * speed01;
                emission.rateOverTimeMultiplier = rt * emission01;

                if (emission01 > 0.01f)
                {
                    emission.enabled = true;

                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                else
                {
                    emission.enabled = false;
                    ps.Stop(
                        true,
                        clearWhenZero
                            ? ParticleSystemStopBehavior.StopEmittingAndClear
                            : ParticleSystemStopBehavior.StopEmitting
                    );
                }
            }
        }

        if (fireLights != null)
        {
            foreach (Light l in fireLights)
            {
                if (l == null) continue;

                float baseI = baseLightIntensity.TryGetValue(l, out float i) ? i : 1f;
                float baseR = baseLightRange.TryGetValue(l, out float r) ? r : 1f;

                l.intensity = baseI * light01;
                l.range = baseR * Mathf.Lerp(0.35f, 1f, light01);
                l.enabled = light01 > 0.01f;
            }
        }
    }

    public void Ignite()
    {
        Ignite(0f);
    }

    public void Ignite(float delay)
    {
        if (isBurning) return;

        if (igniteRoutine != null)
            StopCoroutine(igniteRoutine);

        igniteRoutine = StartCoroutine(IgniteRoutine(delay));
    }

    private IEnumerator IgniteRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (isBurning) yield break;

        isBurning = true;
        visualDamp01 = 1f;

        if (burn01 < igniteStartBurn01)
            burn01 = igniteStartBurn01;

        if (canSpread)
        {
            if (spreadRoutine != null)
                StopCoroutine(spreadRoutine);

            spreadRoutine = StartCoroutine(SpreadRoutine());
        }

        igniteRoutine = null;
    }

    private IEnumerator SpreadRoutine()
    {
        float delay = Random.Range(spreadDelayMin, spreadDelayMax);
        yield return new WaitForSeconds(delay);

        foreach (FlameNode node in neighbors)
        {
            if (node == null) continue;
            if (node.IsBurning) continue;

            float igniteDelay = Random.Range(neighborIgniteDelayMin, neighborIgniteDelayMax);
            node.Ignite(igniteDelay);
        }

        spreadRoutine = null;
    }

    public void Extinguish()
    {
        if (!isBurning && burn01 <= 0.001f) return;

        if (igniteRoutine != null)
        {
            StopCoroutine(igniteRoutine);
            igniteRoutine = null;
        }

        if (spreadRoutine != null)
        {
            StopCoroutine(spreadRoutine);
            spreadRoutine = null;
        }

        isBurning = false;
        visualDamp01 = 1f;
    }

    public void SetVisualDamp01(float value)
    {
        visualDamp01 = Mathf.Clamp01(value);
    }

    public void SetCanSpread(bool value)
    {
        canSpread = value;
    }

    public void AddNeighbor(FlameNode node)
    {
        if (node == null) return;
        if (node == this) return;
        if (!neighbors.Contains(node))
            neighbors.Add(node);
    }

    public void RemoveNeighbor(FlameNode node)
    {
        if (node == null) return;
        neighbors.Remove(node);
    }

    [ContextMenu("Test Ignite")]
    private void TestIgnite()
    {
        Ignite();
    }

    [ContextMenu("Test Extinguish")]
    private void TestExtinguish()
    {
        Extinguish();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (neighbors == null) return;

        foreach (FlameNode node in neighbors)
        {
            if (node == null) continue;
            Gizmos.DrawLine(transform.position, node.transform.position);
            Gizmos.DrawSphere(node.transform.position, 0.05f);
        }
    }

    private void OnValidate()
    {
        if (spreadDelayMin < 0f) spreadDelayMin = 0f;
        if (spreadDelayMax < spreadDelayMin) spreadDelayMax = spreadDelayMin;

        if (neighborIgniteDelayMin < 0f) neighborIgniteDelayMin = 0f;
        if (neighborIgniteDelayMax < neighborIgniteDelayMin)
            neighborIgniteDelayMax = neighborIgniteDelayMin;

        if (growSpeed < 0.01f) growSpeed = 0.01f;
        if (shrinkSpeed < 0.01f) shrinkSpeed = 0.01f;

        igniteStartBurn01 = Mathf.Clamp01(igniteStartBurn01);

        EnsureCurves();
    }

    private void EnsureCurves()
    {
        if (sizeCurve == null || sizeCurve.length == 0)
        {
            sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.08f, 0.16f),
                new Keyframe(0.25f, 0.42f),
                new Keyframe(0.6f, 0.82f),
                new Keyframe(1f, 1f)
            );
        }

        if (speedCurve == null || speedCurve.length == 0)
        {
            speedCurve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(1f, 1f)
            );
        }

        if (emissionCurve == null || emissionCurve.length == 0)
        {
            emissionCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.10f, 0.08f),
                new Keyframe(0.35f, 0.40f),
                new Keyframe(1f, 1f)
            );
        }

        if (lightCurve == null || lightCurve.length == 0)
        {
            lightCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 0.10f),
                new Keyframe(1f, 1f)
            );
        }
    }
}