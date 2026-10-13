using UnityEngine;
using UnityEngine.UI;

// Redraw only the speed badge from the original artwork above the map.
// The rest of the sprite (including its opaque centre) stays behind the map.
public sealed class MinimapSpeedBadge : Image
{
    private static readonly Vector2[] Outline = {
        new Vector2(1233, 895), new Vector2(1516, 1062),
        new Vector2(1455, 1350), new Vector2(1323, 1463),
        new Vector2(1102, 1453), new Vector2(922, 1208),
        new Vector2(1025, 989)
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (sprite == null) return;
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width / 1516f, rect.height / 1463f);
        Vector2 size = new Vector2(1516, 1463) * scale;
        Vector2 origin = rect.center - size * .5f;
        Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        for (int i = 0; i < Outline.Length; i++)
        {
            Vector2 p = new Vector2(Outline[i].x / 1516f, 1f - Outline[i].y / 1463f);
            vh.AddVert(origin + Vector2.Scale(p, size), color,
                new Vector2(Mathf.Lerp(uv.x, uv.z, p.x), Mathf.Lerp(uv.y, uv.w, p.y)));
        }
        for (int i = 1; i < Outline.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
    }

    public static void Ensure(RectTransform frame)
    {
        if (frame.Find("Badge vitesse premier plan") != null) return;
        var root = new GameObject("Badge vitesse premier plan", typeof(RectTransform));
        root.layer = frame.gameObject.layer;
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(frame, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var badge = root.AddComponent<MinimapSpeedBadge>();
        badge.sprite = frame.GetComponent<Image>().sprite;
        badge.raycastTarget = false;
        // Draw after the map and markers, but before the speed text.
        var speed = frame.Find("Vitesse");
        if (speed != null) rect.SetSiblingIndex(speed.GetSiblingIndex());
    }
}
