using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JojoHudImportChecks
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        var hearts = UnityEngine.Object.FindObjectsByType<BusHealthHearts>(FindObjectsSortMode.None);
        if (hearts.Length != 1 || hearts[0].busDamage == null || hearts[0].fullHeart == null || hearts[0].emptyHeart == null)
            throw new Exception("Missing hearts or bus/sprite references");
        var hud = UnityEngine.Object.FindAnyObjectByType<CroustyHUD>();
        var props = new SerializedObject(hud);
        foreach (var field in new[] { "canvas", "deliveryValue", "objectiveValue", "speedValue", "timeValue", "timerValue", "scoreValue", "scoreGain", "waypointTemplate" })
            if (props.FindProperty(field).objectReferenceValue == null) throw new Exception("Missing HUD reference: " + field);
        var targetRects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
        var source = EditorSceneManager.OpenScene("Assets/Jojo/SceneBaseJojo.unity", OpenSceneMode.Additive);
        var sourceRects = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
        foreach (var id in new long[] {230788717, 1258704250, 1268311059, 1426177791, 1913539886, 2113424812, 920000000000000121, 930000000000000101})
        {
            var a = targetRects.Single(r => (long)GlobalObjectId.GetGlobalObjectIdSlow(r).targetObjectId == id);
            var b = sourceRects.Single(r => (long)GlobalObjectId.GetGlobalObjectIdSlow(r).targetObjectId == id);
            if (a.anchorMin != b.anchorMin || a.anchorMax != b.anchorMax || a.anchoredPosition != b.anchoredPosition || a.localScale != b.localScale || a.pivot != b.pivot || a.sizeDelta != b.sizeDelta)
                throw new Exception("Layout mismatch: " + a.name);
        }
        Debug.Log("JOJO_HUD_IMPORT_PASS: 8 layouts match Jojo; hearts, sprites, bus and HUD references valid.");
    }
}
