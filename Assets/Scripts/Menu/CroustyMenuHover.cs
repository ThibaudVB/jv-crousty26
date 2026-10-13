using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CroustyMenuHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform target;
    public bool highlighted;
    public Image colorTarget;
    public TMP_Text textTarget;
    public Color activeImageColor = Color.white;
    public Color activeTextColor = Color.white;
    public Image ribbonImage;
    public Sprite idleRibbon;
    public Sprite activeRibbon;
    public GameObject actionPrompt;
    public float hoverScale = 1.035f;
    public bool animateTransform = true;
    public bool IsActive => hovered || selected || highlighted;
    public bool IsHovered => hovered;
    private bool hovered;
    private bool selected;
    private Vector2 basePosition;
    private Color baseImageColor;
    private Color baseTextColor;
    private bool styleCaptured;
    private bool pressed;
    private float bounceAge = 10;
    private Vector3 baseScale, scaleVelocity;
    private Quaternion baseRotation;

    private void Awake()
    {
        if (target == null) target = transform as RectTransform;
        basePosition = target.anchoredPosition;
        baseScale = target.localScale;
        baseRotation = target.localRotation;
    }

    private void Update()
    {
        if (!styleCaptured)
        {
            if (colorTarget != null) baseImageColor = colorTarget.color;
            if (textTarget != null) baseTextColor = textTarget.color;
            styleCaptured = true;
        }
        float active = IsActive ? 1f : 0f;
        float t = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
        bounceAge += Time.unscaledDeltaTime;
        float bounce = Mathf.Sin(bounceAge * 28f) * Mathf.Exp(-bounceAge * 12f) * .055f;
        Vector3 desired = Vector3.Scale(baseScale, pressed ? new Vector3(.97f,.92f,1) : new Vector3(1f + active * (hoverScale - 1f) + bounce,1f + active * (hoverScale - 1f) + bounce,1));
        if (animateTransform)
        {
            target.localScale = Vector3.SmoothDamp(target.localScale,desired,ref scaleVelocity,.07f,Mathf.Infinity,Time.unscaledDeltaTime);
            target.localRotation = Quaternion.Slerp(target.localRotation,baseRotation * Quaternion.Euler(0,0,pressed ? -1.5f : bounce * 22),t);
            target.anchoredPosition = Vector2.Lerp(target.anchoredPosition,
                basePosition + new Vector2(active * 5f, 0f), t);
        }
        if (colorTarget != null)
            colorTarget.color = Color.Lerp(colorTarget.color,
                Color.Lerp(baseImageColor, activeImageColor, active), t);
        if (textTarget != null)
            textTarget.color = Color.Lerp(textTarget.color,
                Color.Lerp(baseTextColor, activeTextColor, active), t);
        if (ribbonImage != null && idleRibbon != null && activeRibbon != null)
            ribbonImage.sprite = IsActive ? activeRibbon : idleRibbon;
        if (actionPrompt != null && actionPrompt.activeSelf != IsActive) actionPrompt.SetActive(IsActive);
    }

    private void OnDisable()
    {
        hovered = selected = pressed = false;
        if (actionPrompt != null) actionPrompt.SetActive(false);
        if (ribbonImage != null && idleRibbon != null) ribbonImage.sprite = idleRibbon;
        scaleVelocity = Vector3.zero;
        bounceAge = 10;
        if (target != null)
        {
            target.localScale = baseScale;
            target.localRotation = baseRotation;
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
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; bounceAge = 0; }
    public void Bounce() { pressed = false; bounceAge = 0; }
}
