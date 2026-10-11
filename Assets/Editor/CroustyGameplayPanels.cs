using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CroustyGameplayPanels
{
    [MenuItem("Tools/Crousty/Convertir les HUD en panels")]
    public static void ConvertAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Prefab UI stays attached to its bus, including buses spawned in another scene.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Bus" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponentInChildren<BusController>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PrepareRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] { "Assets/SceneBase.unity", "Assets/Jojo/SceneJojo.unity",
            "Assets/Scenes/Scenes Antoine.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects()) PrepareRoot(root);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var session in root.GetComponentsInChildren<SceneBaseSession>(true))
                { session.PrepareUI(); Record(session); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Crousty] HUD enregistrés : mini-carte, livraisons, passagers, PET et effet de chute.");
    }

    private static void PrepareRoot(GameObject root)
    {
        foreach (var bus in root.GetComponentsInChildren<BusController>(true))
        {
            var pet = bus.GetComponent<BusPetController>();
            if (pet == null) pet = bus.gameObject.AddComponent<BusPetController>();
            pet.PrepareUI();
            Record(pet);
        }
        foreach (var flight in root.GetComponentsInChildren<BusFlightController>(true))
        {
            var effect = flight.GetComponent<BusFallSpeedEffect>();
            if (effect == null) effect = flight.gameObject.AddComponent<BusFallSpeedEffect>();
            effect.PrepareUI();
            Record(effect);
        }
        foreach (var hud in root.GetComponentsInChildren<CroustyHUD>(true))
        { hud.PrepareUI(); Record(hud); }
        foreach (var map in root.GetComponentsInChildren<BusMinimap>(true))
        { map.PrepareUI(); Record(map); }
    }

    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
