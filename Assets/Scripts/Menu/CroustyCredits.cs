using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keeps the credits editable in the scene while providing a readable scroll area.
[RequireComponent(typeof(TMP_Text))]
public sealed class CroustyCredits : MonoBehaviour
{
    private ScrollRect scroll;
    private RectTransform viewport;
    private RectTransform content;
    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        content = label.rectTransform;
        var parent = content.parent;
        int sibling = content.GetSiblingIndex();
        var area = new GameObject("Credits scroll area", typeof(RectTransform), typeof(Image),
            typeof(RectMask2D), typeof(ScrollRect));
        viewport = area.GetComponent<RectTransform>();
        viewport.SetParent(parent, false);
        viewport.SetSiblingIndex(sibling);
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.anchoredPosition = content.anchoredPosition;
        viewport.sizeDelta = content.sizeDelta;
        area.GetComponent<Image>().color = new Color(1, 1, 1, .001f);
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(-12, 0);
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.fontSize = 18;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.overflowMode = TextOverflowModes.Overflow;
        scroll = area.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28;
        scroll.decelerationRate = .08f;
        Resize();
    }

    private void Resize()
    {
        if (content == null) return;
        float height = label.GetPreferredValues(label.text, viewport.rect.width - 12, Mathf.Infinity).y;
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewport.rect.height, height));
    }

    private void OnEnable()
    {
        if (scroll == null) return;
        Resize();
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
    }

    private void Update()
    {
        if (scroll == null || !viewport.gameObject.activeInHierarchy) return;
        float input = 0;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.downArrowKey.isPressed || keyboard.pageDownKey.isPressed) input -= 1;
            if (keyboard.upArrowKey.isPressed || keyboard.pageUpKey.isPressed) input += 1;
        }
        var pad = Gamepad.current;
        if (pad != null) input += pad.rightStick.ReadValue().y;
        float overflow = content.rect.height - viewport.rect.height;
        if (overflow > 0 && Mathf.Abs(input) > .1f)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition +
                input * 160 * Time.unscaledDeltaTime / overflow);
    }

    private void OnDestroy()
    {
        if (viewport != null) Destroy(viewport.gameObject);
    }
}
