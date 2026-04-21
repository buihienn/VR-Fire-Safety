using UnityEngine;

public class QuitButton : MonoBehaviour
{
    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("QuitGame called - Application.Quit() does not work in Editor.");
#else
        Application.Quit();
#endif
    }
}
