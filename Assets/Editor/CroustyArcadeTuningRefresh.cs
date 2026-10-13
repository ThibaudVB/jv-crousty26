using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Synchronise les anciennes valeurs en memoire apres la recompilation, sans recharger la scene.
[InitializeOnLoad]
public static class CroustyArcadeTuningRefresh
{
    static CroustyArcadeTuningRefresh()
    {
        EditorApplication.delayCall += ApplyLoadedScene;
        EditorSceneManager.sceneOpened += (scene, mode) => ApplyLoadedScene();
    }

    private static void ApplyLoadedScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyLoadedScene;
            return;
        }
        foreach (var controller in Object.FindObjectsByType<BusController>(FindObjectsInactive.Include))
        {
            if (!controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded) continue;
            var rig = controller.GetComponent<BusWheelRig>();
            if (rig == null) continue;
            if (!EditorApplication.isPlaying)
                Undo.RecordObjects(new Object[] { controller, rig }, "Actualiser la conduite arcade");
            if (!rig.ApplyArcadeTuningUpdate(controller)) continue;
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(rig);
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }
            Debug.Log("[Crousty] Conduite actualisee dans la scene chargee : direction plus reactive, retour au centre rapide et glisse reduite.", controller);
        }
    }
}
