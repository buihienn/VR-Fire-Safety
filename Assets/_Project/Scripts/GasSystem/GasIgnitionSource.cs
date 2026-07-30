using UnityEngine;

public class GasIgnitionSource : MonoBehaviour
{
    private enum ActivationMode
    {
        AlwaysActive,
        Manual,
        GameObjectActive,
        AnyParticlePlaying
    }

    [Header("Identity")]
    [SerializeField] private string sourceId = "IgnitionSource";
    [SerializeField] private Transform ignitionPoint;

    [Header("Activation")]
    [SerializeField] private ActivationMode activationMode = ActivationMode.AlwaysActive;
    [SerializeField] private bool manualActive = true;
    [SerializeField] private GameObject activeObject;
    [SerializeField] private ParticleSystem[] particles;

    public string SourceId =>
        string.IsNullOrWhiteSpace(sourceId) ? gameObject.name : sourceId;

    public Vector3 IgnitionPosition =>
        ignitionPoint != null ? ignitionPoint.position : transform.position;

    public bool IsIgniting
    {
        get
        {
            switch (activationMode)
            {
                case ActivationMode.AlwaysActive:
                    return true;

                case ActivationMode.Manual:
                    return manualActive;

                case ActivationMode.GameObjectActive:
                    return activeObject != null && activeObject.activeInHierarchy;

                case ActivationMode.AnyParticlePlaying:
                    if (particles == null)
                        return false;

                    foreach (ParticleSystem particle in particles)
                    {
                        if (particle != null && particle.isPlaying)
                            return true;
                    }

                    return false;

                default:
                    return false;
            }
        }
    }

    private void Awake()
    {
        if (activationMode == ActivationMode.AnyParticlePlaying &&
            (particles == null || particles.Length == 0))
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    public void SetActive(bool active)
    {
        manualActive = active;
    }
}
