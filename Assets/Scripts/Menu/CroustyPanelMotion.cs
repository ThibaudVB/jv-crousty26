using UnityEngine;

[DisallowMultipleComponent]
public sealed class CroustyPanelMotion : MonoBehaviour
{
    // Exact damped spring: same small bounce at any frame rate, continuous on reversal.
    public static void Spring(ref float value, ref float velocity, float target, float dt)
    {
        const float damping = 13f, frequency = 10f;
        float offset = value - target;
        float b = (velocity + damping * offset) / frequency;
        float decay = Mathf.Exp(-damping * dt);
        float cosine = Mathf.Cos(frequency * dt), sine = Mathf.Sin(frequency * dt);
        float wave = offset * cosine + b * sine;
        value = target + decay * wave;
        velocity = decay * ((b * frequency * cosine - offset * frequency * sine) - damping * wave);
        if (Mathf.Abs(value - target) < .0001f && Mathf.Abs(velocity) < .001f)
        { value = target; velocity = 0f; }
    }
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 position;
    private Vector3 scale;
    private Quaternion rotation;
    private bool captured, closing;
    private float progress = 1;
    private Vector2 fromPosition;
    private Vector3 fromScale;
    private Quaternion fromRotation;
    private float fromAlpha;
    private void Capture()
    {
        if (captured) return;
        rect = transform as RectTransform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (rect != null) { position = rect.anchoredPosition; scale = rect.localScale; rotation = rect.localRotation; }
        captured = true;
    }
    public static void Prepare(GameObject panel) { if (Application.isPlaying && panel != null && panel.GetComponent<CroustyPanelMotion>() == null) panel.AddComponent<CroustyPanelMotion>(); }
    public static void Show(GameObject panel)
    {
        if (panel == null) return;
        if (!Application.isPlaying) { panel.SetActive(true); return; }
        Prepare(panel);
        panel.SetActive(true);
        panel.GetComponent<CroustyPanelMotion>().Begin(false);
    }
    public static void Hide(GameObject panel)
    {
        if (panel == null || !panel.activeSelf) return;
        if (!Application.isPlaying) { panel.SetActive(false); return; }
        Prepare(panel);
        panel.GetComponent<CroustyPanelMotion>().Begin(true);
    }
    private void OnEnable()
    {
        Capture();
        if (rect != null)
        {
            rect.localScale = scale * .91f;
            rect.anchoredPosition = position + new Vector2(0,24);
            rect.localRotation = rotation * Quaternion.Euler(0,0,-2);
        }
        group.alpha = 0;
        Begin(false);
    }
    private void Begin(bool close)
    {
        Capture();
        fromAlpha = group.alpha;
        if (rect != null) { fromPosition = rect.anchoredPosition; fromScale = rect.localScale; fromRotation = rect.localRotation; }
        closing = close;
        progress = 0;
        group.interactable = group.blocksRaycasts = false;
        Apply();
    }
    private void Update()
    {
        if (progress >= 1) return;
        progress = Mathf.Min(1,progress + Time.unscaledDeltaTime / (closing ? .24f : .48f));
        Apply();
        if (progress >= 1)
        {
            if (closing) gameObject.SetActive(false);
            else group.interactable = group.blocksRaycasts = true;
        }
    }
    private void Apply()
    {
        float t = progress;
        float smooth = t * t * t * (t * (6f * t - 15f) + 10f);
        float back = smooth + .12f * Mathf.Sin(Mathf.PI * t) * Mathf.Sin(Mathf.PI * t);
        group.alpha = Mathf.Lerp(fromAlpha,closing ? 0 : 1,Mathf.SmoothStep(0,1,closing ? t : Mathf.Clamp01(t * 2)));
        if (rect == null) return;
        float blend = closing ? smooth : back;
        rect.localScale = Vector3.LerpUnclamped(fromScale,closing ? scale * .94f : scale,blend);
        rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition,position + (closing ? new Vector2(0,-15) : Vector2.zero),blend);
        rect.localRotation = Quaternion.SlerpUnclamped(fromRotation,rotation * Quaternion.Euler(0,0,closing ? 1.5f : 0),blend);
    }
    private void OnDisable()
    {
        if (!captured) return;
        if (rect != null) { rect.anchoredPosition = position; rect.localScale = scale; rect.localRotation = rotation; }
        group.alpha = 1;
        group.interactable = group.blocksRaycasts = true;
        progress = 1;
    }
}
