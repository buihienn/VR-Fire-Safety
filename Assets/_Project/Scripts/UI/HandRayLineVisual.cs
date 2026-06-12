using UnityEngine;

public class HandRayLineVisual : MonoBehaviour
{
    public Transform rayOrigin;
    public LineRenderer lineRenderer;
    public float maxRayLength = 5f;

    void Update()
    {
        if (rayOrigin == null || lineRenderer == null)
            return;

        Vector3 startPoint = rayOrigin.position;
        Vector3 endPoint = startPoint + rayOrigin.forward * maxRayLength;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
    }
}