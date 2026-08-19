using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TutorialCanvasUI : MonoBehaviour
{
    private enum LayoutState
    {
        Closed,
        Menu,
        HowToMove
    }

    [Header("Layouts")]
    [SerializeField] private RectTransform interactionCanvas;
    [SerializeField] private RectTransform uiBackplate;
    [SerializeField] private MonoBehaviour interactionBoundsClipper;
    [SerializeField] private GameObject menuLayout;
    [SerializeField] private GameObject howToMoveLayout;

    [Header("Interaction sizes")]
    [SerializeField] private Vector2 closedSize = new(620f, 360f);
    [SerializeField] private Vector2 menuSize = new(1140f, 830f);
    [SerializeField] private Vector2 howToMoveSize = new(1503f, 1355f);

    [Header("Buttons")]
    [SerializeField] private Button menuButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button howToMoveButton;
    [SerializeField] private Button closeButton;

    [Header("Scene")]
    [SerializeField] private string startSceneName = "StartScene";

    private LayoutState currentState;
    private bool isReturningToStartScene;

    private void OnEnable()
    {
        isReturningToStartScene = false;
        SetButtonsInteractable(true);
        SetState(LayoutState.Closed);
    }

    public void ToggleMenu()
    {
        SetState(currentState == LayoutState.Menu ? LayoutState.Closed : LayoutState.Menu);
    }

    public void ExitTutorial()
    {
        if (isReturningToStartScene)
        {
            return;
        }

        isReturningToStartScene = true;
        SetButtonsInteractable(false);

        TutorialSceneController controller = FindFirstObjectByType<TutorialSceneController>();
        if (controller != null)
        {
            controller.ReturnToStartScene();
            return;
        }

        Debug.LogWarning(
            $"[{nameof(TutorialCanvasUI)}] TutorialSceneController was not found. Loading {startSceneName} directly.",
            this);
        SceneManager.LoadScene(startSceneName, LoadSceneMode.Single);
    }

    public void ShowHowToMove()
    {
        SetState(LayoutState.HowToMove);
    }

    public void CloseHowToMove()
    {
        SetState(LayoutState.Menu);
    }

    private void SetState(LayoutState state)
    {
        currentState = state;

        if (menuLayout != null)
        {
            menuLayout.SetActive(state == LayoutState.Menu);
        }

        if (howToMoveLayout != null)
        {
            howToMoveLayout.SetActive(state == LayoutState.HowToMove);
        }

        Vector2 size = state switch
        {
            LayoutState.Menu => menuSize,
            LayoutState.HowToMove => howToMoveSize,
            _ => closedSize
        };

        ResizeInteractionArea(size);
    }

    private void ResizeInteractionArea(Vector2 size)
    {
        if (interactionCanvas != null)
        {
            interactionCanvas.sizeDelta = size;
        }

        if (uiBackplate != null)
        {
            uiBackplate.anchorMin = Vector2.zero;
            uiBackplate.anchorMax = Vector2.one;
            uiBackplate.anchoredPosition = Vector2.zero;
            uiBackplate.sizeDelta = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(uiBackplate);
        }

        if (interactionBoundsClipper != null)
        {
            FieldInfo sizeField = interactionBoundsClipper.GetType().GetField(
                "_size",
                BindingFlags.Instance | BindingFlags.NonPublic);
            sizeField?.SetValue(interactionBoundsClipper, new Vector3(size.x, size.y, 0.01f));
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (menuButton != null) menuButton.interactable = interactable;
        if (exitButton != null) exitButton.interactable = interactable;
        if (howToMoveButton != null) howToMoveButton.interactable = interactable;
        if (closeButton != null) closeButton.interactable = interactable;
    }
}
