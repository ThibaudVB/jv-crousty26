using UnityEditor;
using UnityEngine;

public sealed class CroustyPoliceBarrierImport : AssetPostprocessor
{
    private void OnPostprocessMaterial(Material material)
    {
        if (assetPath != "Assets/Resources/Crousty/PoliceAir/Barrier.fbx") return;
        Color color = material.color;
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
    }
}
