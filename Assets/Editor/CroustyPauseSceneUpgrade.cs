using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Repairs pause menus kept in memory before their buttons were connected on disk.
// Copies only the authored options UI; never reloads the user's scene or HUD.
public static class CroustyPauseSceneUpgrade
{
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += UpgradeLoadedScenes;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
                UpgradeLoadedScenes();
        };
    }

    [MenuItem("Tools/Crousty/Mettre à jour les boutons pause")]
    public static void UpgradeLoadedScenes()
    {
        if (EditorApplication.isPlaying) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var pause in root.GetComponentsInChildren<CroustyPauseMenu>(true))
                Upgrade(pause);
        }
    }

    private static void Upgrade(CroustyPauseMenu pause)
    {
        var serialized = new SerializedObject(pause);
        var panel = (GameObject)serialized.FindProperty("pausePanel").objectReferenceValue;
        if (panel == null) return;
        var card = panel.transform.Find("Fond du menu");
        if (card == null) return;
        var settings = FindButton(card, "Bouton réglages");
        var mainMenu = FindButton(card, "Bouton menu principal");
        if (settings == null || mainMenu == null) return;
        var options = (CroustyMenu)serialized.FindProperty("optionsMenu").objectReferenceValue;
        if (options != null && options.optionsPanel != null &&
            IsLinked(settings, pause, "OpenSettings") && IsLinked(mainMenu, pause, "ReturnToMainMenu"))
        {
            if (EnsureOptionsLayout(options)) EditorSceneManager.MarkSceneDirty(pause.gameObject.scene);
            return;
        }

        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Relier le menu pause");
        if (options == null || options.optionsPanel == null)
        {
            string path = pause.gameObject.scene.path;
            if (path != "Assets/SceneBase.unity" && path != "Assets/Jojo/SceneJojo.unity")
                path = "Assets/SceneBase.unity";
            var preview = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                CroustyMenu source = null;
                foreach (var root in preview.GetRootGameObjects())
                {
                    var sourcePause = root.GetComponentInChildren<CroustyPauseMenu>(true);
                    if (sourcePause != null) source = sourcePause.GetComponent<CroustyMenu>();
                    if (source != null) break;
                }
                if (source == null || source.optionsPanel == null)
                {
                    Debug.LogError("Le Canvas des réglages pause est absent de la scène enregistrée.", pause);
                    return;
                }
                // Clone the complete source to preserve every internal button/slider reference.
                var clone = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(clone, pause.gameObject.scene);
                clone.name = "Contrôleur réglages pause";
                options = clone.GetComponent<CroustyMenu>();
                options.optionsPanel.transform.SetParent(pause.transform, false);
                options.mainPanel = panel;
                options.pauseMenu = pause;
                options.optionsPanel.SetActive(false);
                // Scene-local text materials must survive closing the preview scene.
                foreach (var text in options.optionsPanel.GetComponentsInChildren<TMP_Text>(true))
                    if (text.fontSharedMaterial != null && !EditorUtility.IsPersistent(text.fontSharedMaterial))
                        text.fontSharedMaterial = new Material(text.fontSharedMaterial);
                Object.DestroyImmediate(clone.GetComponent<CroustyPauseMenu>());
                while (clone.transform.childCount > 0)
                    Object.DestroyImmediate(clone.transform.GetChild(0).gameObject);
                clone.transform.SetParent(pause.transform, false);
                Undo.RegisterCreatedObjectUndo(clone, "Ajouter les réglages pause");
                Undo.RegisterCreatedObjectUndo(options.optionsPanel, "Ajouter le panel réglages pause");
                serialized.Update();
                serialized.FindProperty("optionsMenu").objectReferenceValue = options;
                serialized.ApplyModifiedProperties();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        Link(settings, pause.OpenSettings);
        Link(mainMenu, pause.ReturnToMainMenu);
        EnsureOptionsLayout(options);
        EditorSceneManager.MarkSceneDirty(pause.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Debug.Log("[Crousty] Boutons pause mis à jour dans la scène ouverte. Enregistre la scène pour conserver les liens.", pause);
    }

    public static bool EnsureOptionsLayout(CroustyMenu menu)
    {
        var root = menu.optionsPanel.GetComponent<RectTransform>();
        if (root.GetComponent<Canvas>() != null && root.Find("Contenu réglages pause") != null) return false;
        Undo.RecordObject(root, "Adapter les réglages au Canvas pause");
        Undo.SetTransformParent(root, menu.pauseMenu.transform, "Séparer le Canvas des réglages");
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = Vector2.zero;
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(root.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 601;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(root.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(848, 480);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (root.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(root.gameObject);
        var content = new GameObject("Contenu réglages pause", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(content, "Centrer les options pause");
        var rect = content.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(848, 480);
        var children = new Transform[root.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = root.GetChild(i);
        foreach (var child in children)
        {
            if (child == rect) continue;
            if (child.name == "Fond blanc" || child.name == "Voile réglages")
            {
                Undo.RecordObject(child.gameObject, "Assombrir le fond des options pause");
                child.name = "Voile réglages";
                var image = child.GetComponent<Image>();
                Undo.RecordObject(image, "Assombrir le fond des options pause");
                image.color = new Color32(20, 22, 30, 210);
                image.raycastTarget = true;
            }
            else
            {
                var childRect = (RectTransform)child;
                var position = childRect.anchoredPosition;
                var size = childRect.sizeDelta;
                Undo.SetTransformParent(child, rect, "Centrer le contenu des options");
                childRect.localScale = Vector3.one;
                childRect.anchoredPosition = position;
                childRect.sizeDelta = size;
            }
        }
        return true;
    }

    private static Button FindButton(Transform card, string name) =>
        (card.Find(name) ?? card.Find(name + " (placeholder)"))?.GetComponent<Button>();

    private static bool IsLinked(Button button, CroustyPauseMenu pause, string method) =>
        button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == pause &&
        button.onClick.GetPersistentMethodName(0) == method;

    private static void Link(Button button, UnityAction action)
    {
        Undo.RecordObject(button, "Relier le bouton pause");
        Undo.RecordObject(button.gameObject, "Renommer le bouton pause");
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        button.name = button.name.Replace(" (placeholder)", "");
        EditorUtility.SetDirty(button);
    }
}
