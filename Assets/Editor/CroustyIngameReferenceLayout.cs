using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CroustyIngameReferenceLayout
{
    private const string ScenePath = "Assets/SceneBase.unity";
    private const string Folder = "Assets/UI/IngameReference/";
    private static TMP_FontAsset font;
    private static readonly Color Ink = new Color32(38, 38, 50, 255);

    [MenuItem("Tools/Crousty/Appliquer la maquette du HUD ingame")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Install(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log("CROUSTY_HUD_LAYOUT_OK : maquette enregistree dans BASE uniquement.");
    }

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        EditorApplication.delayCall += UpgradeLoaded;
        EditorSceneManager.sceneOpened += (_, __) => UpgradeLoaded();
    }

    private static void UpgradeLoaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) return;
        var hud = Find<CroustyHUD>(scene);
        if (hud == null || new SerializedObject(hud).FindProperty("referenceLayoutRevision").intValue >= 3) return;
        if (AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Rectangle Argent.png") == null) return;
        Install(scene); // Preserve unsaved scene edits; saving is left to the user.
    }

    private static void Install(Scene scene)
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
        if (font == null) throw new InvalidOperationException("Police RetroBlendy absente.");
        var hud = Find<CroustyHUD>(scene);
        var map = Find<BusMinimap>(scene);
        if (hud == null || map == null) throw new InvalidOperationException("HUD et mini-carte requis dans BASE.");
        var props = new SerializedObject(hud);
        var canvas = props.FindProperty("canvas").objectReferenceValue as Canvas;
        if (canvas.transform.Find("HUD maquette") != null) { RefreshTypography(scene, hud, props); return; }
        foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Construire le HUD maquette");
        ConfigureCanvas(canvas);
        foreach (Transform child in canvas.transform)
            if (child.name != "Atouts" && child.name != "Modèle de repère" && child.name != "Passagers") child.gameObject.SetActive(false);
        var layout = Rect(canvas.transform, "HUD maquette", Vector2.zero, Vector2.zero, Vector2.zero);
        layout.anchorMax = Vector2.one;
        layout.offsetMin = layout.offsetMax = Vector2.zero;

        var money = Card(layout, "Argent", "Rectangle Argent.png", new Vector2(0, 1), new Vector2(48, -16), new Vector2(330, 142));
        var score = Label(money, "Valeur argent", "0", new Vector2(98, -27), new Vector2(195, 84), 76, Color.white);
        score.enableAutoSizing = true; score.fontSizeMin = 44; score.fontSizeMax = 76;
        var gain = Label(money, "Gain anime", "", new Vector2(250, -104), new Vector2(120, 42), 32, new Color(1, .8f, .15f, 0));
        var shipping = Card(layout, "Livraisons", "Rectangle shipping.png", new Vector2(0, 1), new Vector2(48, -158), new Vector2(310, 112));
        var deliveries = Label(shipping, "Progression", "0/5", new Vector2(110, -21), new Vector2(175, 69), 56, Color.white);
        var objective = Card(layout, "Objectif", "Rectangle Objectif.png", new Vector2(1, 1), new Vector2(-50, -18), new Vector2(565, 243));
        Label(objective, "Titre", "TASK", new Vector2(100, -15), new Vector2(425, 83), 67, Ink);
        var objectiveText = Label(objective, "Consigne", "Pick up your orders at\nCrousty's restaurant (0/1)", new Vector2(42, -120), new Vector2(465, 92), 28, Ink);
        objectiveText.enableAutoSizing = true; objectiveText.fontSizeMin = 23; objectiveText.fontSizeMax = 28;
        objectiveText.textWrappingMode = TextWrappingModes.Normal;
        var timer = Label(objective, "Chronometre", "01:30", new Vector2(415, -201), new Vector2(100, 35), 26, Ink);
        timer.gameObject.SetActive(false);
        Set(props, "deliveryValue", deliveries); Set(props, "objectiveValue", objectiveText);
        Set(props, "scoreValue", score); Set(props, "scoreGain", gain);
        Set(props, "timerValue", timer);
        props.FindProperty("referenceHudStyle").boolValue = true;
        props.FindProperty("referenceLayoutRevision").intValue = 3;

        ConfigureBonus(canvas);
        ConfigureMap(map, props);
        props.ApplyModifiedPropertiesWithoutUndo();
        ConfigureDialogue(scene, hud);
        var session = Find<SceneBaseSession>(scene);
        if (session != null)
        {
            var fields = new SerializedObject(session);
            fields.FindProperty("hideEmptyPassengerCounter").boolValue = true;
            var text = fields.FindProperty("passengerText").objectReferenceValue as Text;
            if (text != null)
            {
                text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Menu/RetroBlendy.otf");
                text.fontSize = 25; text.color = Ink;
                var rect = text.rectTransform;
                rect.SetParent(objective, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(42, -205); rect.sizeDelta = new Vector2(340, 35);
                var outline = text.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
                text.gameObject.SetActive(false);
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RefreshTypography(Scene scene, CroustyHUD hud, SerializedObject props)
    {
        if (props.FindProperty("referenceLayoutRevision").intValue >= 3) return;
        var objective = props.FindProperty("objectiveValue").objectReferenceValue as TMP_Text;
        var dialogue = Find<GerantDialogue>(scene);
        var message = dialogue != null ? new SerializedObject(dialogue).FindProperty("authoredMessage").objectReferenceValue as TMP_Text : null;
        foreach (var text in new[] { objective, message })
        {
            if (text == null) continue;
            Undo.RecordObject(text, "Ajuster les textes du HUD");
            text.enableAutoSizing = true; text.fontSizeMin = 23; text.fontSizeMax = text.fontSize = 28;
            EditorUtility.SetDirty(text);
        }
        if (objective != null)
        {
            Undo.RecordObject(objective.rectTransform, "Ajuster la consigne");
            objective.rectTransform.anchoredPosition = new Vector2(42, -120);
            objective.rectTransform.sizeDelta = new Vector2(465, 92);
        }
        var map = Find<BusMinimap>(scene);
        var mapCanvas = new SerializedObject(map).FindProperty("mapCanvas").objectReferenceValue as Canvas;
        MinimapSpeedBadge.Ensure(mapCanvas.transform.Find("Cadre") as RectTransform);
        Undo.RecordObject(hud, "Actualiser le HUD");
        props.FindProperty("referenceLayoutRevision").intValue = 3; props.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureBonus(Canvas canvas)
    {
        var row = canvas.transform.Find("Atouts") as RectTransform;
        var bonus = row.GetComponent<CroustyBonusHUD>();
        var fields = new SerializedObject(bonus);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
        row.anchoredPosition = new Vector2(48, -282); row.sizeDelta = new Vector2(230, 208); row.localScale = Vector3.one;
        fields.FindProperty("singleCardMode").boolValue = true;
        fields.FindProperty("selectedBonus").enumValueIndex = (int)CroustyBonusHUD.SelectedBonus.Pet;
        fields.FindProperty("language").enumValueIndex = (int)CroustyBonusHUD.BonusLanguage.English;
        foreach (var field in new[] { "pet", "gps", "rice", "flight" })
        {
            var card = fields.FindProperty(field);
            var art = card.FindPropertyRelative("artwork").objectReferenceValue as Image;
            var rect = art.transform.parent as RectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = row.sizeDelta; rect.localScale = Vector3.one;
            var name = card.FindPropertyRelative("nameText").objectReferenceValue as TMP_Text;
            var count = card.FindPropertyRelative("countText").objectReferenceValue as TMP_Text;
            name.font = count.font = font; name.outlineWidth = count.outlineWidth = 0;
            name.fontSize = 25; count.fontSize = 27;
            PlaceCentered(name.rectTransform, new Vector2(119, 26), new Vector2(204, 43));
            PlaceCentered(count.rectTransform, new Vector2(36, 79), new Vector2(46, 35));
            if (field == "pet")
            {
                card.FindPropertyRelative("englishName").stringValue = "TOXIC GAS";
                name.text = "TOXIC GAS";
            }
            rect.gameObject.SetActive(field == "pet");
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMap(BusMinimap map, SerializedObject hud)
    {
        var props = new SerializedObject(map);
        props.FindProperty("compactHud").boolValue = true;
        var canvas = props.FindProperty("mapCanvas").objectReferenceValue as Canvas;
        ConfigureCanvas(canvas);
        var frame = canvas.transform.Find("Cadre") as RectTransform;
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1, 0);
        frame.anchoredPosition = new Vector2(-50, 50); frame.sizeDelta = new Vector2(400, 386);
        var background = frame.GetComponent<Image>();
        background.sprite = Sprite("Rectangle GPS + Vitesse.png"); background.color = Color.white;
        background.type = Image.Type.Simple; background.preserveAspect = true;
        var mask = props.FindProperty("maskRect").objectReferenceValue as RectTransform;
        var oldMask = mask.GetComponent<RectMask2D>(); if (oldMask != null) UnityEngine.Object.DestroyImmediate(oldMask);
        PlaceCentered(mask, new Vector2(193, 193), new Vector2(316, 316));
        var circle = props.FindProperty("circleSprite").objectReferenceValue as Sprite;
        var image = mask.GetComponent<Image>() ?? mask.gameObject.AddComponent<Image>();
        image.sprite = circle; image.color = Color.white; image.raycastTarget = false;
        var clip = mask.GetComponent<Mask>() ?? mask.gameObject.AddComponent<Mask>(); clip.showMaskGraphic = false;
        var route = props.FindProperty("routeRoot").objectReferenceValue as RectTransform;
        route.sizeDelta = mask.sizeDelta;
        var speed = Label(frame, "Vitesse", "0\n<size=48%>km/h</size>", new Vector2(275, -273), new Vector2(105, 90), 48, Color.white);
        speed.alignment = TextAlignmentOptions.Center; speed.lineSpacing = -12;
        speed.outlineWidth = .16f; speed.outlineColor = Ink;
        MinimapSpeedBadge.Ensure(frame);
        Set(hud, "speedValue", speed); Set(hud, "speedFill", null);
        var clock = Label(frame, "Heure", "06:00", new Vector2(135, 29), new Vector2(120, 35), 24, Color.white);
        clock.alignment = TextAlignmentOptions.Center; Set(hud, "timeValue", clock);
        var gps = props.FindProperty("gpsLabel").objectReferenceValue as TMP_Text;
        PlaceCentered(gps.rectTransform, new Vector2(177, 14), new Vector2(245, 35));
        gps.font = font; gps.fontSize = 17; gps.color = Color.white;
        props.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureDialogue(Scene scene, CroustyHUD hud)
    {
        var dialogue = Find<GerantDialogue>(scene) ?? hud.gameObject.AddComponent<GerantDialogue>();
        dialogue.manager = hud.manager;
        var root = new GameObject("Dialogue maquette", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(hud.transform, false);
        var canvas = root.GetComponent<Canvas>(); ConfigureCanvas(canvas); canvas.sortingOrder = 105;
        var panel = Rect(root.transform, "Gerant", Vector2.zero, new Vector2(50, 42), new Vector2(830, 430));
        panel.pivot = Vector2.zero;
        ImageAt(panel, "Papier", Resources.Load<Sprite>("Gerant/bandeau"), new Vector2(65, -265), new Vector2(750, 172));
        ImageAt(panel, "Etiquette", Resources.Load<Sprite>("Gerant/etiquette"), new Vector2(178, -192), new Vector2(294, 100));
        ImageAt(panel, "Portrait", Resources.Load<Sprite>("Gerant/portrait"), new Vector2(-10, -36), new Vector2(345, 397));
        Label(panel, "Titre", "MANAGER", new Vector2(212, -223), new Vector2(184, 51), 35, new Color32(239, 233, 225, 255));
        var message = Label(panel, "Message", dialogue.deliveryStartLines[0], new Vector2(263, -300), new Vector2(505, 110), 28, Ink);
        message.enableAutoSizing = true; message.fontSizeMin = 23; message.fontSizeMax = 28;
        message.textWrappingMode = TextWrappingModes.Normal;
        var arrow = ImageAt(panel, "Suite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/RepereFleche.asset"), new Vector2(733, -391), new Vector2(26, 24));
        arrow.color = Ink; arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        var props = new SerializedObject(dialogue);
        Set(props, "authoredMessage", message); Set(props, "authoredArrow", arrow.rectTransform); Set(props, "authoredCanvas", canvas);
        props.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCanvas(Canvas canvas)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
    }
    private static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; }
        return null;
    }
    private static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + file) ?? throw new InvalidOperationException("Sprite manquant : " + file);
    private static RectTransform Card(Transform parent, string name, string file, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = Sprite(file); image.preserveAspect = true; image.raycastTarget = false;
        return rect;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    private static TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize;
        text.text = value; text.color = color; text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Truncate;
        return text;
    }
    private static Image ImageAt(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, new Vector2(0, 1), position, size); var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
    }
    private static void PlaceCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Set(SerializedObject props, string name, UnityEngine.Object value) => props.FindProperty(name).objectReferenceValue = value;
}
