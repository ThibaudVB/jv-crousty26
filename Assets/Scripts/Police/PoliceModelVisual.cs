using UnityEngine;

// Fit imported assets without relying on their original units or pivot.
public static class PoliceModelVisual
{
    public static GameObject Create(GameObject prefab, Transform parent, Vector3 size, float yaw = 0f)
    {
        var visual = new GameObject(prefab.name + " - fitted visual");
        visual.transform.SetParent(parent, false);
        var model = Object.Instantiate(prefab, visual.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        Bounds bounds = LocalBounds(visual, parent);
        if (size.y > 0f && bounds.size.z > bounds.size.x)
        {
            model.transform.localRotation *= Quaternion.Euler(0f, 90f, 0f);
            bounds = LocalBounds(visual, parent);
        }
        visual.transform.localScale = size.y <= 0f
            ? Vector3.one * (size.x / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z)))
            : new Vector3(size.x / Mathf.Max(.01f, bounds.size.x), size.y / Mathf.Max(.01f, bounds.size.y), size.z / Mathf.Max(.01f, bounds.size.z));
        bounds = LocalBounds(visual, parent);
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return visual;
    }

    private static Bounds LocalBounds(GameObject visual, Transform parent)
    {
        var bounds = new Bounds(); bool first = true;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            Bounds local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                corner = parent.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                else bounds.Encapsulate(corner);
            }
        }
        return bounds;
    }
}
