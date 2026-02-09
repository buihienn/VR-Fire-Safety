using UnityEngine;
using System;
using System.Reflection;

public class ToggleParentRigidbodyOnGrab : MonoBehaviour
{
    [Header("Drag the HandGrabInteractable component here (as MonoBehaviour)")]
    public MonoBehaviour handGrab;          

    [Header("Target Rigidbody (parent / root)")]
    public Rigidbody parentRigidbody;       

    private object _stateEnumValueSelect;
    private PropertyInfo _stateProp;
    private bool _wasSelected;

    void Reset()
    {
        if (parentRigidbody == null)
            parentRigidbody = GetComponentInParent<Rigidbody>();
    }

    void Awake()
    {
        if (handGrab == null) return;

        _stateProp = handGrab.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.Public);
        if (_stateProp == null)
        {
            Debug.LogError("HandGrabInteractable has no public property named 'State'. Check the component type.");
            return;
        }

        Type stateType = _stateProp.PropertyType; 
        _stateEnumValueSelect = Enum.Parse(stateType, "Select");
    }

    void Update()
    {
        if (handGrab == null || parentRigidbody == null || _stateProp == null) return;

        object stateVal = _stateProp.GetValue(handGrab);
        bool isSelected = stateVal != null && stateVal.Equals(_stateEnumValueSelect);

        if (isSelected && !_wasSelected)
        {
            parentRigidbody.isKinematic = true;
            parentRigidbody.useGravity = false;
        }
        else if (!isSelected && _wasSelected)
        {
            parentRigidbody.isKinematic = false;
            parentRigidbody.useGravity = true;
        }

        _wasSelected = isSelected;
    }
}
