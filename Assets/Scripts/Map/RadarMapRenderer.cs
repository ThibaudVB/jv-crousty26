using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Dessine uniquement les silhouettes cartographiques, sans modifier les materiaux du monde.
public sealed class RadarMapRenderer : System.IDisposable
{
    private struct Entry { public MeshFilter filter; public int category; }
    private readonly List<Entry> entries = new List<Entry>();
    private readonly Material material;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private readonly CommandBuffer commands = new CommandBuffer { name = "Radar cartographique" };
    private Transform root;
    public float GroundHeight { get; private set; }
    public int GeometryCount => entries.Count;
    private static readonly Color[] Colors = {
        new Color(0.62f, 0.66f, 0.69f), // rues
        new Color(0.16f, 0.19f, 0.22f), // ilots
        new Color(0.2f, 0.29f, 0.26f),  // parcs
        new Color(0.23f, 0.27f, 0.3f)   // empreintes des batiments
    };

    public RadarMapRenderer(Shader shader) { material = new Material(shader); }

    public void RefreshGeometry(Transform mapRoot)
    {
        root = mapRoot;
        entries.Clear();
        GroundHeight = root != null ? root.position.y : 0f;
        if (root == null) return;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            string name = filter.name.ToLowerInvariant();
            int category = -1;
            if (name.Contains("asphalt") || name.Contains("road") && !name.Contains("marking") && !name.Contains("rooftop")) category = 0;
            if (name.Contains("sidewalk") || name.Contains("courtyard")) category = 1;
            if (name.Contains("park") && (name.Contains("ground") || name.Contains("sidewalk"))) category = 2;
            if (name.StartsWith("building") || name.EndsWith("_shop") || name.StartsWith("garage")) category = 3;
            // Un generateur peut classer explicitement ses meshes sans convention de nommage.
            var element = filter.GetComponent<RadarMapElement>();
            if (element != null) category = (int)element.kind;
            if (category >= 0) entries.Add(new Entry { filter = filter, category = category });
        }
        entries.Sort((a,b) => a.category.CompareTo(b.category));
    }

    public void Render(Camera camera, RenderTexture texture)
    {
        commands.Clear();
        commands.SetRenderTarget(texture);
        commands.ClearRenderTarget(true, true, new Color(0.075f, 0.1f, 0.12f));
        // Rendu direct lu par RawImage : conserver l'axe Y des coordonnees viewport.
        // Aucun blit de camera URP ne vient corriger un retournement de RenderTexture.
        commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
            GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
        foreach (var entry in entries)
        {
            if (entry.filter == null || !entry.filter.gameObject.activeInHierarchy) continue;
            var mesh = entry.filter.sharedMesh;
            if (mesh == null) continue;
            properties.SetColor("_Color", Colors[entry.category]);
            properties.SetFloat("_MapHeight", GroundHeight);
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                commands.DrawMesh(mesh, entry.filter.transform.localToWorldMatrix, material, sub, 0, properties);
        }
        if (texture.useMipMap) commands.GenerateMips(texture);
        Graphics.ExecuteCommandBuffer(commands);
    }

    public void Dispose()
    {
        commands.Release();
        Object.Destroy(material);
    }
}
