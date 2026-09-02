using UnityEngine;

public class GasValveClosedWindowCheckVO : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private Transform windowPivot;
    [SerializeField] private float closedThresholdDeg = 5f;

    [Header("Gas")]
    [SerializeField] private GasSystem gasSystem;

    [Header("VO")]
    [SerializeField] private string voKey = "VO_RightAction";
    [SerializeField] private bool playOncePerScene = false;

    private bool previousMainSupplyOpen;
    private bool hasPlayedInScene = false;

    private void Awake()
    {
        if (!gasSystem)
            gasSystem = GasSystem.Instance;

        previousMainSupplyOpen = gasSystem != null && gasSystem.MainSupplyOpen;
    }

    private void Update()
    {
        if (gasSystem == null || windowPivot == null) return;

        bool mainSupplyOpen = gasSystem.MainSupplyOpen;
        bool justClosedValve = previousMainSupplyOpen && !mainSupplyOpen;

        if (justClosedValve && IsWindowClosed())
            TryPlayVo();

        previousMainSupplyOpen = mainSupplyOpen;
    }

    private bool IsWindowClosed()
    {
        float angle = windowPivot.localEulerAngles.y;
        if (angle > 180f) angle -= 360f;
        return Mathf.Abs(angle) <= closedThresholdDeg;
    }

    private void TryPlayVo()
    {
        if (playOncePerScene && hasPlayedInScene)
            return;

        if (AudioManager.Instance == null)
            return;

        // GasValveLeakByAngle owns the valve-close VO. Avoid replaying the
        // same clip from this legacy window-state check in the same moment.
        if (AudioManager.Instance.IsPlaying(voKey))
        {
            hasPlayedInScene = true;
            return;
        }

        AudioManager.Instance.PlayOneShot(voKey);
        hasPlayedInScene = true;
    }
}
