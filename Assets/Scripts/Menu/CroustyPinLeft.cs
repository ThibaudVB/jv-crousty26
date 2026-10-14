using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calque qui recouvre un cadre de menu (848 x 480) et se décale pour que son bord gauche
/// touche celui de l'écran. Les bandeaux placés dedans restent collés à gauche, sans espace,
/// même quand le cadre est centré sur un écran plus large.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class CroustyPinLeft : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform frame;
    private RectTransform screen;
    private readonly Vector3[] corners = new Vector3[4];

    // Déplace les éléments (enfants directs d'un même cadre) dans un calque collé à gauche.
    public static void Pin(params Transform[] items)
    {
        var byFrame = new Dictionary<Transform, List<Transform>>();
        foreach (var item in items)
        {
            if (item == null || item.parent == null) continue;
            if (!byFrame.TryGetValue(item.parent, out var list)) byFrame[item.parent] = list = new List<Transform>();
            if (!list.Contains(item)) list.Add(item);
        }
        foreach (var pair in byFrame)
        {
            var frame = pair.Key as RectTransform;
            if (frame == null || frame.GetComponent<CroustyPinLeft>() != null) continue;
            var list = pair.Value;
            list.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

            var layer = new GameObject("Collé à gauche", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(frame, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.pivot = frame.pivot;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetSiblingIndex(list[0].GetSiblingIndex());
            // Le calque a la même taille que le cadre : les positions des éléments ne changent pas.
            foreach (var item in list) item.SetParent(layer, false);
            layer.gameObject.AddComponent<CroustyPinLeft>();
        }
    }

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        frame = rect.parent as RectTransform;
        var canvas = GetComponentInParent<Canvas>();
        screen = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        Apply();
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (frame == null || screen == null) return;
        screen.GetWorldCorners(corners);
        float screenLeft = corners[0].x;
        frame.GetWorldCorners(corners);
        float frameLeft = corners[0].x;
        float scale = Mathf.Max(0.0001f, frame.lossyScale.x);
        rect.anchoredPosition = new Vector2((screenLeft - frameLeft) / scale, 0f);
    }
}
