using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    [Header("Button Visual")]
    [SerializeField] private Transform buttonVisual;
    [SerializeField] private float offAngle = 98f;
    [SerializeField] private float onAngle = 82f;

    [Header("Targets")]
    [SerializeField] private Light[] lightComponents;
    [SerializeField] private GameObject[] lightObjects;

    [Header("State")]
    [SerializeField] private bool startOn = false;

    private bool isOn;

    public bool IsOn => isOn;

    private void Awake()
    {
        isOn = startOn;
        ApplyStateInstant();
    }

    public void PressButton()
    {
        isOn = !isOn;
        ApplyStateInstant();
    }

    private void ApplyStateInstant()
    {
        SetButtonVisual(isOn ? onAngle : offAngle);

        if (lightComponents != null)
        {
            for (int i = 0; i < lightComponents.Length; i++)
            {
                if (lightComponents[i] == null) continue;
                lightComponents[i].enabled = isOn;
            }
        }

        if (lightObjects != null)
        {
            for (int i = 0; i < lightObjects.Length; i++)
            {
                if (lightObjects[i] == null) continue;
                lightObjects[i].SetActive(isOn);
            }
        }
    }

    private void SetButtonVisual(float yAngle)
    {
        if (buttonVisual == null) return;

        Vector3 euler = buttonVisual.localEulerAngles;
        euler.y = yAngle;
        buttonVisual.localEulerAngles = euler;
    }
}