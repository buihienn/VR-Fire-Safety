using UnityEngine;

public class NozzleFireSmokeTrigger : MonoBehaviour
{
    public ParticleSystem fireSmoke;
    public float delay = 1f;

    Coroutine routine;

    public void OnGrab()
    {
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
        if (fireSmoke != null) fireSmoke.Stop();
    }

    System.Collections.IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        if (fireSmoke != null) fireSmoke.Play();
        routine = null;
    }
}
