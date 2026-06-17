using Fusion;
using UnityEngine;

public class NetworkLightOnGrab : NetworkBehaviour
{
    [SerializeField] private Light[] lights;
    [SerializeField] private bool turnOffOnStart = true;

    [Networked] private bool LightOn { get; set; }

    private bool spawned;
    private bool lastLightOn;

    private void Awake()
    {
        if (lights == null || lights.Length == 0)
            lights = GetComponentsInChildren<Light>(true);

        if (turnOffOnStart)
            ApplyLight(false);
    }

    public override void Spawned()
    {
        spawned = true;

        if (Object.HasStateAuthority)
            LightOn = false;

        lastLightOn = LightOn;
        ApplyLight(LightOn);
    }

    public override void Render()
    {
        if (!spawned) return;

        if (LightOn == lastLightOn) return;

        lastLightOn = LightOn;
        ApplyLight(LightOn);
    }

    public void OnGrabbed()
    {
        SetLight(true);
    }

    public void OnReleased()
    {
        SetLight(false);
    }

    private void SetLight(bool value)
    {
        // Single-player fallback
        if (!spawned)
        {
            ApplyLight(value);
            return;
        }

        // Local preview cho người cầm thấy sáng ngay
        ApplyLight(value);

        if (Object.HasStateAuthority)
        {
            LightOn = value;
            RPC_ApplyLight(value);
        }
        else
        {
            RPC_RequestSetLight(value);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSetLight(bool value)
    {
        LightOn = value;
        RPC_ApplyLight(value);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyLight(bool value)
    {
        ApplyLight(value);
    }

    private void ApplyLight(bool value)
    {
        if (lights == null) return;

        foreach (Light l in lights)
        {
            if (l == null) continue;

            l.gameObject.SetActive(value);
            l.enabled = value;
        }
    }
}