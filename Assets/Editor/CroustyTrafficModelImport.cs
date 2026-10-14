using UnityEditor;
using UnityEngine;

// OBJ/MTL texture names containing spaces are not reliably resolved by the model
// importer. Bind this car's supplied atlas explicitly, including in player builds.
public sealed class CroustyTrafficModelImport : AssetPostprocessor
{
    public override uint GetVersion() => 3;
    void OnPostprocessModel(GameObject model)
    {
        if (assetPath != "Assets/Resources/Crousty/Vehicles/1377 Car.obj" &&
            assetPath != "Assets/Map/City Pack/Car/1377 Car.obj") return;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Crousty/Traffic1377.mat");
        if (material == null) { Debug.LogError("Missing authored material: Traffic1377.mat"); return; }
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }
}
