using UnityEngine;

public class SparkIgnitionTrigger : MonoBehaviour
{
    [Header("Spark FX")]
    [SerializeField] private ParticleSystem sparksFx;
    [SerializeField] private AudioSource sparkAudio;

    [Header("Gas Ignition")]
    [SerializeField] private bool enableGasIgnition;
    [SerializeField] private GasIgnitionController ignitionController;
    [Tooltip("When assigned, only this FlameNode may be ignited by this spark source.")]
    [SerializeField] private FlameNode ignitionNode;
    [SerializeField] private string sourceId = "KitchenLightSwitch";

    [Header("Options")]
    [SerializeField] private bool playSparkEvenIfCannotIgnite = true;
    [SerializeField] private bool debugLog = false;

    [ContextMenu("Debug/Trigger Spark")]
    public void TriggerSpark()
    {
        if (playSparkEvenIfCannotIgnite)
            PlaySparkFx();

        // The ignition request must still run when no AudioManager exists.
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOneShot("Spark");

        if (enableGasIgnition)
            TryIgniteFromSpark();
    }

    private void PlaySparkFx()
    {
        if (sparksFx != null)
        {
            sparksFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparksFx.Play(true);
        }

        if (sparkAudio != null)
            sparkAudio.Play();
    }

    private void TryIgniteFromSpark()
    {
        if (ignitionController == null)
            ignitionController = FindFirstObjectByType<GasIgnitionController>();

        if (ignitionController == null)
        {
            if (debugLog)
                Debug.LogWarning("[SparkIgnitionTrigger] GasIgnitionController is null.");
            return;
        }

        bool hasDedicatedNode = ignitionNode != null;
        Vector3 ignitionPosition = hasDedicatedNode
            ? ignitionNode.transform.position
            : transform.position;

        ignitionController.RequestIgnite(
            ignitionPosition,
            sourceId,
            requireExactFlameTarget: hasDedicatedNode);
    }
}
