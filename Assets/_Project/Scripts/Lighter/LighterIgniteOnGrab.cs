using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class LighterIgniteOnGrab : NetworkBehaviour
{
    [Header("Fire Effect")]
    [SerializeField] private GameObject fireEffectObject;
    [SerializeField] private float igniteDelay = 2f;

    [Header("Behavior")]
    [SerializeField] private bool keepFireOnAfterRelease = true;

    [Header("Multiplayer")]
    [Tooltip("Dùng cho Shared Mode. Khi cầm bật lửa thì xin StateAuthority để sync trạng thái cháy.")]
    [SerializeField] private bool requestAuthorityOnGrab = true;

    [Header("Optional")]
    [SerializeField] private AudioSource igniteSound;

    [Header("Desktop Test")]
    [Tooltip("Cho phep phim L bat/tat lua khi test khong co kinh VR.")]
    [SerializeField] private bool enableDesktopTestInput = true;

    [Networked] private bool FireOnNet { get; set; }

    private ParticleSystem[] fireParticles;
    private Coroutine igniteRoutine;
    private bool isGrabbed;
    private bool isFireOnLocal;
    private bool spawned;
    private bool lastFireOnNet;

    public bool IsFireOn => isFireOnLocal;

    private void Awake()
    {
        if (fireEffectObject != null)
        {
            fireParticles = fireEffectObject.GetComponentsInChildren<ParticleSystem>(true);
            ApplyFire(false, playSound: false);
        }
        else
        {
            Debug.LogWarning($"{name}: Fire Effect Object chưa được gán.");
        }
    }

    public override void Spawned()
    {
        spawned = true;

        if (Object.HasStateAuthority)
            FireOnNet = false;

        lastFireOnNet = FireOnNet;
        ApplyFire(FireOnNet, playSound: false);
    }

    public override void Render()
    {
        if (!spawned)
            return;

        if (FireOnNet == lastFireOnNet)
            return;

        lastFireOnNet = FireOnNet;
        ApplyFire(FireOnNet, playSound: true);
    }

    private void Update()
    {
        if (!enableDesktopTestInput || !Application.isPlaying)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.lKey.wasPressedThisFrame)
            return;

        RequestSetFire(!isFireOnLocal);
    }

    public void OnGrab()
    {
        isGrabbed = true;

        if (spawned && requestAuthorityOnGrab && Object != null && !Object.HasStateAuthority)
            Object.RequestStateAuthority();

        if (isFireOnLocal)
            return;

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
            RequestSetFire(false);
    }

    private IEnumerator IgniteAfterDelay()
    {
        yield return new WaitForSeconds(igniteDelay);

        igniteRoutine = null;

        if (!isGrabbed)
            yield break;

        RequestSetFire(true);
    }

    public void TurnOffFire()
    {
        RequestSetFire(false);
    }

    private void RequestSetFire(bool active)
    {
        if (!spawned)
        {
            ApplyFire(active, playSound: true);
            return;
        }

        // Local preview cho người cầm thấy phản hồi ngay.
        ApplyFire(active, playSound: true);

        if (Object.HasStateAuthority)
        {
            SetFireOnAuthority(active);
        }
        else
        {
            RPC_RequestSetFire(active);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetFire(bool active)
    {
        SetFireOnAuthority(active);
    }

    private void SetFireOnAuthority(bool active)
    {
        if (spawned && !Object.HasStateAuthority)
            return;

        FireOnNet = active;
        ApplyFire(active, playSound: true);

        RPC_ApplyFire(active);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyFire(bool active)
    {
        ApplyFire(active, playSound: true);
    }

    private void ApplyFire(bool active, bool playSound)
    {
        bool wasFireOn = isFireOnLocal;
        isFireOnLocal = active;

        if (fireEffectObject != null)
            fireEffectObject.SetActive(active);

        if (fireParticles != null)
        {
            foreach (ParticleSystem ps in fireParticles)
            {
                if (ps == null) continue;

                if (active)
                    ps.Play(true);
                else
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (active && !wasFireOn && playSound && igniteSound != null)
            igniteSound.Play();
    }
}
