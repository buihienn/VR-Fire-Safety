using System.Collections;
using UnityEngine;

public class HoseBurnSequence : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private FlameNode hoseFireNode;
    [SerializeField] private FlameNode afterFireNode;
    [SerializeField] private FlameNode valveFireNode;
    [SerializeField] private GameObject hoseRoot;
    [SerializeField] private GasSystem gasSystem;

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float hoseBurnSeconds = 2f;
    [Min(0f)] [SerializeField] private float afterBurnSeconds = 4.5f;

    [Header("Start State")]
    [SerializeField] private bool deactivateAfterNodeOnStart = true;
    [SerializeField] private bool deactivateValveNodeOnStart = true;
    [SerializeField] private bool forceHoseLeakTrue = true;

    [Header("Debug")]
    [SerializeField] private bool sequenceRunning = false;
    [SerializeField] private bool sequenceCompleted = false;
    [SerializeField] private string stage = "Idle";
    [SerializeField] private float timerRemaining = 0f;

    private Coroutine sequenceRoutine;

    private void Start()
    {
        if (afterFireNode != null && deactivateAfterNodeOnStart)
        {
            afterFireNode.Extinguish();
            afterFireNode.gameObject.SetActive(false);
        }

        if (valveFireNode != null && deactivateValveNodeOnStart)
        {
            valveFireNode.Extinguish();
            valveFireNode.gameObject.SetActive(false);
        }

        if (forceHoseLeakTrue && gasSystem != null)
            gasSystem.hoseLeak = true;
    }

    private void Update()
    {
        if (sequenceCompleted) return;
        if (sequenceRunning) return;
        if (hoseFireNode == null) return;

        // Khi hose node bắt đầu cháy thì sequence bắt đầu
        if (hoseFireNode.gameObject.activeInHierarchy && hoseFireNode.IsBurning)
        {
            sequenceRoutine = StartCoroutine(RunSequence());
        }
    }

    private IEnumerator RunSequence()
    {
        sequenceRunning = true;
        stage = "HoseBurning";
        timerRemaining = hoseBurnSeconds;

        // Giai đoạn 1: hose cháy trong 2 giây
        while (timerRemaining > 0f)
        {
            if (hoseFireNode == null ||
                !hoseFireNode.gameObject.activeInHierarchy ||
                !hoseFireNode.IsBurning)
            {
                AbortSequence("Hose extinguished before transition");
                yield break;
            }

            timerRemaining -= Time.deltaTime;
            yield return null;
        }

        // Nếu tới đây mà hose đã bị dập thì không chuyển
        if (hoseFireNode == null || !hoseFireNode.IsBurning)
        {
            AbortSequence("Hose extinguished at transition");
            yield break;
        }

        // Chuyển sang after node đúng vị trí / rotation của hose node
        if (afterFireNode == null)
        {
            AbortSequence("Missing afterFireNode");
            yield break;
        }

        afterFireNode.transform.position = hoseFireNode.transform.position;
        afterFireNode.transform.rotation = hoseFireNode.transform.rotation;
        afterFireNode.gameObject.SetActive(true);
        afterFireNode.SetVisualDamp01(1f);
        afterFireNode.Ignite();

        hoseFireNode.Extinguish();
        hoseFireNode.gameObject.SetActive(false);

        // Giai đoạn 2: after node cháy thêm 4.5 giây
        stage = "AfterNodeBurning";
        timerRemaining = afterBurnSeconds;

        while (timerRemaining > 0f)
        {
            if (afterFireNode == null ||
                !afterFireNode.gameObject.activeInHierarchy ||
                !afterFireNode.IsBurning)
            {
                AbortSequence("After node extinguished before valve stage");
                yield break;
            }

            timerRemaining -= Time.deltaTime;
            yield return null;
        }

        // Tắt after node
        if (afterFireNode != null)
        {
            afterFireNode.Extinguish();
            afterFireNode.gameObject.SetActive(false);
        }

        // Tắt cả hose
        stage = "DestroyHose";
        if (hoseRoot != null)
            hoseRoot.SetActive(false);

        // Bật cháy ở đầu vòi bình gas
        stage = "ValveFire";
        if (valveFireNode != null)
        {
            valveFireNode.gameObject.SetActive(true);
            valveFireNode.SetVisualDamp01(1f);
            valveFireNode.Ignite();
        }

        sequenceCompleted = true;
        sequenceRunning = false;
        timerRemaining = 0f;
        stage = "Completed";
        sequenceRoutine = null;
    }

    private void AbortSequence(string reason)
    {
        sequenceRunning = false;
        timerRemaining = 0f;
        stage = reason;
        sequenceRoutine = null;
    }

    [ContextMenu("Reset Sequence State")]
    public void ResetSequenceState()
    {
        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = null;
        sequenceRunning = false;
        sequenceCompleted = false;
        timerRemaining = 0f;
        stage = "Idle";

        if (afterFireNode != null)
        {
            afterFireNode.Extinguish();
            afterFireNode.gameObject.SetActive(false);
        }

        if (valveFireNode != null && deactivateValveNodeOnStart)
        {
            valveFireNode.Extinguish();
            valveFireNode.gameObject.SetActive(false);
        }

        if (forceHoseLeakTrue && gasSystem != null)
            gasSystem.hoseLeak = true;
    }
}