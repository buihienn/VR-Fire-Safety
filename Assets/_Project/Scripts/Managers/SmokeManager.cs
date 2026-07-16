using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class SmokeManager : NetworkBehaviour
{
    public static SmokeManager Instance { get; private set; }

    [Header("Smoke Nodes")]
    [Tooltip("Tự động tìm tất cả CeilingSmokeNode nằm bên dưới SmokeManager.")]
    [SerializeField] private List<CeilingSmokeNode> smokeNodes = new();

    [Header("Manual Testing")]
    [Tooltip("Bật để kiểm tra khói bằng thanh Manual Smoke 01.")]
    [SerializeField] private bool useManualControl = true;

    [Range(0f, 1f)]
    [SerializeField] private float manualSmoke01;

    [Header("Smoke Growth")]
    [Tooltip("Tốc độ tăng khói của một FlameNode đang cháy hoàn toàn.")]
    [Min(0f)]
    [SerializeField] private float buildRatePerFire = 0.02f;

    [Tooltip("Giới hạn tổng đóng góp của nhiều đám cháy.")]
    [Min(0.1f)]
    [SerializeField] private float maximumFireContribution = 5f;

    [Tooltip("Tốc độ khói tự giảm khi không còn đám cháy.")]
    [Min(0f)]
    [SerializeField] private float naturalClearRate = 0.003f;

    [Header("Window Exhaust")]
    [Tooltip("When there is no active fire, open windows clear smoke faster.")]
    [SerializeField] private bool clearFasterWhenWindowOpen = true;

    [Tooltip("Additional clear rate for each fully open window.")]
    [Min(0f)]
    [SerializeField] private float windowClearRate = 0.03f;

    [Header("Network State")]
    [Networked]
    public float Smoke01Net { get; private set; }

    public float Smoke01 =>
        Object != null && Object.IsValid
            ? Smoke01Net
            : manualSmoke01;

    public bool HasSmokeAuthority =>
        Object != null &&
        Object.IsValid &&
        Object.HasStateAuthority;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"Có nhiều SmokeManager trong scene. Object thừa: {name}",
                this
            );
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        RefreshSmokeNodes();
        ApplySmokeVisual(0f);
    }

    public override void Spawned()
    {
        RefreshSmokeNodes();

        // Không cần gán lại Smoke01Net ở đây.
        // Giá trị mặc định ban đầu của Networked float đã là 0.
        ApplySmokeVisual(Smoke01Net);
    }

    public override void FixedUpdateNetwork()
    {
        // Trong Shared Mode, chỉ State Authority/Master Client
        // được phép thay đổi trạng thái khói.
        if (!Object.HasStateAuthority)
            return;

        if (useManualControl)
        {
            Smoke01Net = Mathf.Clamp01(manualSmoke01);
            return;
        }

        UpdateSmokeFromFires();
    }

    public override void Render()
    {
        // Mỗi máy tự chạy particle dựa trên cùng một giá trị network.
        ApplySmokeVisual(Smoke01Net);
    }

    private void UpdateSmokeFromFires()
    {
        float totalFireStrength = GetTotalFireStrength();

        if (totalFireStrength > 0f)
        {
            float contribution = Mathf.Min(
                totalFireStrength,
                maximumFireContribution
            );

            Smoke01Net +=
                contribution *
                buildRatePerFire *
                Runner.DeltaTime;
        }
        else
        {
            float clearRate = naturalClearRate;

            if (clearFasterWhenWindowOpen && GasSystem.Instance != null)
                clearRate += windowClearRate * GasSystem.Instance.WindowOpen01();

            Smoke01Net -= clearRate * Runner.DeltaTime;
        }

        Smoke01Net = Mathf.Clamp01(Smoke01Net);
    }

    private float GetTotalFireStrength()
    {
        float totalStrength = 0f;

        foreach (FlameNode flame in FlameNode.All)
        {
            if (flame == null)
                continue;

            if (!flame.IsBurning)
                continue;

            totalStrength += flame.Burn01;
        }

        return totalStrength;
    }

    private void ApplySmokeVisual(float smoke01)
    {
        smoke01 = Mathf.Clamp01(smoke01);

        for (int i = smokeNodes.Count - 1; i >= 0; i--)
        {
            CeilingSmokeNode node = smokeNodes[i];

            if (node == null)
            {
                smokeNodes.RemoveAt(i);
                continue;
            }

            node.ApplyGlobalSmoke(smoke01);
        }
    }

    [ContextMenu("Refresh Smoke Nodes")]
    public void RefreshSmokeNodes()
    {
        smokeNodes.Clear();

        smokeNodes.AddRange(
            GetComponentsInChildren<CeilingSmokeNode>(true)
        );
    }

    [ContextMenu("Clear Smoke")]
    public void RequestClearSmoke()
    {
        if (Object == null || !Object.IsValid)
        {
            manualSmoke01 = 0f;
            ApplySmokeVisual(0f);
            ClearAllParticles();
            return;
        }

        if (Object.HasStateAuthority)
        {
            ClearSmokeAuthority();
        }
        else
        {
            RPC_RequestClearSmoke();
        }
    }

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority
    )]
    private void RPC_RequestClearSmoke()
    {
        ClearSmokeAuthority();
    }

    private void ClearSmokeAuthority()
    {
        if (!Object.HasStateAuthority)
            return;

        Smoke01Net = 0f;
    }

    private void ClearAllParticles()
    {
        foreach (CeilingSmokeNode node in smokeNodes)
        {
            if (node != null)
                node.ClearImmediately();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        manualSmoke01 = Mathf.Clamp01(manualSmoke01);
        buildRatePerFire = Mathf.Max(0f, buildRatePerFire);
        maximumFireContribution =
            Mathf.Max(0.1f, maximumFireContribution);
        naturalClearRate = Mathf.Max(0f, naturalClearRate);
        windowClearRate = Mathf.Max(0f, windowClearRate);
    }
}
