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

    [Header("Audio - Hose")]
    [SerializeField] private bool manageHoseAudio = true;
    [SerializeField] private string hoseLeakLoopSound = "GasLeakLoop";
    [SerializeField] private string hoseBurstSound = "GasBurst";

    [Header("Debug")]
    [SerializeField] private bool sequenceRunning = false;
    [SerializeField] private bool sequenceCompleted = false;
    [SerializeField] private string stage = "Idle";
    [SerializeField] private float timerRemaining = 0f;

    private Coroutine sequenceRoutine;
    private bool previousHoseBurning;

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

        // Sync state ban đầu để không phát burst ngay lúc vào scene
        previousHoseBurning = IsNodeBurning(hoseFireNode);
    }

    private void Update()
    {
        UpdateHoseAudio();

        if (sequenceCompleted) return;
        if (sequenceRunning) return;
        if (hoseFireNode == null) return;

        // Khi hose node bắt đầu cháy thì sequence bắt đầu
        if (IsNodeBurning(hoseFireNode))
        {
            sequenceRoutine = StartCoroutine(RunSequence());
        }
    }

    private IEnumerator RunSequence()
    {
        sequenceRunning = true;
        stage = "HoseBurning";
        timerRemaining = hoseBurnSeconds;

        while (timerRemaining > 0f)
        {
            if (!IsNodeBurning(hoseFireNode))
            {
                AbortSequence("Hose extinguished before transition");
                yield break;
            }

            timerRemaining -= Time.deltaTime;
            yield return null;
        }

        if (!IsNodeBurning(hoseFireNode))
        {
            AbortSequence("Hose extinguished at transition");
            yield break;
        }

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

        stage = "AfterNodeBurning";
        timerRemaining = afterBurnSeconds;

        while (timerRemaining > 0f)
        {
            if (!IsNodeBurning(afterFireNode))
            {
                AbortSequence("After node extinguished before valve stage");
                yield break;
            }

            timerRemaining -= Time.deltaTime;
            yield return null;
        }

        if (afterFireNode != null)
        {
            afterFireNode.Extinguish();
            afterFireNode.gameObject.SetActive(false);
        }

        stage = "DestroyHose";
        if (hoseRoot != null)
            hoseRoot.SetActive(false);

        // Hose không còn là nguồn phát SFX leak nữa
        SetHoseLeakLoop(false);

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

    private void UpdateHoseAudio()
    {
        if (!manageHoseAudio) return;
        if (AudioManager.Instance == null) return;

        bool hoseBurning = IsNodeBurning(hoseFireNode);
        bool hoseLeakActive = IsHoseLeakActive();

        // Chỉ phát loop khi: còn hose, đang rò, chưa cháy, chưa hoàn tất sequence
        bool shouldPlayLeakLoop =
            hoseLeakActive &&
            !hoseBurning &&
            !sequenceRunning &&
            !sequenceCompleted &&
            IsHosePresent();

        SetHoseLeakLoop(shouldPlayLeakLoop);

        // Chỉ phát burst khi hose vừa mới bắt đầu cháy
        bool justStartedBurning = !previousHoseBurning && hoseBurning;
        if (justStartedBurning)
        {
            SetHoseLeakLoop(false);
            AudioManager.Instance.PlayOneShot(hoseBurstSound);
        }

        previousHoseBurning = hoseBurning;
    }

    private bool IsHoseLeakActive()
    {
        if (gasSystem == null) return false;
        if (!IsHosePresent()) return false;

        // Dùng logic tổng của GasSystem để tôn trọng trạng thái van chính
        return gasSystem.CanSustainNozzleFire();
    }

    private bool IsHosePresent()
    {
        return hoseRoot != null && hoseRoot.activeInHierarchy;
    }

    private bool IsNodeBurning(FlameNode node)
    {
        return node != null &&
               node.gameObject.activeInHierarchy &&
               node.IsBurning;
    }

    private void SetHoseLeakLoop(bool shouldPlay)
    {
        if (AudioManager.Instance == null) return;

        if (shouldPlay)
        {
            if (!AudioManager.Instance.IsPlaying(hoseLeakLoopSound))
                AudioManager.Instance.Play(hoseLeakLoopSound);
        }
        else
        {
            if (AudioManager.Instance.IsPlaying(hoseLeakLoopSound))
                AudioManager.Instance.Stop(hoseLeakLoopSound);
        }
    }

    private void AbortSequence(string reason)
    {
        sequenceRunning = false;
        timerRemaining = 0f;
        stage = reason;
        sequenceRoutine = null;

        // Abort thì hose leak có thể quay lại, UpdateHoseAudio() frame sau sẽ tự xử lý
        SetHoseLeakLoop(false);
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

        SetHoseLeakLoop(false);
        previousHoseBurning = IsNodeBurning(hoseFireNode);
    }
}