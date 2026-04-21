using UnityEngine;

public class NozzleFireSmokeTrigger : MonoBehaviour
{
    [Header("Smoke")]
    public ParticleSystem fireSmoke;
    public float delay = 1f;

    [Header("Safety Pin Requirement")]
    public bool requirePinRemoved = true;
    public SafetyPinDetachOnPull safetyPin;

    [Header("Spray Limit")]
    [Tooltip("Tổng thời gian được phép xịt cho cả bình (giây).")]
    public float maxSpraySeconds = 60f;

    [SerializeField, Tooltip("Thời gian xịt còn lại. Runtime sẽ tự giảm.")]
    private float remainingSpraySeconds = 60f;

    private Coroutine startRoutine;
    private bool isSpraying;

    public float RemainingSpraySeconds => remainingSpraySeconds;
    public bool IsEmpty => remainingSpraySeconds <= 0f;

    private void Awake()
    {
        // Mỗi lần vào scene thì bình bắt đầu đầy
        remainingSpraySeconds = maxSpraySeconds;
    }

    private void Update()
    {
        if (!isSpraying) return;

        remainingSpraySeconds -= Time.deltaTime;

        if (remainingSpraySeconds <= 0f)
        {
            remainingSpraySeconds = 0f;
            StopSpraying();
        }
    }

    public void OnGrab()
    {
        // Hết bình thì không cho xịt nữa
        if (IsEmpty)
            return;

        if (requirePinRemoved)
        {
            if (safetyPin == null)
            {
                Debug.LogWarning("NozzleFireSmokeTrigger: chưa gán SafetyPinDetachOnPull.");
                return;
            }

            if (!safetyPin.IsRemoved)
            {
                // Chưa rút chốt thì không cho xịt
                return;
            }
        }

        if (isSpraying || startRoutine != null)
            return;

        startRoutine = StartCoroutine(StartAfterDelay());
    }

    public void OnRelease()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        StopSpraying();
    }

    private System.Collections.IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        startRoutine = null;

        // Sau delay mà đã hết bình thì thôi
        if (IsEmpty)
            yield break;

        if (fireSmoke != null && !fireSmoke.isPlaying)
            fireSmoke.Play();

        // Sfx
        if (!AudioManager.Instance.IsPlaying("FESpray"))
        {
            AudioManager.Instance.Play("FESpray");
        }

        // Chỉ bắt đầu trừ thời gian từ lúc effect thực sự bắt đầu
        isSpraying = true;
    }

    private void StopSpraying()
    {
        isSpraying = false;

        if (fireSmoke != null && fireSmoke.isPlaying)
            fireSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Sfx
        AudioManager.Instance.Stop("FESpray");
    }
}