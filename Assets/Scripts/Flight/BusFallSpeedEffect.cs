using UnityEngine;
using UnityEngine.UI;

// Visual feedback only: reads vertical velocity, never changes the bus physics.
[DisallowMultipleComponent]
[RequireComponent(typeof(BusFlightController), typeof(Rigidbody))]
public sealed class BusFallSpeedEffect : MonoBehaviour
{
    [Min(0f)] public float startSpeed = 4f;
    [Min(0.1f)] public float fullSpeed = 30f;
    [Range(0f, 1f)] public float maximumOpacity = 0.45f;
    public float Intensity { get; private set; }

    private BusFlightController flight;
    private Rigidbody body;
    private Canvas canvas;
    private RectTransform viewport;
    private CanvasGroup group;
    private RectTransform[] streaks;
    private float[] phases;
    private Texture2D texture;
    private Sprite sprite;

    private void Awake()
    {
        flight = GetComponent<BusFlightController>();
        body = GetComponent<Rigidbody>();
    }

    private void CreateOverlay()
    {
        var root = new GameObject("Bus Fighter - vitesse de chute", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        viewport = root.GetComponent<RectTransform>();
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        texture = new Texture2D(8, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 8; x++)
        {
            float alpha = Mathf.Sin(Mathf.PI * y / 63f) * Mathf.Sin(Mathf.PI * x / 7f);
            texture.SetPixel(x, y, new Color(0.8f, 0.93f, 1f, alpha * alpha));
        }
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, 8, 64), Vector2.one * 0.5f);
        streaks = new RectTransform[16];
        phases = new float[streaks.Length];
        for (int i = 0; i < streaks.Length; i++)
        {
            var line = new GameObject("Trainee verticale", typeof(RectTransform), typeof(Image));
            var rect = line.GetComponent<RectTransform>();
            rect.SetParent(viewport, false);
            float x = 0.04f + (i % 8) * 0.027f;
            if (i >= 8) x = 1f - x;
            rect.anchorMin = rect.anchorMax = new Vector2(x, 0f);
            var graphic = line.GetComponent<Image>();
            graphic.sprite = sprite;
            graphic.raycastTarget = false;
            streaks[i] = rect;
            phases[i] = Mathf.Repeat(i * 0.618034f, 1f);
        }
    }

    private void Update() => UpdateEffect(Time.deltaTime);

    private void UpdateEffect(float deltaTime)
    {
        if (!flight.isActiveAndEnabled || !flight.IsFalling)
        {
            Intensity = 0f;
            if (canvas != null) canvas.enabled = false;
            return;
        }
        if (Time.timeScale <= 0f) return;
        float downwardSpeed = Mathf.Max(0f, -body.linearVelocity.y);
        float target = Mathf.InverseLerp(startSpeed, Mathf.Max(startSpeed + 0.1f, fullSpeed), downwardSpeed);
        Intensity = Mathf.Lerp(Intensity, target, 1f - Mathf.Exp(-12f * deltaTime));
        if (Intensity < 0.001f)
        {
            if (canvas != null) canvas.enabled = false;
            return;
        }
        if (canvas == null) CreateOverlay();
        canvas.enabled = true;
        group.alpha = Intensity * maximumOpacity;
        float height = Mathf.Max(1f, viewport.rect.height);
        float length = Mathf.Lerp(45f, 220f, Intensity);
        float speed = Mathf.Lerp(400f, 1800f, Intensity);
        for (int i = 0; i < streaks.Length; i++)
        {
            // Upward streaks convey downward motion; keep the center clear for the bus.
            phases[i] = Mathf.Repeat(phases[i] + speed * deltaTime / (height + length), 1f);
            streaks[i].anchoredPosition = new Vector2(0f, phases[i] * (height + length) - length * 0.5f);
            streaks[i].sizeDelta = new Vector2(i % 3 == 0 ? 4f : 2.5f, length * (0.7f + (i % 3) * 0.15f));
        }
    }

    private void OnDisable()
    {
        Intensity = 0f;
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
