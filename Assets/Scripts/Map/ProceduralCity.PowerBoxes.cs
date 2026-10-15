using UnityEngine;

public sealed partial class ProceduralCity
{
    private void BuildPowerBoxRows(Chunk chunk, Vector2Int coordinate)
    {
        for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++)
        {
            var from = coordinate * 2 + new Vector2Int(x, z);
            for (int axis = 0; axis < 2; axis++)
            {
                var heading = axis == 0 ? Vector2Int.right : Vector2Int.up;
                int along = axis == 0 ? from.x : from.y;
                int street = axis == 0 ? from.y : from.x;
                if (FloorMod(along + street, 10) != 1 || !IsRoadEdgeOpen(from, from + heading)) continue;
                Vector3 center = JunctionPosition(from) + Flat(heading) * (BlockSize * .5f) + Vector3.up * 1.4f;
                CreateRoadPowerBox(chunk.root.transform, center);
            }
        }
    }

    private void CreateRoadPowerBox(Transform parent, Vector3 position)
    {
        if (roadPowerBoxPrefab == null) return;
        var root = new GameObject("Box bonus");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        // Imported ring parents may be scaled; pickups keep the same world size.
        Vector3 scale = parent.lossyScale;
        root.transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        var visual = Instantiate(roadPowerBoxPrefab, root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        foreach (var oldBox in visual.GetComponentsInChildren<PowerUpBox>(true)) oldBox.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float fit = 1.8f / Mathf.Max(.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
            visual.transform.localScale *= fit;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += position - bounds.center;
        }
        var trigger = root.AddComponent<BoxCollider>();
        trigger.size = new Vector3(2.6f, 2.8f, 2.6f);
        trigger.isTrigger = true;
        var pickup = root.AddComponent<PowerUpBox>();
        pickup.visual = visual;
        pickup.respawnDelay = 18f;
    }
}
