using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A small UI polygon. The menu artwork stays sharp at any Canvas scale.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CroustyVectorGraphic : MaskableGraphic
{
    public Vector2[] points;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Length < 3) return;

        Rect r = rectTransform.rect;
        for (int i = 0; i < points.Length; i++)
        {
            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = new Vector3(r.xMin + points[i].x * r.width,
                r.yMin + points[i].y * r.height);
            vh.AddVert(v);
        }

        // Ear clipping also handles the torn, concave ends of the ribbons.
        List<int> remaining = new List<int>(points.Length);
        for (int i = 0; i < points.Length; i++) remaining.Add(i);
        bool ccw = SignedArea() > 0f;
        int guard = points.Length * points.Length;
        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                int b = remaining[i];
                int c = remaining[(i + 1) % remaining.Count];
                float cross = Cross(points[a], points[b], points[c]);
                if (ccw ? cross <= 0.000001f : cross >= -0.000001f) continue;
                bool contains = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    int p = remaining[j];
                    if (p == a || p == b || p == c) continue;
                    if (InTriangle(points[p], points[a], points[b], points[c]))
                    {
                        contains = true;
                        break;
                    }
                }
                if (contains) continue;
                // Unity UI's front face uses clockwise vertex order.
                if (ccw) vh.AddTriangle(c, b, a);
                else vh.AddTriangle(a, b, c);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        if (remaining.Count == 3)
        {
            if (ccw) vh.AddTriangle(remaining[2], remaining[1], remaining[0]);
            else vh.AddTriangle(remaining[0], remaining[1], remaining[2]);
        }
    }

    private float SignedArea()
    {
        float area = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            area += a.x * b.y - b.x * a.y;
        }
        return area;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
        (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        bool n1 = Cross(a, b, p) < 0f;
        bool n2 = Cross(b, c, p) < 0f;
        bool n3 = Cross(c, a, p) < 0f;
        return n1 == n2 && n2 == n3;
    }
}
