using UnityEngine;

public class WireProxyFollowAnchor : MonoBehaviour
{
    [Header("Anchor inside FireExtinguisher")]
    public Transform anchor;

    [Header("Proxy Rigidbody")]
    public Rigidbody rb;

    [Header("Copy scale too")]
    public bool copyWorldScale = true;

    void Reset()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (anchor == null) return;

        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(anchor.position);
            rb.MoveRotation(anchor.rotation);
        }
        else
        {
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        if (copyWorldScale)
        {
            SetWorldScale(transform, anchor.lossyScale);
        }
    }

    private void SetWorldScale(Transform target, Vector3 desiredWorldScale)
    {
        if (target.parent == null)
        {
            target.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentScale = target.parent.lossyScale;

        target.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z)
        );
    }

    private float SafeDivide(float a, float b)
    {
        if (Mathf.Abs(b) < 0.000001f) return a;
        return a / b;
    }
}