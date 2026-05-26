using System.Collections;
using UnityEngine;

public class LighterIgniteOnGrab : MonoBehaviour
{
    [Header("Fire Effect")]
    [SerializeField] private GameObject fireEffectObject;
    [SerializeField] private float igniteDelay = 2f;

    [Header("Behavior")]
    [SerializeField] private bool keepFireOnAfterRelease = true;

    [Header("Optional")]
    [SerializeField] private AudioSource igniteSound;

    private ParticleSystem[] fireParticles;
    private Coroutine igniteRoutine;
    private bool isGrabbed;
    private bool isFireOn;

    private void Awake()
    {
        if (fireEffectObject != null)
        {
            fireParticles = fireEffectObject.GetComponentsInChildren<ParticleSystem>(true);
            SetFire(false);
        }
        else
        {
            Debug.LogWarning($"{name}: Fire Effect Object chưa được gán.");
        }
    }

    public void OnGrab()
    {
        isGrabbed = true;

        if (isFireOn) return;

        if (igniteRoutine != null)
            StopCoroutine(igniteRoutine);

        igniteRoutine = StartCoroutine(IgniteAfterDelay());
    }

    public void OnRelease()
    {
        isGrabbed = false;

        if (igniteRoutine != null)
        {
            StopCoroutine(igniteRoutine);
            igniteRoutine = null;
        }

        if (!keepFireOnAfterRelease)
        {
            SetFire(false);
        }
    }

    private IEnumerator IgniteAfterDelay()
    {
        yield return new WaitForSeconds(igniteDelay);

        igniteRoutine = null;

        if (!isGrabbed) yield break;

        SetFire(true);

        if (igniteSound != null)
            igniteSound.Play();
    }

    public void TurnOffFire()
    {
        SetFire(false);
    }

    private void SetFire(bool active)
    {
        isFireOn = active;

        if (fireEffectObject != null)
            fireEffectObject.SetActive(active);

        if (fireParticles == null) return;

        foreach (ParticleSystem ps in fireParticles)
        {
            if (ps == null) continue;

            if (active)
            {
                ps.Play(true);
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}