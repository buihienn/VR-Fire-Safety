using UnityEngine;

public class FanRotator : MonoBehaviour
{
    public float speed = 300f;   // tốc độ quay
    public bool isOn = false;    // trạng thái quạt

    void Update()
    {
        if (isOn)
        {
            // Chọn trục đúng với hướng cánh quạt của em:
            // Vector3.forward = trục Z, đổi thành right/up nếu cần.
            transform.Rotate(Vector3.forward, speed * Time.deltaTime);
        }
    }

    // Hàm bật/tắt (dùng cho button)
    public void Toggle()
    {
        isOn = !isOn;
    }

    // Dùng nếu muốn set trực tiếp
    public void SetOn(bool value)
    {
        isOn = value;
    }
}
