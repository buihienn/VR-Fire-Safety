using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class StartScenePlayerBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject scenePlayer;

    private void Awake()
    {
        if (scenePlayer == null)
        {
            Debug.LogError(
                $"[{nameof(StartScenePlayerBootstrap)}] Scene Player reference is missing on {name}.",
                this);
            return;
        }

        if (PersistentLocalPlayer.HasInstance)
        {
            scenePlayer.SetActive(false);
            return;
        }

        scenePlayer.SetActive(true);
    }
}
