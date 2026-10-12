using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Migrates scenes already open in memory. Panels are saved editor objects, never created in Play.
public static class CroustyRunHudSceneUpgrade
{
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorSceneManager.sceneOpened += (_, __) => UpgradeLoadedScenes();
    }

    [MenuItem("Tools/Crousty/Placer l'horloge et la tournée dans le Canvas")]
    public static void UpgradeLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var hud in root.GetComponentsInChildren<CroustyHUD>(true))
            {
                var fields = new SerializedObject(hud);
                bool missing = false;
                foreach (string name in new[] { "timeValue", "timerValue", "scoreValue", "scoreGain" })
                    missing |= fields.FindProperty(name).objectReferenceValue == null;
                if (!missing) continue;
                var canvas = fields.FindProperty("canvas").objectReferenceValue as Canvas;
                if (canvas == null || fields.FindProperty("speedValue").objectReferenceValue == null) continue;
                Undo.RecordObject(hud,"Place the run HUD in the Canvas");
                Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject,"Place the run HUD in the Canvas");
                hud.PrepareUI();
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
