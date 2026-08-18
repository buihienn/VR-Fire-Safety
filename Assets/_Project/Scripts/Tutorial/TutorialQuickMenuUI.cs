using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialQuickMenuUI : MonoBehaviour
{
    [Header("Editable UI hierarchy")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private RectTransform interactionCanvas;
    [SerializeField] private MonoBehaviour interactionBoundsClipper;
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject howToMoveCanvas;

    [Header("Interaction canvas sizes")]
    [SerializeField] private Vector2 closedCanvasSize = new(380f, 160f);
    [SerializeField] private Vector2 menuCanvasSize = new(1250f, 780f);
    [SerializeField] private Vector2 howToMoveCanvasSize = new(1500f, 920f);

    [Header("Buttons")]
    [SerializeField] private Button menuToggleButton;
    [SerializeField] private Button readyToPlayButton;
    [SerializeField] private Button howToMoveButton;
    [SerializeField] private Button closeHowToMoveButton;

    private bool isReturningToStartScene;

    private void OnEnable()
    {
        isReturningToStartScene = false;
        SetButtonsInteractable(true);

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }

        CloseAllPanels();
    }

    public void ToggleMenu()
    {
        if (menuCanvas == null)
        {
            return;
        }

        if (menuCanvas.activeSelf)
        {
            CloseAllPanels();
            return;
        }

        menuCanvas.SetActive(true);
        if (howToMoveCanvas != null) howToMoveCanvas.SetActive(false);
        ResizeInteractionCanvas(menuCanvasSize, true);
    }

    public void ReadyToPlay()
    {
        if (isReturningToStartScene)
        {
            return;
        }

        TutorialSceneController controller = FindFirstObjectByType<TutorialSceneController>();
        if (controller == null)
        {
            Debug.LogWarning($"[{nameof(TutorialQuickMenuUI)}] TutorialSceneController was not found.", this);
            return;
        }

        isReturningToStartScene = true;
        SetButtonsInteractable(false);
        controller.ReturnToStartScene();
    }

    public void ShowHowToMove()
    {
        if (menuCanvas != null)
        {
            menuCanvas.SetActive(false);
        }

        if (howToMoveCanvas != null)
        {
            howToMoveCanvas.SetActive(true);
        }

        ResizeInteractionCanvas(howToMoveCanvasSize, true);
    }

    public void HideHowToMove()
    {
        if (howToMoveCanvas != null)
        {
            howToMoveCanvas.SetActive(false);
        }

        CloseAllPanels();
    }

    private void CloseAllPanels()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (howToMoveCanvas != null) howToMoveCanvas.SetActive(false);
        ResizeInteractionCanvas(closedCanvasSize, false);
    }

    private void ResizeInteractionCanvas(Vector2 size, bool panelOpen)
    {
        if (interactionCanvas != null)
        {
            interactionCanvas.sizeDelta = size;
        }

        if (interactionBoundsClipper != null)
        {
            FieldInfo sizeField = interactionBoundsClipper.GetType().GetField(
                "_size",
                BindingFlags.Instance | BindingFlags.NonPublic);
            sizeField?.SetValue(interactionBoundsClipper, new Vector3(size.x, size.y, 0.01f));
        }

        if (menuToggleButton == null)
        {
            return;
        }

        RectTransform toggleRect = menuToggleButton.transform as RectTransform;
        if (toggleRect == null)
        {
            return;
        }

        Vector2 anchor = panelOpen ? Vector2.one : new Vector2(0.5f, 0.5f);
        toggleRect.anchorMin = anchor;
        toggleRect.anchorMax = anchor;
        toggleRect.pivot = anchor;
        toggleRect.anchoredPosition = panelOpen ? new Vector2(-35f, -35f) : Vector2.zero;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (menuToggleButton != null) menuToggleButton.interactable = interactable;
        if (readyToPlayButton != null) readyToPlayButton.interactable = interactable;
        if (howToMoveButton != null) howToMoveButton.interactable = interactable;
        if (closeHowToMoveButton != null) closeHowToMoveButton.interactable = interactable;
    }
}
