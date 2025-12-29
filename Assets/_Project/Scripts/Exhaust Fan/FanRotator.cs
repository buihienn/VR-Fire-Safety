using UnityEngine;

public class FanRotator : MonoBehaviour
{
    public float speed = 300f;   // tốc độ quay
    public bool isOn = false;    // trạng thái quạt

    void Update()
    {
        if (isOn)
        {
            transform.Rotate(Vector3.forward, speed * Time.deltaTime);
        }
    }

    public void Toggle()
    {
        isOn = !isOn;
    }

    public void SetOn(bool value)
    {
        isOn = value;
    }
}
