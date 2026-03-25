using System.Collections;
using UnityEngine;

public class FanButton : MonoBehaviour
{
    [Header("Button Visual")]
    [SerializeField] private float offAngle = 98f;
    [SerializeField] private float onAngle = 82f;

    [Header("Electric Spark")]
    [SerializeField] private ParticleSystem sparksFx;
    [SerializeField] private float igniteDelay = 0.05f;

    [Header("Fire")]
    [SerializeField] private FlameNode[] nodesToIgnite;
    [SerializeField] private bool triggerOnlyWhenTurningOn = true;
    [SerializeField] private bool igniteOnlyOnce = true;

    private bool isPressed = false;
    private bool hasIgnited = false;
    private Coroutine igniteRoutine;

    // Khởi tạo góc nút và đảm bảo tia lửa đã dừng khi bắt đầu.
    void Start()
    {
        SetAngle(offAngle);

        if (sparksFx != null)
            sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Được gọi từ sự kiện chọn để bật/tắt nút và có thể kích hoạt đánh lửa.
    public void PressButton()
    {
        isPressed = !isPressed;
        SetAngle(isPressed ? onAngle : offAngle);

        bool shouldTrigger = !triggerOnlyWhenTurningOn || isPressed;
        if (!shouldTrigger) return;

        if (igniteOnlyOnce && hasIgnited) return;

        PlaySparks();

        if (igniteRoutine != null)
            StopCoroutine(igniteRoutine);

        igniteRoutine = StartCoroutine(IgniteAfterDelay());
    }

    // Chờ một khoảng ngắn trước khi đánh lửa các nút lửa đã cấu hình.
    private IEnumerator IgniteAfterDelay()
    {
        yield return new WaitForSeconds(igniteDelay);

        if (nodesToIgnite != null)
        {
            foreach (var node in nodesToIgnite)
            {
                if (node != null)
                    node.Ignite();
            }
        }

        hasIgnited = true;
    }

    // Khởi động lại hiệu ứng tia lửa để tạo tín hiệu đánh lửa rõ ràng.
    private void PlaySparks()
    {
        if (sparksFx == null) return;

        sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparksFx.Play(true);
    }

    // Đặt góc xoay Y cục bộ để khớp trạng thái hiển thị của nút.
    private void SetAngle(float angle)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = angle;
        transform.localEulerAngles = euler;
    }
}