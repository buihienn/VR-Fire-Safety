using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class ExplosionPhysicsReceiver : NetworkBehaviour
{
    [Header("Physics target")]
    [SerializeField] private Rigidbody targetBody;
    [SerializeField] private bool detachFromParentOnExplosion = true;
    [SerializeField, Min(0.01f)] private float mass = 4f;

    [Header("Explosion response")]
    [SerializeField, Min(0f)] private float explosionForce = 14f;
    [SerializeField, Min(0.01f)] private float explosionRadius = 8f;
    [SerializeField, Min(0f)] private float upwardModifier = 0.65f;
    [SerializeField, Min(0f)] private float tippingTorque = 8f;
    [SerializeField, Min(0f)] private float maximumTriggerDistance = 10f;
    [SerializeField] private bool reactOnlyOnce = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Networked] private NetworkBool ExplosionAppliedNet { get; set; }
    [Networked] private Vector3 ExplosionPositionNet { get; set; }

    private bool fusionSpawned;
    private bool hasReacted;

    private void Awake()
    {
        if (!targetBody)
            targetBody = GetComponent<Rigidbody>();

        ConfigureBodyForWaiting();
    }

    private void OnEnable()
    {
        GasExplosionEffect.ExplosionPlayed += HandleExplosion;
    }

    private void OnDisable()
    {
        GasExplosionEffect.ExplosionPlayed -= HandleExplosion;
    }

    public override void Spawned()
    {
        fusionSpawned = true;
        ConfigureAuthorityPhysics();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        fusionSpawned = false;
        ConfigureBodyForWaiting();
    }

    public override void Render()
    {
        if (!fusionSpawned || hasReacted || !ExplosionAppliedNet)
            return;

        if (Object != null && Object.HasStateAuthority)
            ApplyExplosionInternal(ExplosionPositionNet);
        else
            hasReacted = true;
    }

    [ContextMenu("Debug/Apply Explosion")]
    public void DebugApplyExplosion()
    {
        Vector3 fallbackOrigin = transform.position - transform.forward * 2f;
        ApplyExplosion(fallbackOrigin);
    }

    public void ApplyExplosion(Vector3 explosionPosition)
    {
        if (reactOnlyOnce && hasReacted)
            return;

        float distance = Vector3.Distance(explosionPosition, transform.position);
        if (maximumTriggerDistance > 0f && distance > maximumTriggerDistance)
            return;

        if (fusionSpawned)
        {
            if (Object == null || !Object.HasStateAuthority)
                return;

            ExplosionPositionNet = explosionPosition;
            ExplosionAppliedNet = true;
        }

        ApplyExplosionInternal(explosionPosition);
    }

    private void ApplyExplosionInternal(Vector3 explosionPosition)
    {
        if (reactOnlyOnce && hasReacted)
            return;

        hasReacted = true;

        if (detachFromParentOnExplosion)
            transform.SetParent(null, true);

        EnsureRigidbody();

        bool canSimulate = !fusionSpawned ||
            (Object != null && Object.HasStateAuthority);

        targetBody.isKinematic = !canSimulate;
        targetBody.useGravity = canSimulate;

        if (!canSimulate)
            return;

        float distance = Vector3.Distance(explosionPosition, transform.position);

        targetBody.AddExplosionForce(
            explosionForce,
            explosionPosition,
            explosionRadius,
            upwardModifier,
            ForceMode.Impulse);

        Vector3 awayDirection =
            Vector3.ProjectOnPlane(transform.position - explosionPosition, Vector3.up);

        if (awayDirection.sqrMagnitude < 0.001f)
            awayDirection = transform.forward;
        else
            awayDirection.Normalize();

        Vector3 tippingAxis = Vector3.Cross(Vector3.up, awayDirection).normalized;
        Vector3 torque =
            tippingAxis * tippingTorque +
            Random.insideUnitSphere * (tippingTorque * 0.25f);

        targetBody.AddTorque(torque, ForceMode.Impulse);

        if (debugLog)
        {
            Debug.Log(
                $"[ExplosionPhysicsReceiver] Applied explosion to {name} " +
                $"from {explosionPosition} at distance {distance:0.00}m. " +
                $"Networked={fusionSpawned}.",
                this);
        }
    }

    private void EnsureRigidbody()
    {
        EnsureCollider();

        if (!targetBody)
            targetBody = GetComponent<Rigidbody>();

        if (!targetBody)
            targetBody = gameObject.AddComponent<Rigidbody>();

        targetBody.mass = mass;
        targetBody.useGravity = true;
        targetBody.isKinematic = false;
        targetBody.constraints = RigidbodyConstraints.None;
        targetBody.linearDamping = 0.12f;
        targetBody.angularDamping = 0.1f;
        targetBody.interpolation = RigidbodyInterpolation.Interpolate;
        targetBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>(true) != null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds localBounds = default;

        foreach (Renderer itemRenderer in renderers)
        {
            Bounds worldBounds = itemRenderer.bounds;
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldCorner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        if (!hasBounds)
            return;

        BoxCollider generatedCollider = gameObject.AddComponent<BoxCollider>();
        generatedCollider.center = localBounds.center;
        generatedCollider.size = localBounds.size;
    }

    private void ConfigureBodyForWaiting()
    {
        EnsureRigidbody();
        targetBody.useGravity = false;
        targetBody.isKinematic = true;
    }

    private void ConfigureAuthorityPhysics()
    {
        EnsureRigidbody();

        bool shouldSimulate =
            ExplosionAppliedNet &&
            Object != null &&
            Object.HasStateAuthority;

        targetBody.useGravity = shouldSimulate;
        targetBody.isKinematic = !shouldSimulate;
    }

    private void HandleExplosion(Vector3 explosionPosition)
    {
        ApplyExplosion(explosionPosition);
    }

    private void OnValidate()
    {
        mass = Mathf.Max(0.01f, mass);
        explosionForce = Mathf.Max(0f, explosionForce);
        explosionRadius = Mathf.Max(0.01f, explosionRadius);
        upwardModifier = Mathf.Max(0f, upwardModifier);
        tippingTorque = Mathf.Max(0f, tippingTorque);
        maximumTriggerDistance = Mathf.Max(0f, maximumTriggerDistance);
    }
}
