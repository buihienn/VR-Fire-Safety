using UnityEngine;

public class NozzleFrostBySmoke : MonoBehaviour
{
    private enum FrostStage
    {
        Black = 0,
        Frost1 = 1,
        Frost2 = 2,
        Frost3 = 3
    }

    [Header("Smoke Source")]
    public NozzleFireSmokeTrigger smokeTrigger;

    [Header("Target Renderer")]
    public Renderer targetRenderer;

    [Tooltip("If the renderer has only one material slot, keep this at 0.")]
    public int materialSlot = 0;

    [Header("Frost Materials")]
    public Material blackMaterial;
    public Material frost1Material;
    public Material frost2Material;
    public Material frost3Material;

    [Header("Frost Build-Up (seconds of actual spray)")]
    [Tooltip("Frost Level 1 starts after this many seconds of actual spray.")]
    public float frost1At = 3f;

    [Tooltip("Frost Level 2 starts after this many seconds of actual spray.")]
    public float frost2At = 5f;

    [Tooltip("Frost Level 3 starts after this many seconds of actual spray.")]
    public float frost3At = 10f;

    [Tooltip("Maximum visual frost time used to cap frost accumulation.")]
    public float maxVisualFrostTime = 12f;

    [Header("Frost Cooling")]
    [Tooltip("How long frost stays unchanged after spraying stops before it starts fading.")]
    public float holdAfterStop = 1.0f;

    [Tooltip("Frost fade speed. 1 = fades at the same speed as it builds. Less than 1 = slower fade.")]
    public float cooldownMultiplier = 0.75f;

    [Header("Runtime Debug")]
    [SerializeField] private float accumulatedSprayTime = 0f;
    [SerializeField] private float stopTimer = 0f;
    [SerializeField] private bool isActuallySpraying = false;
    [SerializeField] private FrostStage currentStage = FrostStage.Black;

    private Material[] runtimeMaterials;

    private void Awake()
    {
        if (targetRenderer != null)
            runtimeMaterials = targetRenderer.materials;

        ApplyStage(FrostStage.Black, true);
    }

    private void OnValidate()
    {
        if (frost2At < frost1At) frost2At = frost1At;
        if (frost3At < frost2At) frost3At = frost2At;
        if (maxVisualFrostTime < frost3At) maxVisualFrostTime = frost3At;
        if (cooldownMultiplier < 0f) cooldownMultiplier = 0f;
        if (holdAfterStop < 0f) holdAfterStop = 0f;
        if (materialSlot < 0) materialSlot = 0;
    }

    private void Update()
    {
        isActuallySpraying = CheckActualSpraying();

        if (isActuallySpraying)
        {
            stopTimer = 0f;
            accumulatedSprayTime += Time.deltaTime;
            accumulatedSprayTime = Mathf.Min(accumulatedSprayTime, maxVisualFrostTime);
        }
        else
        {
            stopTimer += Time.deltaTime;

            if (stopTimer >= holdAfterStop)
            {
                accumulatedSprayTime -= Time.deltaTime * cooldownMultiplier;
                if (accumulatedSprayTime < 0f)
                    accumulatedSprayTime = 0f;
            }
        }

        FrostStage nextStage = EvaluateStage(accumulatedSprayTime);

        if (nextStage != currentStage)
            ApplyStage(nextStage);
    }

    private bool CheckActualSpraying()
    {
        if (smokeTrigger == null)
            return false;

        if (smokeTrigger.IsEmpty)
            return false;

        if (smokeTrigger.fireSmoke == null)
            return false;

        return smokeTrigger.fireSmoke.isPlaying;
    }

    private FrostStage EvaluateStage(float t)
    {
        if (t >= frost3At) return FrostStage.Frost3;
        if (t >= frost2At) return FrostStage.Frost2;
        if (t >= frost1At) return FrostStage.Frost1;
        return FrostStage.Black;
    }

    private void ApplyStage(FrostStage newStage, bool force = false)
    {
        if (!force && newStage == currentStage)
            return;

        currentStage = newStage;

        if (targetRenderer == null)
            return;

        if (runtimeMaterials == null || runtimeMaterials.Length == 0)
            runtimeMaterials = targetRenderer.materials;

        if (materialSlot < 0 || materialSlot >= runtimeMaterials.Length)
        {
            Debug.LogWarning($"NozzleFrostBySmoke: Material slot {materialSlot} is invalid on {targetRenderer.name}");
            return;
        }

        Material targetMat = GetMaterialForStage(newStage);
        if (targetMat == null)
            return;

        runtimeMaterials[materialSlot] = targetMat;
        targetRenderer.materials = runtimeMaterials;
    }

    private Material GetMaterialForStage(FrostStage stage)
    {
        switch (stage)
        {
            case FrostStage.Frost1: return frost1Material != null ? frost1Material : blackMaterial;
            case FrostStage.Frost2: return frost2Material != null ? frost2Material : frost1Material;
            case FrostStage.Frost3: return frost3Material != null ? frost3Material : frost2Material;
            default: return blackMaterial;
        }
    }

    public void ResetFrostNow()
    {
        accumulatedSprayTime = 0f;
        stopTimer = 0f;
        isActuallySpraying = false;
        ApplyStage(FrostStage.Black, true);
    }
}