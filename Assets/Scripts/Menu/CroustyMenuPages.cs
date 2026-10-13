using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CroustyMenuPages : MonoBehaviour
{
    public RectTransform viewport;
    public RectTransform firstPage;
    public RectTransform secondPage;
    public CanvasGroup firstGroup;
    public CanvasGroup secondGroup;
    public RectTransform navigationTab;
    public RectTransform navigationViewport;
    public Image ticketImage;
    public RectTransform navigationPrompt;
    public Button[] buttons;
    [Min(0.05f)] public float transitionDuration = 0.55f;
    public float tabHeight = 88f;
    public bool IsExpanded { get; private set; }
    public bool IsAnimating => Mathf.Abs(progress - (IsExpanded ? 1f : 0f)) > .0001f || Mathf.Abs(velocity) > .001f;

    private float progress, velocity;

    private void Awake() => Apply();
    private void OnEnable() => Apply();
    private void OnRectTransformDimensionsChange() => Apply();

    public void Toggle() => SetExpanded(!IsExpanded);

    public void SetExpanded(bool expanded)
    {
        if (IsExpanded == expanded) return;
        IsExpanded = expanded;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
        Apply();
    }

    private void Update()
    {
        if (IsAnimating)
        {
            CroustyPanelMotion.Spring(ref progress, ref velocity, IsExpanded ? 1f : 0f,
                Time.unscaledDeltaTime * .55f / Mathf.Max(.05f, transitionDuration));
        }
        Apply();
        if (IsAnimating || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        Button chosen = null;
        foreach (var button in buttons)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) continue;
            var hover = button.GetComponent<CroustyMenuHover>();
            if (hover == null || !hover.IsHovered) continue;
            chosen = button;
            break;
        }
        if (chosen == null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
            if (selected != null && selected.isActiveAndEnabled && selected.IsInteractable() &&
                System.Array.IndexOf(buttons, selected) >= 0) chosen = selected;
        }
        if (chosen != null) chosen.onClick.Invoke();
    }

    private void Apply()
    {
        if (viewport == null) return;
        float height = viewport.rect.height;
        float screenHeight = navigationViewport != null ? navigationViewport.rect.height : height;
        float ticketHeight = ticketImage != null ? ticketImage.rectTransform.rect.height : tabHeight;
        float scrollDistance = ticketImage != null ? screenHeight + ticketHeight - 2f * tabHeight : height;
        // Both menus travel the same distance as the ticket, preserving the gaps on either side.
        firstPage.anchoredPosition = new Vector2(0f, progress * scrollDistance);
        secondPage.anchoredPosition = new Vector2(0f, (progress - 1f) * scrollDistance);
        firstPage.gameObject.SetActive(IsAnimating || progress < 1f);
        secondPage.gameObject.SetActive(IsAnimating || progress > 0f);
        firstGroup.interactable = firstGroup.blocksRaycasts = !IsAnimating && !IsExpanded;
        secondGroup.interactable = secondGroup.blocksRaycasts = !IsAnimating && IsExpanded;
        if (ticketImage == null) return; // An old, loaded scene is upgraded by the editor migration.
        // One long image passes through the viewport. Its printed ends are revealed by clipping.
        float closedPosition = -screenHeight + tabHeight;
        float openPosition = ticketHeight - tabHeight;
        navigationTab.anchoredPosition = new Vector2(60f, Mathf.Lerp(closedPosition, openPosition, progress));
        if (navigationPrompt != null)
            navigationPrompt.anchoredPosition = new Vector2(216f,
                -16f - Mathf.Lerp(0f, ticketHeight - tabHeight, progress));
    }
}
