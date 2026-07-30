using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GasIgnitionZone : MonoBehaviour
{
    [SerializeField] private GasIgnitionController ignitionController;

    private void Awake()
    {
        ResolveController();

        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: GasIgnitionZone requires its Collider to be a trigger.",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryReact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryReact(other);
    }

    private void TryReact(Collider other)
    {
        ResolveController();

        if (ignitionController == null ||
            !ignitionController.HasIgnitionAuthority)
        {
            return;
        }

        GasIgnitionSource genericSource =
            other.GetComponentInParent<GasIgnitionSource>();

        if (genericSource != null)
        {
            if (genericSource.IsIgniting)
            {
                ignitionController.TryIgnite(
                    genericSource.IgnitionPosition,
                    genericSource.SourceId);
            }

            return;
        }

        LighterIgniteOnGrab lighter =
            other.GetComponentInParent<LighterIgniteOnGrab>();

        if (lighter != null && lighter.IsFireOn)
        {
            ignitionController.TryIgnite(
                lighter.transform.position,
                lighter.gameObject.name);
        }
    }

    private void ResolveController()
    {
        if (ignitionController == null)
            ignitionController = GetComponentInParent<GasIgnitionController>();
    }

}
