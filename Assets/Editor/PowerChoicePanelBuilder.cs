using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PowerChoicePanelBuilder
{
    private static readonly Color Ink = new Color32(38, 38, 50, 255);
    private static TMP_FontAsset font;
    private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    [MenuItem("Tools/Crousty/Installer le panel de choix des pouvoirs")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        var bus = UnityEngine.Object.FindAnyObjectByType<BusController>();
        if (bus.GetComponent<PowerUnlockProgression>() != null) return;
        var progression = bus.gameObject.AddComponent<PowerUnlockProgression>();
        progression.deliveries = UnityEngine.Object.FindAnyObjectByType<DeliveryManager>();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
        progression.gpsSprite = Sprite("Assets/UI/Bonuses/BONUS_GPS.png");
        progression.riceSprite = Sprite("Assets/UI/Bonuses/BONUS_RIZ.png");
        progression.flightSprite = Sprite("Assets/UI/Bonuses/BONUS_VOL.png");
        progression.creamPaper = Sprite("Assets/Menu/RibbonCream.png");
        progression.orangePaper = Sprite("Assets/Menu/RibbonOrange.png");
        const string materialPath = "Assets/UI/Bonuses/PowerChoiceBlur.mat";
        progression.blurMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (progression.blurMaterial == null)
        {
            progression.blurMaterial = new Material(Shader.Find("Crousty/PowerChoiceBlur"));
            AssetDatabase.CreateAsset(progression.blurMaterial, materialPath);
        }

        var backdrop = Canvas("Power choice - blurred world", 90);
        progression.backdrop = backdrop.gameObject;
        var world = Rect(backdrop, "World capture", Vector2.zero, Vector2.zero);
        Stretch(world);
        progression.blurredWorld = world.gameObject.AddComponent<RawImage>();
        progression.blurredWorld.raycastTarget = false;
        var shade = Picture(backdrop, "Dim", null, Vector2.zero, Vector2.zero);
        Stretch(shade.rectTransform);
        shade.color = new Color(.09f, .08f, .12f, .48f);
        backdrop.gameObject.SetActive(false);

        var canvas = Canvas("Power choice Canvas", 200);
        var panel = Rect(canvas, "Panel - Choose a power", Vector2.zero, Vector2.zero);
        Stretch(panel);
        // Blocks clicks through the modal without hiding the existing HUD.
        panel.gameObject.AddComponent<Image>().color = Color.clear;
        progression.choicePanel = panel.gameObject;
        var content = Rect(panel, "Choice content", new Vector2(0, 20), new Vector2(680, 760));
        Picture(content, "Manager paper", Sprite("Assets/Resources/HUDControls/ManagerCard.png"), new Vector2(70, 125), new Vector2(490, 220));
        Picture(content, "Manager portrait", Sprite("Assets/Jojo/Resources/Gerant/portrait.png"), new Vector2(-220, 195), new Vector2(210, 310));
        Label(content, "Manager", "Manager", new Vector2(0, 168), new Vector2(200, 60), 34, Color.white);
        progression.message = Label(content, "Message", "Congratulations! Choose the\nnext power for the boxes!", new Vector2(94, 85), new Vector2(425, 90), 24, Ink);

        progression.choices = new Button[2]; progression.artwork = new Image[2];
        progression.titles = new TMP_Text[2]; progression.prices = new TMP_Text[2];
        progression.pricePaper = new Image[2]; progression.confirmHints = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            var card = Rect(content, "Choice " + (i + 1), new Vector2(i == 0 ? -155 : 155, -180), new Vector2(285, 355));
            var hit = card.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            progression.choices[i] = button;
            progression.artwork[i] = Picture(card, "Power artwork", i == 0 ? progression.gpsSprite : progression.riceSprite, new Vector2(0, 50), new Vector2(280, 255));
            progression.titles[i] = Label(card, "Power name", i == 0 ? "Express GPS" : "Asian Rice", new Vector2(0, -42), new Vector2(260, 48), 29, Color.white);
            progression.pricePaper[i] = Picture(card, "Price paper", progression.creamPaper, new Vector2(0, -145), new Vector2(220, 80));
            progression.pricePaper[i].preserveAspect = false;
            progression.prices[i] = Label(card, "Price", "$130", new Vector2(0, -145), new Vector2(190, 64), 38, Ink);
            var hint = Rect(card, "Confirm hint", new Vector2(109, -145), new Vector2(52, 52));
            Circle(hint, 52, new Color32(143, 255, 77, 255));
            Circle(hint, 44, Ink);
            Label(hint, "A", "A", Vector2.zero, new Vector2(42, 42), 29, new Color32(143, 255, 77, 255));
            progression.confirmHints[i] = hint.gameObject;
        }
        for (int i = 0; i < 2; i++)
            progression.choices[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = progression.choices[1-i], selectOnRight = progression.choices[1-i] };
        progression.balance = Label(content, "Available money", "", new Vector2(0, -460), new Vector2(400, 40), 24, Color.white);
        progression.balance.gameObject.SetActive(false); // The existing money HUD stays visible above the blur.
        var next = Rect(content, "Continue when funds are insufficient", new Vector2(0, -425), new Vector2(300, 70));
        var nextImage = next.gameObject.AddComponent<Image>(); nextImage.sprite = progression.creamPaper;
        progression.continueButton = next.gameObject.AddComponent<Button>(); progression.continueButton.targetGraphic = nextImage;
        Label(next, "Label", "Continue", Vector2.zero, new Vector2(280, 65), 30, Ink);
        next.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("POWER_CHOICE_PANEL_INSTALLED");
    }

    private static RectTransform Canvas(string name, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        return (RectTransform)root.transform;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Circle(Transform parent, float size, Color color)
    {
        var graphic = Rect(parent, "Circle", Vector2.zero, new Vector2(size, size)).gameObject.AddComponent<CroustyVectorGraphic>();
        graphic.points = Enumerable.Range(0, 40).Select(i => new Vector2(.5f + .5f * Mathf.Cos(i * Mathf.PI / 20), .5f + .5f * Mathf.Sin(i * Mathf.PI / 20))).ToArray();
        graphic.color = color; graphic.raycastTarget = false;
    }
    private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
    }
    private static TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = fontSize - 4; label.fontSizeMax = fontSize;
        return label;
    }
}
