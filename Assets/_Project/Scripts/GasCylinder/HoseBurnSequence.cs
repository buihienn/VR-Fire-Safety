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
    [SerializeField] private bool extinguishAfterNodeOnStart = true;
    [SerializeField] private bool extinguishValveNodeOnStart = true;
    [SerializeField] private bool forceHoseLeakTrue = true;

    [Header("Hose Visual")]
    [Tooltip("Khi dây cháy xong thì ẩn model dây gas. Nếu FlameNode nằm trong hoseRoot thì nó cũng bị inactive.")]
    [SerializeField] private bool hideHoseRootWhenBurned = true;

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

    private const float ForceExtinguishAmount = 999999f;

    private void Awake()
    {
        if (!gasSystem)
            gasSystem = GasSystem.Instance;
    }

    private void Start()
    {
        // Không SetActive(false) FlameNode nữa.
        // FlameNode nên luôn active để FireManager có thể sync state.
        if (afterFireNode != null)
        {
            afterFireNode.gameObject.SetActive(true);

            if (extinguishAfterNodeOnStart)
                ExtinguishNodeLocalOnly(afterFireNode);
        }

        if (valveFireNode != null)
        {
            valveFireNode.gameObject.SetActive(true);

            if (extinguishValveNodeOnStart)
                ExtinguishNodeLocalOnly(valveFireNode);
        }

        if (forceHoseLeakTrue && gasSystem != null)
            gasSystem.SetHoseLeak(true);

        previousHoseBurning = IsNodeBurning(hoseFireNode);
    }

    private void Update()
    {
        if (FireManager.Instance != null && !FireManager.Instance.HasFireAuthority)
            return;
        UpdateHoseAudio();

        if (sequenceCompleted) return;
        if (sequenceRunning) return;
        if (hoseFireNode == null) return;

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

        afterFireNode.gameObject.SetActive(true);
        afterFireNode.transform.position = hoseFireNode.transform.position;
        afterFireNode.transform.rotation = hoseFireNode.transform.rotation;
        afterFireNode.SetVisualDamp01(1f);

        IgniteNode(afterFireNode);
        ExtinguishNode(hoseFireNode);

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

        ExtinguishNode(afterFireNode);

        stage = "DestroyHose";

        if (hideHoseRootWhenBurned && hoseRoot != null)
            hoseRoot.SetActive(false);

        SetHoseLeakLoop(false);

        stage = "ValveFire";

        if (valveFireNode != null)
        {
            valveFireNode.gameObject.SetActive(true);
            valveFireNode.SetVisualDamp01(1f);
            IgniteNode(valveFireNode);
        }

        sequenceCompleted = true;
        sequenceRunning = false;
        timerRemaining = 0f;
        stage = "Completed";
        sequenceRoutine = null;
    }

    private void IgniteNode(FlameNode node)
    {
        if (node == null) return;

        node.gameObject.SetActive(true);

        if (FireManager.Instance != null)
        {
            FireManager.Instance.RequestIgnite(node);
        }
        else
        {
            node.Ignite();
        }
    }

    private void ExtinguishNode(FlameNode node)
    {
        if (node == null) return;

        if (FireManager.Instance != null)
        {
            FireManager.Instance.RequestExtinguish(node, ForceExtinguishAmount);
        }
        else
        {
            node.Extinguish();
        }
    }

    private void ExtinguishNodeLocalOnly(FlameNode node)
    {
        if (node == null) return;

        node.Extinguish();
        node.SetVisualDamp01(1f);
    }

    private void UpdateHoseAudio()
    {
        if (!manageHoseAudio) return;

        bool hoseBurning = IsNodeBurning(hoseFireNode);
        bool hoseLeakActive = IsHoseLeakActive();

        bool shouldPlayLeakLoop =
            hoseLeakActive &&
            !hoseBurning &&
            !sequenceRunning &&
            !sequenceCompleted &&
            IsHosePresent();

        SetHoseLeakLoop(shouldPlayLeakLoop);

        bool justStartedBurning = !previousHoseBurning && hoseBurning;
        if (justStartedBurning)
        {
            SetHoseLeakLoop(false);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayOneShot(hoseBurstSound);
        }

        previousHoseBurning = hoseBurning;
    }

    private bool IsHoseLeakActive()
    {
        if (gasSystem == null) return false;
        if (!IsHosePresent()) return false;

        return gasSystem.CanSustainNozzleFire();
    }

    private bool IsHosePresent()
    {
        return hoseRoot == null || hoseRoot.activeInHierarchy;
    }

    private bool IsNodeBurning(FlameNode node)
    {
        if (node == null) return false;

        if (FireManager.Instance != null)
            return FireManager.Instance.IsNodeBurning(node);

        return node.IsBurning;
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
            afterFireNode.gameObject.SetActive(true);
            ExtinguishNode(afterFireNode);
        }

        if (valveFireNode != null && extinguishValveNodeOnStart)
        {
            valveFireNode.gameObject.SetActive(true);
            ExtinguishNode(valveFireNode);
        }

        if (forceHoseLeakTrue && gasSystem != null)
            gasSystem.SetHoseLeak(true);

        SetHoseLeakLoop(false);
        previousHoseBurning = IsNodeBurning(hoseFireNode);
    }
}