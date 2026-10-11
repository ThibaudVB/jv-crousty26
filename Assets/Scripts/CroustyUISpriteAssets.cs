#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only migration helper. The generated sprites become normal project assets.
public static class CroustyUISpriteAssets
{
    public static Sprite Save(Sprite sprite, string name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
        string path = "Assets/UI/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var texture = Object.Instantiate(sprite.texture);
        texture.name = name + " Texture";
        var saved = Sprite.Create(texture, sprite.rect, sprite.pivot / sprite.rect.size,
            sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        saved.name = name;
        AssetDatabase.CreateAsset(saved, path);
        AssetDatabase.AddObjectToAsset(texture, saved);
        AssetDatabase.SaveAssets();
        return saved;
    }
}
#endif
