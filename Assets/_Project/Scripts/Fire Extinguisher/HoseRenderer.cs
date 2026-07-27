using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HoseRenderer : MonoBehaviour
{
    public Transform bodyAnchor;
    public Transform nozzleAnchor;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;
    }

    void LateUpdate()
    {
        if (bodyAnchor == null || nozzleAnchor == null) return;

        lr.SetPosition(0, bodyAnchor.position);
        lr.SetPosition(1, nozzleAnchor.position);
    }
}
