using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlameExtinguishable : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private FlameNode flameNode;

    [Header("Filter")]
    [SerializeField] private string extinguisherTag = "ExtinguisherSpray";
    [SerializeField] private bool requireParticleSystem = true;

    [Header("Extinguish Damage")]
    [SerializeField] private float extinguishPerHit = 0.25f;
    [SerializeField] private int maxHitsPerFrame = 3;

    [Header("Network Request")]
    [Tooltip("Tranh spam RPC len Host khi particle hit qua nhieu.")]
    [SerializeField] private float sendRequestInterval = 0.1f;

    private float pendingExtinguishAmount;
    private float nextSendRequestTime;
    private readonly List<ParticleCollisionEvent> collisionEvents = new();

    private void Awake()
    {
        if (!flameNode)
            flameNode = GetComponent<FlameNode>();

        if (!flameNode)
            flameNode = GetComponentInParent<FlameNode>();
    }

    private void Update()
    {
        if (pendingExtinguishAmount > 0f && Time.time >= nextSendRequestTime)
            SendPendingExtinguishRequest();
    }

    private void OnParticleCollision(GameObject other)
    {
        if (flameNode == null) return;
        if (!flameNode.IsBurning) return;
        if (!other.CompareTag(extinguisherTag)) return;

        ParticleSystem ps = other.GetComponent<ParticleSystem>();
        if (requireParticleSystem && ps == null) return;

        int hitCount = 1;

        if (ps != null)
        {
            hitCount = ps.GetCollisionEvents(gameObject, collisionEvents);
            if (hitCount <= 0)
                hitCount = 1;
        }

        hitCount = Mathf.Clamp(hitCount, 1, maxHitsPerFrame);
        pendingExtinguishAmount += hitCount * extinguishPerHit;

        if (Time.time >= nextSendRequestTime)
            SendPendingExtinguishRequest();
    }

    private void SendPendingExtinguishRequest()
    {
        if (flameNode == null) return;

        if (!flameNode.IsBurning)
        {
            pendingExtinguishAmount = 0f;
            return;
        }

        if (pendingExtinguishAmount <= 0f) return;

        float amount = pendingExtinguishAmount;
        pendingExtinguishAmount = 0f;
        nextSendRequestTime = Time.time + sendRequestInterval;

        if (FireManager.Instance != null)
        {
            // Multiplayer path: Client/Host request goes through FireManager.
            // Host will decide whether the flame is extinguished and sync it.
            FireManager.Instance.RequestExtinguish(flameNode, amount);
        }
        else
        {
            // Single-player fallback if FireManager is not in the scene.
            flameNode.ApplyExtinguishFromFireManager(amount);
        }
    }

    private void OnValidate()
    {
        if (extinguishPerHit < 0.001f) extinguishPerHit = 0.001f;
        if (maxHitsPerFrame < 1) maxHitsPerFrame = 1;
        if (sendRequestInterval < 0.02f) sendRequestInterval = 0.02f;
    }
}
