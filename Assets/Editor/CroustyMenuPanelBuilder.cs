using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Vector-drawn title and options menus, designed on an 848 x 480 canvas.</summary>
public sealed class CroustyMenuPanelBuilder
{
    private Transform transform;

    [MenuItem("Tools/Crousty/Convertir les menus en panels")]
    public static void ConvertScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string path = "Assets/Scenes/CroustyMainMenu.unity";
        var scene = SceneManager.GetSceneByPath(path);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        CroustyMenu menu = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            menu = root.GetComponentInChildren<CroustyMenu>(true);
            if (menu != null) break;
        }
        if (menu == null) throw new System.InvalidOperationException("CroustyMenu introuvable.");
        if (menu.transform.Find("Crousty Menu Canvas") != null)
        {
            if (menu.GetComponentInChildren<EventSystem>(true) == null)
            {
                var events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(menu.transform, false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("Les panels existent déjà : aucune mise en page remplacée.", menu);
            return;
        }
        var builder = new CroustyMenuPanelBuilder();
        builder.transform = menu.transform;
        builder.logoOverride = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("ecfc1c76427874e03b2d62d3e7a3ad82"));
        builder.orangeRibbon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("5bdb991265688419fae47094ae75f7d3"));
        builder.creamRibbon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("7c70cb0c66cf74fc59538b3420745a62"));
        builder.menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("6a57f33945af343e29f1558f4a2c90f0"));
        builder.font = builder.menuFont != null ? builder.menuFont : TMP_Settings.defaultFontAsset;
        builder.Build();
        builder.Wire(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Crousty] Menus convertis et enregistrés : Accueil, Réglages et 4 panels d'options.", menu);
    }

    private void Wire(CroustyMenu menu)
    {
        menu.mainPanel = mainRoot.gameObject;
        menu.optionsPanel = optionsRoot.gameObject;
        menu.categoryPanels = new GameObject[4];
        for (int i = 0; i < 4; i++) menu.categoryPanels[i] = optionsContent.GetChild(i).gameObject;
        menu.tabs = tabs;
        menu.tabBackgrounds = new Graphic[4];
        menu.tabShadows = new Graphic[4];
        menu.tabLabels = new TMP_Text[4];
        string[] names = { "COMMANDES", "AUDIO", "VIDÉO", "CRÉDITS" };
        for (int i = 0; i < 4; i++)
        {
            menu.tabBackgrounds[i] = optionsRoot.Find(names[i] + " couleur").GetComponent<Graphic>();
            menu.tabShadows[i] = optionsRoot.Find(names[i] + " ombre")?.GetComponent<Graphic>();
            menu.tabLabels[i] = tabs[i].GetComponentInChildren<TMP_Text>(true);
            UnityEventTools.AddIntPersistentListener(tabs[i].GetComponent<Button>().onClick, menu.SelectCategory, i);
        }
        menu.orangeRibbon = orangeRibbon;
        menu.creamRibbon = creamRibbon;
        UnityEventTools.AddPersistentListener(mainRoot.Find("JOUER").GetComponent<Button>().onClick, menu.Play);
        UnityEventTools.AddPersistentListener(mainRoot.Find("RÉGLAGES").GetComponent<Button>().onClick, menu.OpenOptions);
        UnityEventTools.AddPersistentListener(mainRoot.Find("QUITTER").GetComponent<Button>().onClick, menu.Quit);
        UnityEventTools.AddPersistentListener(optionsRoot.Find("RETOUR").GetComponent<Button>().onClick, menu.CloseOptions);
        menu.sensitivitySlider = menu.categoryPanels[0].GetComponentInChildren<Slider>(true);
        menu.volumeSlider = menu.categoryPanels[1].GetComponentInChildren<Slider>(true);
        menu.sensitivityLabel = menu.categoryPanels[0].transform.Find("0.75 texte").GetComponent<TMP_Text>();
        menu.volumeLabel = menu.categoryPanels[1].transform.Find("100% texte").GetComponent<TMP_Text>();
        var fullscreen = menu.categoryPanels[2].GetComponentInChildren<Button>(true);
        menu.fullscreenLabel = fullscreen.GetComponentInChildren<TMP_Text>(true);
        UnityEventTools.AddPersistentListener(menu.sensitivitySlider.onValueChanged, menu.SetSensitivity);
        UnityEventTools.AddPersistentListener(menu.volumeSlider.onValueChanged, menu.SetVolume);
        UnityEventTools.AddPersistentListener(fullscreen.onClick, menu.ToggleFullscreen);
        EditorUtility.SetDirty(menu);
    }

    [Tooltip("Optional final logo sprite. The vector text logo is used when empty.")]
    public Sprite logoOverride;
    public Sprite orangeRibbon;
    public Sprite creamRibbon;
    public Sprite mainOrangeRibbon;
    public Sprite mainCreamRibbon;
    public Sprite optionsPanel;
    public Sprite blackTag;
    public TMP_FontAsset menuFont;

    public const string SensitivityKey = "Crousty.MouseSensitivity";
    private const string VolumeKey = "Crousty.MasterVolume";
    private static readonly Color Blue = Hex("#0799F3");
    private static readonly Color Orange = Hex("#E98424");
    private static readonly Color Gold = Hex("#FFAA05");
    private static readonly Color Cream = Hex("#EFE9E1");
    private static readonly Color Ink = Hex("#262632");
    private static readonly Color White = Hex("#FFFDF7");
    private TMP_FontAsset font;
    private RectTransform canvasRect;
    private RectTransform mainRoot;
    private RectTransform optionsRoot;
    private RectTransform optionsContent;
    private CanvasGroup mainGroup;
    private CanvasGroup optionsGroup;
    private CanvasGroup contentGroup;
    private CroustyMenuHover[] tabs;







    private void Build()
    {
        GameObject canvasObject = new GameObject("Crousty Menu Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(848, 480);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = canvasObject.GetComponent<RectTransform>();
        if (transform.GetComponentInChildren<EventSystem>(true) == null)
        {
            GameObject events = new GameObject("Menu Event System", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }

        mainRoot = FullRoot(canvasRect, "Accueil");
        mainGroup = mainRoot.gameObject.AddComponent<CanvasGroup>();
        BuildMain();
        optionsRoot = FullRoot(canvasRect, "Réglages");
        optionsGroup = optionsRoot.gameObject.AddComponent<CanvasGroup>();
        BuildOptions();
        optionsRoot.gameObject.SetActive(false);
    }

    private void BuildMain()
    {
        mainRoot.gameObject.AddComponent<CroustyMenuVideoBackground>();
        DrawLogo(mainRoot);
        MainRibbon("JOUER", 210, true, null);
        MainRibbon("RÉGLAGES", 285, false, null);
        MainRibbon("QUITTER", 360, false, null);
    }

    private void DrawLogo(RectTransform root)
    {
        if (logoOverride != null)
        {
            Image logo = Rect(root, "Logo final", 19, 27, 350, 148).gameObject.AddComponent<Image>();
            logo.sprite = logoOverride;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            return;
        }
        Art(root, "Silhouette du logo", Ink, 30, 55, 45, 42, 70, 29, 95, 33,
            121, 30, 140, 39, 159, 31, 180, 34, 202, 31, 229, 35,
            241, 28, 315, 40, 331, 49, 341, 37, 355, 45, 368, 57,
            368, 76, 346, 107, 356, 119, 350, 142, 315, 152,
            310, 164, 274, 166, 270, 160, 116, 166, 112, 171,
            66, 173, 60, 162, 22, 145, 20, 122, 36, 116, 30, 93);
        Art(root, "Trait clair du logo", Cream, 24, 121, 120, 99, 344, 98,
            356, 107, 341, 118, 356, 122, 330, 139, 287, 144, 276, 151,
            110, 154, 88, 163, 83, 153, 19, 145, 20, 123);
        Text(root, "CROUSTY", 31, 23, 340, 94, 80, Orange, TextAlignmentOptions.Center, Ink, 0.24f, -4);
        Text(root, "EXPRESS", 41, 87, 316, 74, 66, White, TextAlignmentOptions.Center, Ink, 0.27f, -5);
    }

    private void MainRibbon(string label, float y, bool orange, UnityEngine.Events.UnityAction action)
    {
        Color fill = orange ? Orange : Cream;
        Sprite ribbon = orange ? mainOrangeRibbon : mainCreamRibbon;
        if (ribbon != null)
        {
            Image shadow = ImageAt(mainRoot, label + " ombre", ribbon, 0, y + 1, 390, 94);
            shadow.color = Ink;
            ImageAt(mainRoot, label + " bande", ribbon, 0, y - 3, 390, 94);
        }
        else
        {
            Art(mainRoot, label + " ombre", Ink, 0, y + 36, 350, y + 4,
                380, y + 3, 361, y + 15, 383, y + 23, 358, y + 34,
                389, y + 50, 363, y + 60, 390, y + 72, 0, y + 94);
            Art(mainRoot, label + " bande", fill, 0, y + 31, 354, y + 4,
                378, y, 357, y + 13, 382, y + 19, 360, y + 30,
                384, y + 38, 357, y + 51, 386, y + 58, 0, y + 90);
        }
        Button b = Hit(mainRoot, label, 0, y, 389, 92, action);
        TMP_Text ribbonText = Text(b.transform, label, 30, 25, 210, 60, 34,
            orange ? White : Ink, TextAlignmentOptions.Center,
            orange ? Hex("#8C4E1C") : Color.clear, orange ? 0.1f : 0f, 5f);
        // Tourner autour du centre garde le libellé au milieu du bandeau incliné.
        ribbonText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ribbonText.rectTransform.anchoredPosition = new Vector2(135f, -55f);
        b.gameObject.AddComponent<CroustyMenuHover>();
    }

    private void DrawBus(RectTransform root)
    {
        Art(root, "Rouge du toit", Hex("#DD0D02"), 548, 153, 550, 143,
            562, 136, 568, 139, 557, 120, 558, 113, 565, 103,
            581, 110, 583, 87, 593, 77, 604, 74, 615, 85, 625, 138);
        Art(root, "Côté du bus", White, 420, 248, 452, 215, 551, 151,
            626, 137, 692, 157, 717, 194, 755, 293, 774, 374,
            740, 403, 493, 425, 439, 412, 421, 379);
        Art(root, "Liseré du toit", Gold, 439, 234, 551, 156, 624, 139,
            694, 160, 715, 189, 717, 209, 637, 203, 531, 184,
            486, 213, 477, 207, 530, 177);
        Art(root, "Flanc orange", Gold, 420, 369, 485, 343, 548, 331,
            686, 314, 778, 347, 773, 392, 494, 418, 446, 417);
        Art(root, "Bas de flanc", Hex("#F79600"), 486, 358, 548, 341,
            687, 328, 778, 350, 777, 390, 497, 421);
        Art(root, "Fenêtre gauche", Hex("#2F9ADE"), 492, 234, 533, 216,
            554, 303, 498, 318);
        Art(root, "Fenêtre frontale", Hex("#309CDE"), 546, 213, 710, 220,
            741, 302, 560, 301);
        Art(root, "Montant pare-brise", White, 533, 213, 544, 209,
            565, 302, 551, 305);
        Art(root, "Casquette avant", Gold, 488, 204, 532, 184,
            716, 194, 719, 214, 532, 204, 487, 230);
        Art(root, "Pare-brise haut", Ink, 556, 152, 625, 143, 694, 159,
            685, 187, 646, 188, 623, 167, 604, 184, 568, 183);
        Art(root, "Éclat", Gold, 626, 167, 647, 166, 652, 184, 629, 200, 607, 181);
        Art(root, "Miroir", Ink, 524, 284, 546, 285, 552, 310, 526, 313);
        Art(root, "Face avant", Gold, 492, 326, 560, 300, 787, 302,
            795, 351, 577, 395, 496, 389);
        Art(root, "Phares gauche", White, 602, 306, 632, 306, 634, 337, 605, 337);
        Art(root, "Phares gauche doré", Gold, 606, 308, 634, 309, 637, 340, 601, 340);
        Art(root, "Phares droite doré", Gold, 752, 305, 786, 306, 789, 340, 755, 340);
        Art(root, "Phares droite", White, 759, 311, 780, 310, 783, 335, 760, 334);
        Art(root, "Calandre", Color.black, 658, 307, 730, 308, 733, 341, 659, 341);
        Art(root, "Pare-chocs", Color.black, 579, 351, 807, 350,
            808, 370, 580, 377);
        Art(root, "Roue arrière", Color.black, 434, 389, 449, 382,
            464, 391, 470, 418, 459, 434, 440, 427);
        Art(root, "Jante arrière", White, 443, 392, 451, 391, 456, 420, 448, 423);
        Art(root, "Roue avant", Color.black, 535, 365, 552, 359,
            572, 369, 584, 401, 580, 424, 561, 434, 540, 427, 528, 408);
        Art(root, "Jante avant", White, 541, 375, 553, 374, 561, 399,
            558, 416, 548, 419, 539, 407);
        Art(root, "Roue côté droit", Color.black, 711, 385, 775, 378,
            777, 405, 765, 423, 720, 426, 710, 412);
    }

    private void DrawRoad(RectTransform root)
    {
        Art(root, "Route", Hex("#353535"), 340, 443, 365, 430, 468, 418,
            555, 412, 675, 407, 848, 414, 848, 480, 335, 480);
        Art(root, "Route ombre", Hex("#252525"), 344, 451, 440, 426,
            659, 412, 848, 415, 848, 480, 335, 480);
        Art(root, "Marquage 1", White, 568, 422, 595, 423, 642, 433, 626, 436);
        Art(root, "Marquage 2", White, 696, 448, 739, 445, 787, 457, 756, 467);
    }

    private void BuildOptions()
    {
        Solid(optionsRoot, "Fond blanc", Color.white);
        Art(optionsRoot, "Trait bleu", Blue, 0, 0, 848, 0, 848, 3, 0, 3);
        if (optionsPanel != null)
            ImageAt(optionsRoot, "Panneau", optionsPanel, 325, 60, 523, 365);
        else
        {
            Art(optionsRoot, "Panneau ombre", Hex("#7E7B77"), 325, 69, 846, 67,
                769, 135, 850, 192, 769, 269, 849, 321, 768, 369,
                847, 424, 328, 424);
            Art(optionsRoot, "Panneau", Cream, 325, 60, 848, 61,
                768, 132, 848, 186, 769, 263, 848, 315, 769, 361,
                848, 413, 326, 414);
        }
        SmallRibbon(optionsRoot, "RETOUR", 0, 16, 170, 43, false, null);
        if (blackTag != null)
            ImageAt(optionsRoot, "Cartouche options", blackTag, 284, 39, 141, 49);
        else
            Art(optionsRoot, "Cartouche options", Ink, 284, 58, 310, 40,
                366, 39, 379, 44, 420, 53, 425, 68, 398, 87,
                284, 84);
        Text(optionsRoot, "OPTIONS", 300, 47, 111, 35, 25, White,
            TextAlignmentOptions.Center);

        tabs = new CroustyMenuHover[4];
        string[] labels = { "COMMANDES", "AUDIO", "VIDÉO", "CRÉDITS" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            float y = 116 + 75 * i;
            bool chosen = i == 0;
            DrawOptionsRibbon(labels[i], y, chosen);
            Button b = Hit(optionsRoot, labels[i], 0, y, 312, 67, null);
            Text(b.transform, labels[i], 19, -3, 250, 59, 34,
                chosen ? White : Ink, TextAlignmentOptions.MidlineLeft,
                chosen ? Hex("#855021") : Color.clear, chosen ? 0.11f : 0f, -1);
            tabs[i] = b.gameObject.AddComponent<CroustyMenuHover>();
            tabs[i].highlighted = chosen;
        }
        optionsContent = FullRoot(optionsRoot, "Contenu des options");
        contentGroup = optionsContent.gameObject.AddComponent<CanvasGroup>();
        RectTransform container = optionsContent;
        string[] names = { "Commandes", "Audio", "Vidéo", "Crédits" };
        for (int i = 0; i < names.Length; i++)
        {
            optionsContent = FullRoot(container, names[i]);
            switch (i)
            {
                case 0: BuildCommands(); break;
                case 1: BuildAudio(); break;
                case 2: BuildVideo(); break;
                case 3: BuildCredits(); break;
            }
            optionsContent.gameObject.SetActive(i == 0);
        }
        optionsContent = container;
    }





    private void DrawOptionsRibbon(string name, float y, bool chosen)
    {
        if (creamRibbon != null && orangeRibbon != null)
        {
            Art(optionsRoot, name + " ombre", chosen ? Ink : Color.clear,
                0, y + 7, 307, y + 5, 290, y + 16, 313, y + 24,
                289, y + 35, 312, y + 45, 290, y + 56, 311, y + 67, 0, y + 67);
            ImageAt(optionsRoot, name + " couleur", chosen ? orangeRibbon : creamRibbon,
                0, y + 2, 312, 62);
            return;
        }
        Color fill = chosen ? Orange : Cream;
        Art(optionsRoot, name + " bord", Ink, 0, y + 6, 307, y + 3,
            290, y + 14, 313, y + 22, 289, y + 33, 312, y + 43,
            290, y + 54, 311, y + 65, 0, y + 65);
        Art(optionsRoot, name + " couleur", fill, 0, y + 3, 309, y + 2,
            287, y + 13, 313, y + 20, 286, y + 31, 311, y + 40,
            287, y + 50, 310, y + 60, 0, y + 62);
    }





    private void BuildCommands()
    {
        Section("SOURIS", 354, 102, 81);
        Text(optionsContent, "Sensibilité", 358, 132, 98, 25, 16, Ink,
            TextAlignmentOptions.MidlineLeft);
        float value = 0.75f;
        Slider s = AddSlider(optionsContent, 461, 130, 171, value, 0.2f, 1.5f);
        if (blackTag != null)
            ImageAt(optionsContent, "Valeur souris", blackTag, 666, 132, 65, 22);
        else
            Art(optionsContent, "Valeur souris", Ink,
                669, 137, 687, 132, 715, 132, 731, 142, 715, 153, 669, 153, 666, 147);
        TMP_Text number = Text(optionsContent, "0.75", 674, 133, 50, 20,
            15, White, TextAlignmentOptions.Center);
        Section("CONTRÔLEUR", 354, 163, 96);
        Text(optionsContent, "Contrôleur", 358, 197, 100, 25, 16, Ink,
            TextAlignmentOptions.MidlineLeft);
        if (orangeRibbon != null)
            ImageAt(optionsContent, "Panneau périphérique", orangeRibbon, 461, 199, 171, 17);
        else
            Art(optionsContent, "Panneau périphérique", Orange, 461, 199, 632, 199,
                627, 204, 633, 209, 628, 216, 461, 216);
        Text(optionsContent, "CLAVIER / SOURIS", 465, 198, 163, 18, 14, White,
            TextAlignmentOptions.MidlineLeft);
    }

    private void BuildAudio()
    {
        Section("AUDIO", 354, 102, 81);
        Text(optionsContent, "Volume général", 358, 137, 110, 25, 16, Ink,
            TextAlignmentOptions.MidlineLeft);
        float volume = 1f;
        Slider s = AddSlider(optionsContent, 475, 135, 157, volume, 0f, 1f);
        TMP_Text label = Text(optionsContent, Mathf.RoundToInt(volume * 100f) + "%",
            666, 135, 60, 23, 17, Ink, TextAlignmentOptions.Center);
    }

    private void BuildVideo()
    {
        Section("VIDÉO", 354, 102, 81);
        Text(optionsContent, "Plein écran", 358, 137, 115, 25, 16, Ink,
            TextAlignmentOptions.MidlineLeft);
        Button toggle = Hit(optionsContent, "Basculer plein écran", 476, 132, 180, 30, null);
        Image image = toggle.GetComponent<Image>();
        image.color = Orange;
        TMP_Text label = Text(toggle.transform, Screen.fullScreen ? "ACTIVÉ" : "DÉSACTIVÉ",
            4, 1, 172, 28, 18, White, TextAlignmentOptions.Center);
        toggle.gameObject.AddComponent<CroustyMenuHover>();
    }

    private void BuildCredits()
    {
        Section("CREDITS", 354, 102, 96);
        Text(optionsContent, "CROUSTY EXPRESS", 358, 140, 330, 48, 34, Ink,
            TextAlignmentOptions.MidlineLeft);
        var credits = Resources.Load<TextAsset>("Crousty/Credits");
        var body = Text(optionsContent, credits != null ? credits.text : "Thanks for riding with us!",
            358, 189, 420, 235, 18, Ink, TextAlignmentOptions.TopLeft);
        body.gameObject.AddComponent<CroustyCredits>();
    }

    private void Section(string title, float x, float y, float width)
    {
        if (blackTag != null)
            ImageAt(optionsContent, title + " étiquette", blackTag, x, y, width, 28);
        else
            Art(optionsContent, title + " étiquette", Ink, x, y + 12, x + 13, y,
                x + width - 12, y + 2, x + width, y + 10,
                x + width - 3, y + 23, x + 3, y + 28);
        Text(optionsContent, title, x + 7, y + 3, width - 14, 23,
            title.Length >= 10 ? 14 : 16, White, TextAlignmentOptions.Center);
    }

    private void SmallRibbon(RectTransform root, string label, float x, float y,
        float w, float h, bool orange, UnityEngine.Events.UnityAction action)
    {
        Sprite ribbon = orange ? orangeRibbon : creamRibbon;
        Button b;
        Image background = null;
        if (ribbon != null)
        {
            b = Hit(root, label, x, y, w, h, action);
            background = ImageAt(b.transform, label + " fond", ribbon, 0, 0, w, h);
        }
        else
        {
            Art(root, label + " bord", Ink, x, y + 5, x + w, y + 4,
                x + w - 13, y + 14, x + w, y + 22,
                x + w - 12, y + 30, x + w, y + h, x, y + h);
            Art(root, label + " fond", orange ? Orange : Cream,
                x, y, x + w, y, x + w - 12, y + 9,
                x + w, y + 19, x + w - 14, y + 28,
                x + w, y + h - 4, x, y + h - 4);
            b = Hit(root, label, x, y, w, h, action);
        }
        TMP_Text caption = Text(b.transform, label, 22, 3, w - 44, h - 7, 20,
            orange ? White : Ink, TextAlignmentOptions.Center);
        CroustyMenuHover hover = b.gameObject.AddComponent<CroustyMenuHover>();
        hover.colorTarget = background;
        hover.textTarget = background != null ? caption : null;
        hover.activeImageColor = Orange;
        hover.activeTextColor = White;
    }

    private Slider AddSlider(RectTransform root, float x, float y, float width,
        float value, float min, float max)
    {
        RectTransform baseRect = Rect(root, "Glissière", x, y, width, 24);
        Image hitArea = baseRect.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        Slider slider = baseRect.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        Image rail = Rect(baseRect, "Rail", 0, 11, width, 4).gameObject.AddComponent<Image>();
        rail.color = Ink;
        rail.raycastTarget = false;
        RectTransform sliding = Rect(baseRect, "Zone mobile", 0, 0, width, 24);
        RectTransform handle = Rect(sliding, "Curseur", 0, 5, 14, 14);
        CroustyVectorGraphic circle = handle.gameObject.AddComponent<CroustyVectorGraphic>();
        circle.color = Orange;
        circle.raycastTarget = false;
        circle.points = Circle(20);
        circle.SetVerticesDirty();
        slider.handleRect = handle;
        slider.targetGraphic = circle;
        handle.anchorMin = new Vector2(handle.anchorMin.x, 0.5f);
        handle.anchorMax = new Vector2(handle.anchorMax.x, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(14f, 14f);
        handle.anchoredPosition = Vector2.zero;
        slider.value = value;
        handle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 14f);
        return slider;
    }

    private static Vector2[] Circle(int sides)
    {
        Vector2[] pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            pts[i] = new Vector2(0.5f + 0.5f * Mathf.Cos(a),
                0.5f + 0.5f * Mathf.Sin(a));
        }
        return pts;
    }









    private static RectTransform FullRoot(Transform parent, string name)
    {
        RectTransform r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return r;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y,
        float w, float h)
    {
        RectTransform r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    private static void Solid(Transform parent, string name, Color color)
    {
        Image image = FullRoot(parent, name).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static CroustyVectorGraphic Art(Transform parent, string name, Color color,
        params float[] xy)
    {
        RectTransform r = Rect(parent, name, 0, 0, 848, 480);
        CroustyVectorGraphic g = r.gameObject.AddComponent<CroustyVectorGraphic>();
        g.color = color;
        g.raycastTarget = false;
        Vector2[] p = new Vector2[xy.Length / 2];
        for (int i = 0; i < p.Length; i++)
            p[i] = new Vector2(xy[i * 2] / 848f, 1f - xy[i * 2 + 1] / 480f);
        g.points = p;
        g.SetVerticesDirty();
        return g;
    }

    private static Image ImageAt(Transform parent, string name, Sprite sprite,
        float x, float y, float w, float h)
    {
        Image image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Text(Transform parent, string content, float x, float y,
        float w, float h, float size, Color color, TextAlignmentOptions alignment,
        Color outline = default, float outlineWidth = 0f, float rotation = 0f)
    {
        RectTransform r = Rect(parent, content + " texte", x, y, w, h);
        r.localEulerAngles = new Vector3(0, 0, rotation);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = content;
        // Retro Blendy has a larger visual cap height than the placeholder font.
        t.fontSize = menuFont != null ? size * 0.8f : size;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.alignment = alignment;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (outlineWidth > 0f)
        {
            t.outlineColor = outline;
            t.outlineWidth = outlineWidth;
        }
        return t;
    }

    private static Button Hit(Transform parent, string name, float x, float y,
        float w, float h, UnityEngine.Events.UnityAction action)
    {
        RectTransform r = Rect(parent, name, x, y, w, h);
        Image i = r.gameObject.AddComponent<Image>();
        i.color = new Color(1, 1, 1, 0.001f);
        Button b = r.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        if (action != null) b.onClick.AddListener(action);
        return b;
    }

    private static Color Hex(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color c);
        return c;
    }
}
