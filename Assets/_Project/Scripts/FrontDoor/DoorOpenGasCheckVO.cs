using UnityEngine;

public class DoorOpenGasCheckVO : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openThresholdDeg = 25f;

    [Header("Gas")]
    [SerializeField] private GasSystem gasSystem;
    [SerializeField] private bool requireGasStillOpen = true;

    [Header("VO")]
    [SerializeField] private string voKey = "VO_RightAction";
    [SerializeField] private bool playOncePerOpen = true;
    [SerializeField] private bool playOncePerScene = true;

    private bool hasPlayedThisOpen = false;
    private static bool hasPlayedInScene = false;

    private void Awake()
    {
        if (!gasSystem)
            gasSystem = GasSystem.Instance;
    }

    private void Update()
    {
        if (doorPivot == null) return;

        bool isOpenEnough = IsDoorOpenEnough();

        if (!isOpenEnough)
        {
            if (playOncePerOpen)
                hasPlayedThisOpen = false;
            return;
        }

        if (playOncePerOpen && hasPlayedThisOpen)
            return;

        if (playOncePerScene && hasPlayedInScene)
            return;

        if (requireGasStillOpen && gasSystem != null && !gasSystem.MainSupplyOpen)
            return;

        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayOneShot(voKey);
        hasPlayedThisOpen = true;
        hasPlayedInScene = true;
    }

    private bool IsDoorOpenEnough()
    {
        float angle = doorPivot.localEulerAngles.y;
        if (angle > 180f) angle -= 360f;
        return Mathf.Abs(angle) >= openThresholdDeg;
    }
}
