using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scene references keep the corrected font and its atlas available in player builds.
[DefaultExecutionOrder(-1000)]
public sealed class CroustyTypography : MonoBehaviour
{
    public TMP_FontAsset fontAsset;
    public Font legacyFont;
    public static TMP_FontAsset FontAsset { get; private set; }
    public static Font LegacyFont { get; private set; }

    private void Awake()
    {
        FontAsset = fontAsset;
        LegacyFont = legacyFont;
        ApplyScene();
    }

    private IEnumerator Start()
    {
        // Include HUD elements created by other components during Start.
        yield return null;
        ApplyScene();
    }

    private void ApplyScene()
    {
        foreach (var label in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            if (label.gameObject.scene == gameObject.scene) Apply(label);
        if (legacyFont == null) return;
        foreach (var label in FindObjectsByType<Text>(FindObjectsInactive.Include))
            if (label.gameObject.scene == gameObject.scene) label.font = legacyFont;
    }

    public static void Apply(TMP_Text label)
    {
        if (FontAsset == null || label == null) return;
        label.font = FontAsset;
        label.fontSharedMaterial = FontAsset.material;
        label.SetAllDirty();
    }

    private void OnDestroy()
    {
        if (FontAsset == fontAsset) { FontAsset = null; LegacyFont = null; }
    }
}
