using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlameExtinguishable : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private FlameNode flameNode;

    [Header("Filter")]
    [SerializeField] private string extinguisherTag = "ExtinguisherSpray";

    [Header("Extinguish")]
    [SerializeField] private float extinguishNeeded = 30f;
    [SerializeField] private float extinguishPerHit = 0.25f;
    [SerializeField] private int maxHitsPerFrame = 3;
    [SerializeField] private float recoveryPerSecond = 5f;
    [SerializeField] private float recoveryDelayAfterSpray = 0.25f;

    [Header("Visual Damp")]
    [Range(0f, 1f)] [SerializeField] private float visualShrinkStart01 = 0.15f;
    [Range(0f, 1f)] [SerializeField] private float minVisualWhileBurning = 0.12f;

    private float extinguishProgress;
    private float lastHitTime = -999f;
    private readonly List<ParticleCollisionEvent> collisionEvents = new();

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();
    }

    private void Update()
    {
        if (flameNode == null) return;

        if (!flameNode.IsBurning)
        {
            extinguishProgress = 0f;
            flameNode.SetVisualDamp01(1f);
            return;
        }

        bool canRecover = Time.time >= lastHitTime + recoveryDelayAfterSpray;

        if (canRecover && extinguishProgress > 0f)
        {
            extinguishProgress -= recoveryPerSecond * Time.deltaTime;

            if (extinguishProgress < 0f)
                extinguishProgress = 0f;
        }

        UpdateFlameVisual();
    }

    private void OnParticleCollision(GameObject other)
    {
        if (flameNode == null || !flameNode.IsBurning) return;
        if (!other.CompareTag(extinguisherTag)) return;

        ParticleSystem ps = other.GetComponent<ParticleSystem>();
        if (ps == null) return;

        int hitCount = ps.GetCollisionEvents(gameObject, collisionEvents);
        if (hitCount <= 0) hitCount = 1;

        hitCount = Mathf.Clamp(hitCount, 1, maxHitsPerFrame);
        lastHitTime = Time.time;

        extinguishProgress += hitCount * extinguishPerHit;
        if (extinguishProgress > extinguishNeeded)
            extinguishProgress = extinguishNeeded;

        UpdateFlameVisual();

        if (extinguishProgress >= extinguishNeeded)
        {
            flameNode.Extinguish();
            extinguishProgress = 0f;
            flameNode.SetVisualDamp01(1f);
        }
    }

    private void UpdateFlameVisual()
    {
        if (flameNode == null) return;
        if (!flameNode.IsBurning) return;

        float progress01 = extinguishNeeded > 0.001f
            ? Mathf.Clamp01(extinguishProgress / extinguishNeeded)
            : 0f;

        float start = Mathf.Clamp01(visualShrinkStart01);

        if (progress01 < start)
        {
            flameNode.SetVisualDamp01(1f);
            return;
        }

        float t = Mathf.InverseLerp(start, 1f, progress01);
        float damp = Mathf.Lerp(1f, minVisualWhileBurning, t);

        flameNode.SetVisualDamp01(damp);
    }

    private void OnValidate()
    {
        if (extinguishNeeded < 0.01f) extinguishNeeded = 0.01f;
        if (extinguishPerHit < 0.001f) extinguishPerHit = 0.001f;
        if (maxHitsPerFrame < 1) maxHitsPerFrame = 1;
        if (recoveryPerSecond < 0f) recoveryPerSecond = 0f;
        if (recoveryDelayAfterSpray < 0f) recoveryDelayAfterSpray = 0f;

        visualShrinkStart01 = Mathf.Clamp01(visualShrinkStart01);
        minVisualWhileBurning = Mathf.Clamp01(minVisualWhileBurning);
    }
}