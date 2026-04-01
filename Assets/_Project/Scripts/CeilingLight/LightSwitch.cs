using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightSwitch : MonoBehaviour
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

    [System.Serializable]
    public class CeilingLightElement
    {
        [Header("Diffuse with Emission")]
        public Renderer diffuseRenderer;

        [Header("Point Light")]
        public Light pointLight;

        [HideInInspector] public Color diffuseEmissionColor;
    }

    [Header("Ceiling Light Elements")]
    [SerializeField] private List<CeilingLightElement> elements = new List<CeilingLightElement>();

    private bool isPressed = false;
    private bool hasIgnited = false;
    private Coroutine igniteRoutine;

    // Khởi tạo góc nút, cache emission và đảm bảo tia lửa đã dừng khi bắt đầu.
    void Start()
    {
        SetAngle(offAngle);
        CacheEmissionColors();

        if (sparksFx != null)
            sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        SetLights(false);
    }

    // Được gọi từ sự kiện chọn để bật/tắt nút, đèn và có thể kích hoạt đánh lửa.
    public void PressButton()
    {
        isPressed = !isPressed;
        Debug.Log($"[LightSwitch] Switch is {(isPressed ? "ON" : "OFF")}", this);
        SetAngle(isPressed ? onAngle : offAngle);
        SetLights(isPressed);

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

    // Bật/tắt emission và point light cho từng phần tử đèn trần.
    private void SetLights(bool state)
    {
        foreach (var element in elements)
        {
            if (element == null) continue;

            SetEmission(element.diffuseRenderer, element.diffuseEmissionColor, state);

            if (element.pointLight != null)
                element.pointLight.enabled = state;
        }
    }

    private void CacheEmissionColors()
    {
        foreach (var element in elements)
        {
            if (element == null) continue;

            element.diffuseEmissionColor = GetEmissionColor(element.diffuseRenderer);
        }
    }

    private static Color GetEmissionColor(Renderer renderer)
    {
        if (renderer == null || renderer.sharedMaterial == null)
            return Color.black;

        return renderer.sharedMaterial.GetColor("_EmissionColor");
    }

    private static void SetEmission(Renderer renderer, Color emissionColor, bool enabled)
    {
        if (renderer == null || renderer.material == null)
            return;

        if (enabled)
        {
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", emissionColor);
        }
        else
        {
            renderer.material.SetColor("_EmissionColor", Color.black);
            renderer.material.DisableKeyword("_EMISSION");
        }
    }

    // Đặt góc xoay Y cục bộ để khớp trạng thái hiển thị của nút.
    private void SetAngle(float angle)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = angle;
        transform.localEulerAngles = euler;
    }
}
