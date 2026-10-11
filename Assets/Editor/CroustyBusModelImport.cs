using UnityEditor;
using UnityEngine;

// This FBX contains thin/reversed surfaces which are visible from both sides in Blender.
// Keep the correction on the imported asset so prefab and scene references stay intact.
public sealed class CroustyBusModelImport : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    private void OnPostprocessModel(GameObject model)
    {
        if (assetPath != "Assets/NouveauCroustyBus.fbx") return;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || !material.HasProperty("_Cull")) continue;
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.doubleSidedGI = true;
        }
    }
}
