using UnityEngine;

public class NozzleFireSmokeTrigger : MonoBehaviour
{
    [Header("Smoke")]
    public ParticleSystem fireSmoke;
    public float delay = 1f;

    [Header("Safety Pin Requirement")]
    public bool requirePinRemoved = true;
    public SafetyPinDetachOnPull safetyPin;

    private Coroutine routine;

    public void OnGrab()
    {
        if (requirePinRemoved)
        {
            if (safetyPin == null)
            {
                Debug.LogWarning("NozzleFireSmokeTrigger: chưa gán SafetyPinRelease.");
                return;
            }

            if (!safetyPin.IsRemoved)
            {
                // Chưa rút chốt thì không cho xịt
                return;
            }
        }

        if (routine == null)
            routine = StartCoroutine(StartAfterDelay());
    }

    public void OnRelease()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (fireSmoke != null)
            fireSmoke.Stop();
    }

    private System.Collections.IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(delay);

        if (fireSmoke != null)
            fireSmoke.Play();

        routine = null;
    }
}