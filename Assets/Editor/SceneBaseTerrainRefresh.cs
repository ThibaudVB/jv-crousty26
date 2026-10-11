using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Update the loaded scene without reloading it or discarding unsaved edits.
[InitializeOnLoad]
public static class SceneBaseTerrainRefresh
{
    static SceneBaseTerrainRefresh()
    {
        EditorApplication.delayCall += ApplyLoadedScene;
        EditorSceneManager.sceneOpened += (scene, mode) => ApplyLoadedScene();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) ApplyLoadedScene();
        };
    }

    private static void ApplyLoadedScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyLoadedScene;
            return;
        }
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.path != "Assets/SceneBase.unity") continue;
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var bus in root.GetComponentsInChildren<BusController>(true))
                {
                    if (bus.GetComponent<BusWheelRig>() == null) continue;
                    var grounding = bus.GetComponent<SceneBaseGrounding>();
                    if (grounding == null)
                    {
                        grounding = Undo.AddComponent<SceneBaseGrounding>(bus.gameObject);
                        changed = true;
                    }
                    Undo.RecordObject(grounding, "Actualiser la suspension de SceneBase");
                    if (grounding.ApplyTerrainTuningUpdate())
                    {
                        EditorUtility.SetDirty(grounding);
                        changed = true;
                    }
                    var smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/SceneBaseData/DriftSmoke.mat");
                    var smoke = bus.GetComponent<SceneBaseDriftSmoke>();
                    if (smoke == null && smokeMaterial != null)
                    {
                        smoke = Undo.AddComponent<SceneBaseDriftSmoke>(bus.gameObject);
                        changed = true;
                    }
                    if (smoke != null && smoke.smokeMaterial == null && smokeMaterial != null)
                    {
                        Undo.RecordObject(smoke, "Ajouter la fumee de drift");
                        smoke.smokeMaterial = smokeMaterial;
                        EditorUtility.SetDirty(smoke);
                        changed = true;
                    }
                    if (smoke != null)
                    {
                        Undo.RecordObject(smoke, "Actualiser la fumee cartoon");
                        if (smoke.ApplyCartoonStyleUpdate())
                        {
                            EditorUtility.SetDirty(smoke);
                            changed = true;
                        }
                    }
                }
                if (!root.name.StartsWith("CroustyCity_LowPoly") &&
                    !root.name.StartsWith("CroustyCity_PastelPalette")) continue;
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "Road_Markings") continue;
                    foreach (var collider in child.GetComponents<Collider>())
                    {
                        if (!collider.enabled) continue;
                        Undo.RecordObject(collider, "Retirer la collision des marquages routiers");
                        collider.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                        EditorUtility.SetDirty(collider);
                        changed = true;
                    }
                }
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
