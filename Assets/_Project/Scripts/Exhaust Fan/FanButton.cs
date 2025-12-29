using UnityEngine;

public class FanButton : MonoBehaviour
{
    public float offAngle = 98f;
    public float onAngle = 82f;

    private bool isPressed = false;

    void Start()
    {
        SetAngle(offAngle); // góc ban đầu
    }

    // GỌI TỪ When Select()
    public void PressButton()
    {
        isPressed = !isPressed;
        SetAngle(isPressed ? onAngle : offAngle);
    }

    void SetAngle(float angle)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = angle;
        transform.localEulerAngles = euler;
    }
}
