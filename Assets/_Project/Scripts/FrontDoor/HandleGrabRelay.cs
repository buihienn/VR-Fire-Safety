using UnityEngine;
using Oculus.Interaction;

public class HandleGrabRelay : MonoBehaviour
{
    [SerializeField] private DoorLeftOpenByHandle doorController;

    private Grabbable _grabbable;
    private bool _wasGrabbed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (_grabbable == null || doorController == null) return;

        bool grabbed = _grabbable.SelectingPointsCount > 0;

        if (grabbed != _wasGrabbed)
            Debug.Log($"[HandleGrabRelay] grabbed={grabbed}, selectingCount={_grabbable.SelectingPointsCount}");
        // Rising edge: grab
        if (grabbed && !_wasGrabbed)
            doorController.OnHandleGrabbed();

        // Falling edge: release
        if (!grabbed && _wasGrabbed) {
            doorController.OnHandleReleased();
            // transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        _wasGrabbed = grabbed;
    }
}
