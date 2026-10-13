using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CroustyMenuReferenceLayout
{
    private const string ScenePath = "Assets/Scenes/CroustyMainMenu.unity";
    private static readonly Color Ink = new Color32(38, 38, 50, 255);
    private static readonly Color Cream = new Color32(239, 233, 225, 255);
    private static readonly Color Orange = new Color32(233, 132, 36, 255);
    private static TMP_FontAsset font;
    private static Sprite creamRibbon, orangeRibbon;

    [MenuItem("Tools/Crousty/Appliquer le menu Voir plus et Voir moins")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        var menu = FindMenu(scene);
        if (menu.homePages != null)
        {
            InstallTicket(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Crousty] Ticket continu enregistré dans le menu existant.");
            return;
        }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
        creamRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/MainRibbonCream.png");
        orangeRibbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/MainRibbonOrange.png");
        var tabSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/MainMenuTicket.png");
        if (font == null || creamRibbon == null || orangeRibbon == null || tabSprite == null)
            throw new InvalidOperationException("Les assets de la maquette doivent être importés avant la conversion.");
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Adapter le menu à la maquette");
        Transform main = menu.mainPanel.transform;
        var original = new List<Transform>();
        foreach (Transform child in main) original.Add(child);
        var frame = Rect(main, "Cadre maquette 848x480", 0, 0, 848, 480);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        foreach (var child in original)
        {
            if (IsOldButton(child.name)) { UnityEngine.Object.DestroyImmediate(child.gameObject); continue; }
            if (child.name == "Fond bleu") continue;
            child.SetParent(frame, false);
        }
        var canvas = menu.GetComponentInChildren<Canvas>(true);
        canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var viewport = Rect(frame, "Fenêtre des deux menus", 0, 0, 440, 480);
        viewport.gameObject.AddComponent<RectMask2D>();
        var first = Rect(viewport, "Menu principal", 0, 0, 440, 480);
        var second = Rect(viewport, "Menu supplémentaire", 0, 480, 440, 480);
        var firstGroup = first.gameObject.AddComponent<CanvasGroup>();
        var secondGroup = second.gameObject.AddComponent<CanvasGroup>();
        var logos = new List<Transform>();
        foreach (Transform child in frame)
            if (child.name == "Logo final" || child.name.Contains("logo") ||
                child.name == "CROUSTY texte" || child.name == "EXPRESS texte") logos.Add(child);
        foreach (var logo in logos) logo.SetParent(first, false);
        var buttons = new List<Button>();
        buttons.Add(Ribbon(first, "JOUER", "START GAME", 165, 38, menu.Play));
        buttons.Add(Ribbon(first, "LEADERBOARD", "LEADERBOARD", 235, 31, menu.OpenLeaderboard));
        buttons.Add(Ribbon(second, "RÉGLAGES", "SETTINGS", 166, 34, menu.OpenSettings));
        buttons.Add(Ribbon(second, "CRÉDITS", "CREDITS", 236, 34, menu.OpenCredits));
        buttons.Add(Ribbon(second, "QUITTER", "LEAVE GAME", 306, 34, menu.Quit));

        var tab = Rect(frame, "Voir plus et Voir moins", 60, 392, 232, 88);
        var tabButton = Button(tab, menu.ToggleMore);
        var tabVisual = Rect(tab, "Visuel du papier", 0, 0, 232, 88);
        var tabHover = tab.gameObject.AddComponent<CroustyMenuHover>();
        tabHover.target = tabVisual;
        tabHover.actionPrompt = Prompt(tabVisual, 216, 16);
        buttons.Add(tabButton);
        var pages = main.gameObject.AddComponent<CroustyMenuPages>();
        pages.viewport = viewport;
        pages.firstPage = first;
        pages.secondPage = second;
        pages.firstGroup = firstGroup;
        pages.secondGroup = secondGroup;
        pages.navigationTab = tab;
        pages.buttons = buttons.ToArray();
        menu.homePages = pages;
        InstallTicket(menu);
        second.gameObject.SetActive(false);
        menu.leaderboardPanel = BuildLeaderboard(menu, main.parent);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Crousty] Maquette enregistrée : deux pages, survol orange, onglet mobile et assets fournis.");
    }

    [InitializeOnLoadMethod]
    private static void WatchLoadedMenu()
    {
        EditorApplication.delayCall += UpgradeLoadedMenu;
        EditorSceneManager.sceneOpened += (scene, mode) => UpgradeLoadedMenu();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) UpgradeLoadedMenu();
        };
    }

    private static void UpgradeLoadedMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += UpgradeLoadedMenu;
            return;
        }
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded || AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/MainMenuTicket.png") == null) return;
        var menu = FindMenu(scene);
        if (menu.homePages == null || menu.homePages.ticketImage != null) return;
        InstallTicket(menu);
        EditorSceneManager.MarkSceneDirty(scene); // Preserve unsaved edits; do not reload or save the open scene.
    }

    private static void InstallTicket(CroustyMenu menu)
    {
        var pages = menu.homePages;
        if (pages.ticketImage != null) return;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/MainMenuTicket.png");
        if (sprite == null) throw new InvalidOperationException("Importer MainMenuTicket.png avant de convertir le ticket.");
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Installer le ticket de défilement");
        var clip = Rect(menu.mainPanel.transform, "Fenêtre du ticket continu", 0, 0, 848, 480);
        clip.anchorMin = Vector2.zero;
        clip.anchorMax = Vector2.one;
        clip.offsetMin = clip.offsetMax = Vector2.zero;
        clip.gameObject.AddComponent<RectMask2D>();
        float height = 232f * sprite.rect.height / sprite.rect.width;
        var tab = pages.navigationTab;
        tab.SetParent(clip, false);
        tab.sizeDelta = new Vector2(232, height);
        tab.localScale = Vector3.one;
        tab.localRotation = Quaternion.identity;
        tab.anchoredPosition = new Vector2(60, -480f + pages.tabHeight);
        var visual = tab.Find("Visuel du papier") as RectTransform;
        var obsolete = new List<GameObject>();
        foreach (Transform child in visual)
            if (child.name != "Indication E au survol") obsolete.Add(child.gameObject);
        foreach (var child in obsolete) UnityEngine.Object.DestroyImmediate(child);
        visual.sizeDelta = new Vector2(232, height);
        visual.anchoredPosition = Vector2.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        var image = Image(visual, "Ticket imprimé Voir Plus et Voir Moins", sprite, 0, 0, 232, height);
        image.preserveAspect = true;
        image.transform.SetAsFirstSibling();
        var hover = tab.GetComponent<CroustyMenuHover>();
        hover.animateTransform = false;
        pages.navigationViewport = clip;
        pages.ticketImage = image;
        pages.navigationPrompt = hover.actionPrompt.transform as RectTransform;
        EditorUtility.SetDirty(pages);
        EditorUtility.SetDirty(hover);
    }

    private static CroustyMenu FindMenu(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var menu = root.GetComponentInChildren<CroustyMenu>(true);
            if (menu != null && menu.pauseMenu == null) return menu;
        }
        throw new InvalidOperationException("Menu principal introuvable.");
    }

    private static bool IsOldButton(string name) => name == "JOUER" || name == "RÉGLAGES" || name == "QUITTER" ||
        name.StartsWith("JOUER ") || name.StartsWith("RÉGLAGES ") || name.StartsWith("QUITTER ");

    private static Button Ribbon(Transform parent, string name, string label, float y, float size, UnityAction action)
    {
        var hit = Rect(parent, name, 0, y, 418, 104);
        var button = Button(hit, action);
        var visual = Rect(hit, "Visuel du bandeau", 0, 0, 380, 104);
        var shadow = Image(visual, "Ombre", creamRibbon, 0, 3, 380, 94);
        shadow.color = Ink;
        var ribbon = Image(visual, "Papier", creamRibbon, 0, -1, 380, 94);
        var text = Text(visual, label, 25, 29, 310, 46, size, Ink);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = new Vector2(180, -52);
        text.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
        var hover = hit.gameObject.AddComponent<CroustyMenuHover>();
        hover.target = visual;
        hover.ribbonImage = ribbon;
        hover.idleRibbon = creamRibbon;
        hover.activeRibbon = orangeRibbon;
        hover.textTarget = text;
        hover.activeTextColor = Cream;
        hover.hoverScale = 1.1f;
        hover.actionPrompt = Prompt(visual, 350, 8);
        return button;
    }

    private static GameObject Prompt(Transform parent, float x, float y)
    {
        var root = Rect(parent, "Indication E au survol", x, y, 34, 38);
        var white = Rect(root, "Contour blanc", 0, 0, 34, 34).gameObject.AddComponent<CroustyVectorGraphic>();
        white.points = Circle(); white.color = Color.white; white.raycastTarget = false;
        var fill = Rect(root, "Touche orange", 4, 4, 26, 26).gameObject.AddComponent<CroustyVectorGraphic>();
        fill.points = Circle(); fill.color = new Color32(248, 76, 40, 255); fill.raycastTarget = false;
        Text(root, "E", 0, 1, 34, 31, 20, Color.white);
        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    private static GameObject BuildLeaderboard(CroustyMenu menu, Transform parent)
    {
        var panel = Rect(parent, "Leaderboard", 0, 0, 848, 480);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<CanvasGroup>();
        var background = Rect(panel, "Fond bleu", 0, 0, 848, 480).gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color32(7, 153, 243, 255);
        background.raycastTarget = false;
        var card = Image(panel, "Papier du classement", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/OptionsPanel.png"), 270, 75, 550, 345);
        Text(panel, "LEADERBOARD", 300, 105, 440, 60, 38, Ink);
        Text(panel, "Aucun score enregistré", 300, 205, 410, 50, 25, Ink);
        Text(panel, "Les scores apparaîtront ici.", 300, 265, 410, 40, 20, Ink);
        Ribbon(panel, "Retour classement", "RETOUR", 25, 30, menu.CloseLeaderboard);
        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    private static Button Button(RectTransform rect, UnityAction action)
    {
        var hit = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        hit.color = new Color(1, 1, 1, 0.001f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return button;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float width, float height)
    {
        var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
    {
        var text = Rect(parent, value + " texte", x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static Vector2[] Circle()
    {
        var points = new Vector2[32];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * 2f * Mathf.PI / points.Length;
            points[i] = new Vector2(.5f + .5f * Mathf.Cos(angle), .5f + .5f * Mathf.Sin(angle));
        }
        return points;
    }
}
