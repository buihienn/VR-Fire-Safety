using System;
using System.Collections.Generic;
using UnityEngine;

public class BreakableWindowExplosion : MonoBehaviour
{
    [Header("Automatic hierarchy lookup")]
    [SerializeField] private Transform windowModelRoot;
    [SerializeField] private string intactSuffix = "_Intact";
    [SerializeField] private string brokenSuffix = "_Broken";
    [SerializeField] private string shardNameToken = "_cell";

    [Header("Explosion physics")]
    [SerializeField, Min(0.01f)] private float shardMass = 0.08f;
    [SerializeField, Min(0f)] private float explosionForce = 6.5f;
    [SerializeField, Min(0.01f)] private float explosionRadius = 8f;
    [SerializeField, Min(0f)] private float upwardModifier = 0.35f;
    [SerializeField, Min(0f)] private float randomTorque = 2.5f;
    [SerializeField, Min(0f)] private float maximumTriggerDistance = 12f;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    private readonly List<GameObject> intactObjects = new List<GameObject>();
    private readonly List<GameObject> brokenRoots = new List<GameObject>();
    private readonly List<Transform> shards = new List<Transform>();
    private bool isBroken;

    private void Awake()
    {
        CacheWindowParts();
        ShowIntactWindow();
    }

    private void OnEnable()
    {
        GasExplosionEffect.ExplosionPlayed += HandleExplosion;
    }

    private void OnDisable()
    {
        GasExplosionEffect.ExplosionPlayed -= HandleExplosion;
    }

    [ContextMenu("Debug/Break Window")]
    public void DebugBreakWindow()
    {
        Vector3 fallbackOrigin = transform.position - transform.forward * 2f;
        BreakWindow(fallbackOrigin);
    }

    public void BreakWindow(Vector3 explosionPosition)
    {
        if (isBroken)
            return;

        CacheWindowParts();

        if (maximumTriggerDistance > 0f &&
            Vector3.Distance(explosionPosition, transform.position) > maximumTriggerDistance)
        {
            return;
        }

        isBroken = true;

        foreach (GameObject intactObject in intactObjects)
        {
            if (intactObject)
                intactObject.SetActive(false);
        }

        foreach (GameObject brokenRoot in brokenRoots)
        {
            if (brokenRoot)
                brokenRoot.SetActive(true);
        }

        foreach (Transform shard in shards)
        {
            if (!shard)
                continue;

            shard.gameObject.SetActive(true);
            PrepareShardPhysics(shard, explosionPosition);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[BreakableWindowExplosion] Broke {shards.Count} glass shards " +
                $"from explosion at {explosionPosition}.",
                this);
        }
    }

    private void HandleExplosion(Vector3 explosionPosition)
    {
        BreakWindow(explosionPosition);
    }

    private void CacheWindowParts()
    {
        intactObjects.Clear();
        brokenRoots.Clear();
        shards.Clear();

        Transform searchRoot = windowModelRoot ? windowModelRoot : transform;
        Transform[] descendants = searchRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform item in descendants)
        {
            if (item == searchRoot)
                continue;

            if (item.name.EndsWith(intactSuffix, StringComparison.OrdinalIgnoreCase))
                intactObjects.Add(item.gameObject);

            if (item.name.EndsWith(brokenSuffix, StringComparison.OrdinalIgnoreCase))
                brokenRoots.Add(item.gameObject);

            if (item.name.IndexOf(shardNameToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
                item.GetComponent<MeshRenderer>() != null)
            {
                shards.Add(item);
            }
        }

        if (debugLog && (intactObjects.Count == 0 || shards.Count == 0))
        {
            Debug.LogWarning(
                $"[BreakableWindowExplosion] Hierarchy lookup found " +
                $"{intactObjects.Count} intact objects, {brokenRoots.Count} broken roots, " +
                $"and {shards.Count} shards.",
                this);
        }
    }

    private void ShowIntactWindow()
    {
        if (isBroken)
            return;

        foreach (GameObject intactObject in intactObjects)
        {
            if (intactObject)
                intactObject.SetActive(true);
        }

        foreach (GameObject brokenRoot in brokenRoots)
        {
            if (brokenRoot)
                brokenRoot.SetActive(false);
        }
    }

    private void PrepareShardPhysics(Transform shard, Vector3 explosionPosition)
    {
        GameObject shardObject = shard.gameObject;

        Collider shardCollider = shardObject.GetComponent<Collider>();
        if (!shardCollider)
        {
            BoxCollider boxCollider = shardObject.AddComponent<BoxCollider>();
            MeshFilter meshFilter = shardObject.GetComponent<MeshFilter>();
            if (meshFilter && meshFilter.sharedMesh)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                boxCollider.center = meshBounds.center;
                boxCollider.size = EnsureColliderThickness(meshBounds.size);
            }

            shardCollider = boxCollider;
        }

        shardCollider.enabled = true;

        Rigidbody body = shardObject.GetComponent<Rigidbody>();
        if (!body)
            body = shardObject.AddComponent<Rigidbody>();

        body.mass = shardMass;
        body.useGravity = true;
        body.isKinematic = false;
        body.linearDamping = 0.05f;
        body.angularDamping = 0.05f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        shard.SetParent(null, true);

        body.AddExplosionForce(
            explosionForce,
            explosionPosition,
            explosionRadius,
            upwardModifier,
            ForceMode.Impulse);

        body.AddTorque(
            UnityEngine.Random.insideUnitSphere * randomTorque,
            ForceMode.Impulse);
    }

    private static Vector3 EnsureColliderThickness(Vector3 size)
    {
        const float minimumThickness = 0.008f;
        size.x = Mathf.Max(size.x, minimumThickness);
        size.y = Mathf.Max(size.y, minimumThickness);
        size.z = Mathf.Max(size.z, minimumThickness);
        return size;
    }

    private void OnValidate()
    {
        shardMass = Mathf.Max(0.01f, shardMass);
        explosionForce = Mathf.Max(0f, explosionForce);
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        upwardModifier = Mathf.Max(0f, upwardModifier);
        randomTorque = Mathf.Max(0f, randomTorque);
        maximumTriggerDistance = Mathf.Max(0f, maximumTriggerDistance);
    }
}
