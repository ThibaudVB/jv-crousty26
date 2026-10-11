using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ruban pose dans le monde puis projete : meme inclinaison et meme relief que la carte.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MinimapRouteGraphic : MaskableGraphic
{
    private Camera mapCamera;
    private float mapHeight;
    private IReadOnlyList<Vector3> points;
    private readonly UIVertex[] quad = new UIVertex[4];

    public void UpdateRoute(Camera camera, IReadOnlyList<Vector3> route, float groundHeight)
    {
        mapCamera = camera;
        mapHeight = groundHeight;
        points = route;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (mapCamera == null || points == null || points.Count < 2) return;
        DrawRibbon(vh, 2.6f, new Color(0.02f, 0.15f, 0.2f, 0.95f));
        DrawRibbon(vh, 1.65f, new Color(0.05f, 0.85f, 1f, 1f));
    }

    private void DrawRibbon(VertexHelper vh, float halfWidth, Color tint)
    {
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 a = points[i - 1] + Vector3.up * 0.2f;
            Vector3 b = points[i] + Vector3.up * 0.2f;
            if ((b - a).sqrMagnitude < 0.001f) continue;
            Vector3 left = SideAt(i - 1) * halfWidth;
            Vector3 right = SideAt(i) * halfWidth;
            SetVertex(0, a - left, tint);
            SetVertex(1, a + left, tint);
            SetVertex(2, b + right, tint);
            SetVertex(3, b - right, tint);
            vh.AddUIVertexQuad(quad);
        }
    }

    private Vector3 SideAt(int index)
    {
        Vector3 before = points[index] - points[Mathf.Max(0, index - 1)];
        Vector3 after = points[Mathf.Min(points.Count - 1, index + 1)] - points[index];
        Vector3 n1 = Vector3.Cross(Vector3.up, before).normalized;
        Vector3 n2 = Vector3.Cross(Vector3.up, after).normalized;
        if (n1.sqrMagnitude < 0.001f) return n2;
        if (n2.sqrMagnitude < 0.001f) return n1;
        Vector3 joint = (n1 + n2).normalized;
        if (joint.sqrMagnitude < 0.001f) return n2;
        return joint / Mathf.Max(0.5f, Vector3.Dot(joint, n2));
    }

    private void SetVertex(int index, Vector3 position, Color tint)
    {
        position.y = mapHeight;
        Vector3 viewport = mapCamera.WorldToViewportPoint(position);
        Rect rect = rectTransform.rect;
        quad[index] = UIVertex.simpleVert;
        quad[index].position = new Vector3(rect.xMin + viewport.x * rect.width,
            rect.yMin + viewport.y * rect.height, 0);
        quad[index].color = tint;
    }
}
