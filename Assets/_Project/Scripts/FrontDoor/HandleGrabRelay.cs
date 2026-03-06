using UnityEngine;
using Oculus.Interaction;

public class HandleGrabRelay : MonoBehaviour
{
    [SerializeField] private DoorLeftOpenByHandle doorController;

    private Grabbable _grabbable;

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();

        if (_grabbable == null)
            Debug.LogError("Missing Grabbable component!");
    }

    void OnEnable()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    void OnDisable()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (doorController == null) return;

        if (evt.Type == PointerEventType.Select)
            doorController.OnHandleGrabbed();

        if (evt.Type == PointerEventType.Unselect)
            doorController.OnHandleReleased();
    }
}