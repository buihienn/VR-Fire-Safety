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

    [Header("Spread")]
    [SerializeField] private List<FlameNode> neighbors = new List<FlameNode>();
    [SerializeField] private bool canSpread = true;
    [SerializeField] private float spreadDelayMin = 1.2f;
    [SerializeField] private float spreadDelayMax = 2.5f;
    [SerializeField] private float neighborIgniteDelayMin = 0.05f;
    [SerializeField] private float neighborIgniteDelayMax = 0.35f;

    private bool isBurning;
    private Coroutine igniteRoutine;
    private Coroutine spreadRoutine;

    public bool IsBurning => isBurning;

    private void Awake()
    {
        if (autoFindEffects)
        {
            if (fireEffects == null || fireEffects.Length == 0)
                fireEffects = GetComponentsInChildren<ParticleSystem>(true);

            if (fireLights == null || fireLights.Length == 0)
                fireLights = GetComponentsInChildren<Light>(true);
        }

        if (igniteOnStart)
        {
            SetBurning(true);
        }
        else
        {
            SetBurning(false, true);
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

        SetBurning(true);

        if (canSpread)
        {
            if (spreadRoutine != null)
                StopCoroutine(spreadRoutine);

            spreadRoutine = StartCoroutine(SpreadRoutine());
        }
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
    }

    public void Extinguish()
    {
        if (!isBurning) return;

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

        SetBurning(false, true);
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

    private void SetBurning(bool value, bool clearParticles = false)
    {
        isBurning = value;

        if (fireEffects != null)
        {
            foreach (ParticleSystem ps in fireEffects)
            {
                if (ps == null) continue;

                if (value)
                {
                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                else
                {
                    ps.Stop(true,
                        clearParticles
                            ? ParticleSystemStopBehavior.StopEmittingAndClear
                            : ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        if (fireLights != null)
        {
            foreach (Light l in fireLights)
            {
                if (l != null)
                    l.enabled = value;
            }
        }
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
    }
}