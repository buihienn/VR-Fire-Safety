using UnityEngine;
using UnityEngine.InputSystem;

public class FireTestInput : MonoBehaviour
{
    [SerializeField] private FlameNode node;

    void Update()
    {
        if (node == null) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.iKey.wasPressedThisFrame)
            node.Ignite();

        if (Keyboard.current.oKey.wasPressedThisFrame)
            node.Extinguish();
    }
}