using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The floating repair diamond is baked into each garage mesh. Remove only the
// elevated Repair_Glow triangles; the floor marker, walls, roof and doors remain.
// ProceduralCity's MeshCollider uses this same corrected imported mesh.
public sealed class CroustyGarageModelImport : AssetPostprocessor
{
    public override uint GetVersion() => 1;
    void OnPostprocessModel(GameObject model)
    {
        if (assetPath != "Assets/Map/CroustyCity_PastelPalette_V3_Unity.fbx") return;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Garage_01" && filter.name != "Garage_02" && filter.name != "Garage_03") continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null) continue;
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var matrix = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var materials = renderer.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
            {
                if (materials[sub] == null || !materials[sub].name.EndsWith("Repair_Glow")) continue;
                var indices = mesh.GetTriangles(sub);
                var kept = new List<int>(indices.Length);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    bool floating = matrix.MultiplyPoint3x4(vertices[indices[i]]).y > 1f &&
                        matrix.MultiplyPoint3x4(vertices[indices[i + 1]]).y > 1f &&
                        matrix.MultiplyPoint3x4(vertices[indices[i + 2]]).y > 1f;
                    if (floating) continue;
                    kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]);
                }
                mesh.SetTriangles(kept, sub, false);
                Debug.Log("Garage repair marker removed: " + filter.name + " (" + (indices.Length - kept.Count) / 3 + " triangles). Building collisions preserved.");
            }
        }
    }
}
