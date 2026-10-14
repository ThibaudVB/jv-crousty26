using TMPro;
using UnityEngine;

// Texte qui flotte au-dessus du bus pendant le boost de points des figures (BusAirTricks) :
//   x2 BOOST
//   7s
// Toujours tourné vers la caméra. Ce n'est pas dans le HUD : il suit le bus dans le monde.
//
// À ajouter à la main sur le bus (Add Component > BusBoostDisplay). Les réglages sont dans l'Inspector.
public class BusBoostDisplay : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Hauteur du texte au-dessus du bus (m)")]
    public float heightAboveBus = 5f;

    [Header("Texte")]
    [Tooltip("Taille du texte dans le monde")]
    [Min(0.1f)] public float textSize = 12f;
    public Color boostColor = new Color(1f, 0.82f, 0.13f);
    public Color outlineColor = new Color(0.45f, 0.15f, 0f);
    [Tooltip("Texte après le multiplicateur")]
    public string boostLabel = "BOOST";
    [Tooltip("Affiche les secondes restantes sous le texte")]
    public bool showTimeLeft = true;

    [Header("Animation")]
    [Tooltip("Grossissement quand un boost démarre (1 = aucun)")]
    [Min(1f)] public float popScale = 1.6f;
    [Tooltip("Durée du grossissement (s)")]
    [Min(0.01f)] public float popDuration = 0.35f;
    [Tooltip("Le texte clignote pendant ces dernières secondes")]
    [Min(0f)] public float blinkSecondsLeft = 3f;

    private TextMeshPro label;
    private DeliveryManager manager;
    private float popTime = -999f;

    private void Start()
    {
        var textObject = new GameObject("Texte du boost");
        textObject.transform.SetParent(transform, false);
        label = textObject.AddComponent<TextMeshPro>();
        CroustyTypography.Apply(label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.outlineWidth = 0.25f;
        label.outlineColor = outlineColor;
        label.rectTransform.sizeDelta = new Vector2(20f, 6f);
        textObject.SetActive(false);
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        if (manager != null) manager.ScoreBoostStarted -= OnBoostStarted;
        manager = null;
        if (label != null) label.gameObject.SetActive(false);
    }

    private void Subscribe()
    {
        if (manager != null || DeliveryManager.Instance == null) return;
        manager = DeliveryManager.Instance;
        manager.ScoreBoostStarted += OnBoostStarted;
    }

    private void OnBoostStarted(float multiplier, float duration) => popTime = Time.time;

    private void LateUpdate()
    {
        if (label == null) return;
        Subscribe(); // le DeliveryManager peut apparaître après le bus

        float remaining = manager != null ? manager.BoostRemaining : 0f;
        float multiplier = manager != null ? manager.ScoreMultiplier : 1f;
        bool active = remaining > 0f && multiplier > 1f;
        label.gameObject.SetActive(active);
        if (!active) return;

        // Au-dessus du bus, sans suivre ses rotations, et face à la caméra
        Transform text = label.transform;
        text.position = transform.position + Vector3.up * heightAboveBus;
        Camera cam = Camera.main;
        if (cam != null)
            text.rotation = Quaternion.LookRotation(text.position - cam.transform.position, cam.transform.up);

        string value = multiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        label.text = showTimeLeft
            ? $"x{value} {boostLabel}\n<size=60%>{Mathf.CeilToInt(remaining)}s</size>"
            : $"x{value} {boostLabel}";

        // Grossit quand un boost démarre, puis revient à sa taille
        float pop = Mathf.Clamp01((Time.time - popTime) / popDuration);
        float scale = Mathf.Lerp(popScale, 1f, 1f - (1f - pop) * (1f - pop));
        label.fontSize = textSize * scale;

        // Clignote à la fin du boost
        Color color = boostColor;
        if (remaining < blinkSecondsLeft)
            color.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
        label.color = color;
    }
}
