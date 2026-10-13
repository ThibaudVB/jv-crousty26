using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Adds controller affordances without changing the authored portrait or HUD anchors.
public sealed class CroustyHudControls : MonoBehaviour
{
    public RectTransform objective;
    public RectTransform managerPanel;
    public CroustyBonusHUD bonuses;
    public CanvasGroup front, managerGroup;
    public Image rbPrompt, lbPrompt, xPrompt;
    public TMP_Text rbKey, lbKey, xKey;
    public GerantDialogue dialogue;
    [Min(120f)] public float objectiveVisibleWidth = 180f;
    private Vector2 objectivePosition;
    private float slide, slideVelocity, dialogueVelocity;
    private bool hidden, dialogueHidden;

    private void Start()
    {
        if (objective == null || managerPanel == null || bonuses == null || front == null ||
            managerGroup == null || rbPrompt == null || lbPrompt == null ||
            xPrompt == null || rbKey == null || lbKey == null || xKey == null)
        {
            Debug.LogError("HUD : assigner les panels et indications dans la scene.", this);
            enabled = false;
            return;
        }
        objectivePosition = objective.anchoredPosition;
        if (dialogue != null) dialogue.MessageChanged += ShowDialogue;
        RenderSlide();
    }

    private void OnDestroy()
    {
        if (dialogue != null) dialogue.MessageChanged -= ShowDialogue;
    }

    private void ShowDialogue() => dialogueHidden = false;

    private void Update()
    {
        if (front == null || Time.timeScale <= 0f || CroustyPauseMenu.ToggledThisFrame) return;
        var pad = Gamepad.current;
        var keyboard = Keyboard.current;
        if (pad != null && pad.rightShoulder.wasPressedThisFrame || keyboard != null && keyboard.oKey.wasPressedThisFrame)
            hidden = !hidden;
        if (pad != null && pad.leftShoulder.wasPressedThisFrame || keyboard != null && keyboard.lKey.wasPressedThisFrame)
            dialogueHidden = true;
        // Preserve velocity when RB is pressed again during the slide.
        CroustyPanelMotion.Spring(ref slide, ref slideVelocity, hidden ? 1f : 0f, Time.unscaledDeltaTime);
        RenderSlide();
        managerGroup.alpha = Mathf.SmoothDamp(managerGroup.alpha, dialogueHidden ? 0f : 1f,
            ref dialogueVelocity, .16f, Mathf.Infinity, Time.unscaledDeltaTime);
        managerGroup.interactable = managerGroup.blocksRaycasts = !dialogueHidden;
        bool controller = pad != null;
        // TASK already includes RB. The authored overlay replaces it with O on keyboard.
        rbPrompt.enabled = !controller;
        lbPrompt.enabled = controller && !dialogueHidden;
        rbKey.gameObject.SetActive(!controller); lbKey.gameObject.SetActive(!controller && !dialogueHidden);
        if (controller && pad.buttonWest.wasPressedThisFrame) bonuses.ActivateSelected();
        if (controller && pad.dpad.right.wasPressedThisFrame) bonuses.CycleBonus(1);
        if (controller && pad.dpad.left.wasPressedThisFrame) bonuses.CycleBonus(-1);
    }

    private void LateUpdate()
    {
        if (bonuses == null || xPrompt == null || xKey == null) return;
        bool available = bonuses.HasSelectedBonus;
        bool controller = Gamepad.current != null;
        xPrompt.enabled = controller && available;
        xKey.gameObject.SetActive(!controller && available);
        xKey.text = bonuses.SelectedKeyLabel;
    }

    private void RenderSlide()
    {
        // Keep the left strip (including the printed RB) on screen while retracted.
        float distance = Mathf.Max(0f, objective.rect.width - objectiveVisibleWidth - objectivePosition.x);
        objective.anchoredPosition = objectivePosition + Vector2.right * (distance * slide);
        front.alpha = 1f;
        front.interactable = front.blocksRaycasts = !hidden;
    }

}
