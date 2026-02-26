using UnityEngine;

public class LighterIgnite : MonoBehaviour
{
    [SerializeField] private ParticleSystem flame;
    [SerializeField] private bool pressToToggle = true;
    [SerializeField] private float triggerThreshold = 0.8f;

    private bool isHeld;
    private bool isOn;

    private void Awake()
    {
        if (!flame) flame = GetComponentInChildren<ParticleSystem>(true);
        SetOn(false);
    }

    public void OnGrab() => isHeld = true;

    public void OnRelease()
    {
        isHeld = false;
        SetOn(false);
    }

    private void Update()
    {
        if (!isHeld) return;

        bool triggerDown =
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);

        bool triggerHeld =
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > triggerThreshold ||
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > triggerThreshold;

        if (pressToToggle)
        {
            if (triggerDown) SetOn(!isOn);
        }
        else
        {
            SetOn(triggerHeld);
        }
    }

    private void SetOn(bool on)
    {
        if (isOn == on) return;
        isOn = on;

        if (!flame) return;

        if (on)
        {
            flame.gameObject.SetActive(true);
            flame.Play(true);
        }
        else
        {
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flame.gameObject.SetActive(false);
        }
    }
}