using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CroustyMenuHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public RectTransform target;
    public bool highlighted;
    public Image colorTarget;
    public TMP_Text textTarget;
    public Color activeImageColor = Color.white;
    public Color activeTextColor = Color.white;
    private bool hovered;
    private bool selected;
    private Vector2 basePosition;
    private Color baseImageColor;
    private Color baseTextColor;
    private bool styleCaptured;

    private void Awake()
    {
        if (target == null) target = transform as RectTransform;
        basePosition = target.anchoredPosition;
    }

    private void Update()
    {
        if (!styleCaptured)
        {
            if (colorTarget != null) baseImageColor = colorTarget.color;
            if (textTarget != null) baseTextColor = textTarget.color;
            styleCaptured = true;
        }
        float active = hovered || selected || highlighted ? 1f : 0f;
        float t = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
        target.localScale = Vector3.Lerp(target.localScale, Vector3.one * (1f + active * 0.035f), t);
        target.anchoredPosition = Vector2.Lerp(target.anchoredPosition,
            basePosition + new Vector2(active * 5f, 0f), t);
        if (colorTarget != null)
            colorTarget.color = Color.Lerp(colorTarget.color,
                Color.Lerp(baseImageColor, activeImageColor, active), t);
        if (textTarget != null)
            textTarget.color = Color.Lerp(textTarget.color,
                Color.Lerp(baseTextColor, activeTextColor, active), t);
    }

    private void OnDisable()
    {
        hovered = selected = false;
        if (target != null)
        {
            target.localScale = Vector3.one;
            target.anchoredPosition = basePosition;
        }
        if (!styleCaptured) return;
        if (colorTarget != null) colorTarget.color = baseImageColor;
        if (textTarget != null) textTarget.color = baseTextColor;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;
}
