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
    [SerializeField] private float extinguishNeeded = 12f;
    [SerializeField] private float extinguishPerHit = 1f;
    [SerializeField] private float recoveryPerSecond = 0f;

    private float extinguishProgress;
    private readonly List<ParticleCollisionEvent> collisionEvents = new();

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();
    }

    private void Update()
    {
        if (flameNode == null || !flameNode.IsBurning) return;

        if (extinguishProgress > 0f)
        {
            extinguishProgress -= recoveryPerSecond * Time.deltaTime;
            if (extinguishProgress < 0f)
                extinguishProgress = 0f;
        }
    }

    private void OnParticleCollision(GameObject other)
    {
        if (flameNode == null || !flameNode.IsBurning) return;
        if (!other.CompareTag(extinguisherTag)) return;

        ParticleSystem ps = other.GetComponent<ParticleSystem>();
        if (ps == null) return;

        int hitCount = ps.GetCollisionEvents(gameObject, collisionEvents);
        if (hitCount <= 0) hitCount = 1;

        extinguishProgress += hitCount * extinguishPerHit;

        Debug.Log($"{gameObject.name} hit by spray. Progress: {extinguishProgress}/{extinguishNeeded}");

        if (extinguishProgress >= extinguishNeeded)
        {
            flameNode.Extinguish();
            extinguishProgress = 0f;
            Debug.Log($"{gameObject.name} extinguished");
        }
    }
}