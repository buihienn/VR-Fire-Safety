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

    void Start()
    {
        SetAngle(offAngle);

        if (sparksFx != null)
            sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // GỌI TỪ When Select()
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

    private void PlaySparks()
    {
        if (sparksFx == null) return;

        sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        sparksFx.Play(true);
    }

    private void SetAngle(float angle)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = angle;
        transform.localEulerAngles = euler;
    }
}