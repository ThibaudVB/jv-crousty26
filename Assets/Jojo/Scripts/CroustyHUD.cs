using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// HUD de la partie, d'après la maquette 1920x1080 :
// argent et commandes en haut à gauche, objectif en haut à droite,
// gérant en bas à gauche, mini map en bas à droite.
// Les images sont chargées depuis Resources si les champs sont vides.
public class CroustyHUD : MonoBehaviour
{
    public DeliveryManager manager;

    [Header("Images (auto depuis Resources si vides)")]
    [Tooltip("Bandeau papier de l'objectif, aussi utilisé en sombre derrière l'argent")]
    public Sprite banner;
    [Tooltip("Forme sombre derrière les commandes")]
    public Sprite tag;
    public Sprite moneyIcon;
    public Sprite ordersIcon;
    public Sprite objectiveIcon;

    [Header("Texte")]
    [Tooltip("Police du HUD. Si vide, police par défaut de Unity.")]
    public Font font;
    public Color darkColor = new Color32(0x26, 0x26, 0x31, 0xFF);
    public Color lightColor = new Color32(0xEC, 0xE7, 0xDE, 0xFF);

    private Text moneyText;
    private Text ordersText;
    private Text objectiveText;
    private GerantDialogue gerant;

    // Milliers séparés par un point : 6.767
    private static readonly NumberFormatInfo MoneyFormat =
        new NumberFormatInfo { NumberGroupSeparator = "." };

    private void Start()
    {
        if (manager == null)
            manager = DeliveryManager.Instance != null
                ? DeliveryManager.Instance
                : FindAnyObjectByType<DeliveryManager>();

        BuildUI();
    }

    private void Update()
    {
        if (manager == null)
            return;

        moneyText.text = manager.Score.ToString("#,0", MoneyFormat);

        ordersText.text = manager.HasStarted
            ? $"{manager.Delivered}/{manager.OrdersTotal}"
            : $"0/{manager.NextRunOrders}";

        objectiveText.text = manager.NeedsPickup
            ? "Récupère les commandes au restaurant de Crousty (0/1)"
            : $"Livre les crousty aux adresses indiquées sur la carte ({manager.Delivered}/{manager.OrdersTotal})";

        gerant.SetMessage(GetMessage());
    }

    // Répliques du gérant
    private string GetMessage()
    {
        if (!manager.HasStarted)
            return "Hop hop hop ! Les commandes attendent, on se bouge !";

        if (manager.AllDelivered)
            return "Bien joué ! Reviens au magasin, y'a d'autres commandes !";

        if (manager.CroustyLeft > 0)
            return manager.CroustyLeft == 1
                ? "Plus qu'une commande, on lâche rien !"
                : $"Encore {manager.CroustyLeft} commandes, les clients ont faim !";

        return "";
    }

    // ---------- CREATION DE L'UI ----------

    private void BuildUI()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (banner == null) banner = Resources.Load<Sprite>("Gerant/bandeau");
        if (tag == null) tag = Resources.Load<Sprite>("Gerant/etiquette");
        if (moneyIcon == null) moneyIcon = Resources.Load<Sprite>("HUD/dollar");
        if (ordersIcon == null) ordersIcon = Resources.Load<Sprite>("HUD/sac");
        if (objectiveIcon == null) objectiveIcon = Resources.Load<Sprite>("HUD/epingle");

        GameObject canvasObject = new GameObject(
            "Crousty HUD",
            typeof(Canvas),
            typeof(CanvasScaler)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Positions en pixels depuis le coin haut-gauche de chaque bloc, d'après la maquette

        // Argent (points) : le bandeau papier, en sombre
        RectTransform money = CreatePanel(canvas.transform, "Argent",
            new Vector2(0f, 1f), new Vector2(48f, -57f), new Vector2(327f, 115f));
        CreateImage(money, "Fond", banner, darkColor, new Rect(0f, 0f, 327f, 115f), false);
        CreateImage(money, "Icone", moneyIcon, Color.white, new Rect(14f, 5f, 64f, 95f), true);
        moneyText = CreateText(money, "Valeur", new Rect(100f, 0f, 220f, 115f), 64, lightColor);

        // Commandes livrées / à livrer
        RectTransform orders = CreatePanel(canvas.transform, "Commandes",
            new Vector2(0f, 1f), new Vector2(48f, -192f), new Vector2(302f, 114f));
        CreateImage(orders, "Fond", tag, Color.white, new Rect(0f, 6f, 302f, 108f), false);
        CreateImage(orders, "Icone", ordersIcon, Color.white, new Rect(34f, 0f, 58f, 70f), true);
        ordersText = CreateText(orders, "Valeur", new Rect(112f, 25f, 180f, 64f), 52, lightColor);

        // Objectif
        RectTransform objective = CreatePanel(canvas.transform, "Objectif",
            Vector2.one, new Vector2(-55f, -57f), new Vector2(555f, 239f));
        CreateImage(objective, "Fond", banner, Color.white, new Rect(0f, 0f, 555f, 239f), false);
        CreateImage(objective, "Icone", objectiveIcon, Color.white, new Rect(33f, 23f, 49f, 62f), true);
        CreateText(objective, "Titre", new Rect(98f, 18f, 440f, 74f), 62, darkColor).text = "OBJECTIF";
        objectiveText = CreateText(objective, "Texte", new Rect(42f, 118f, 470f, 100f), 30, darkColor);
        objectiveText.alignment = TextAnchor.UpperLeft;
        objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Gérant en bas à gauche
        gerant = GetComponent<GerantDialogue>();
        if (gerant == null)
            gerant = gameObject.AddComponent<GerantDialogue>();
        gerant.Build(canvas.transform);

        // Mini map en bas à droite
        MiniMap miniMap = GetComponent<MiniMap>();
        if (miniMap == null)
            miniMap = gameObject.AddComponent<MiniMap>();
        miniMap.Build(canvas.transform);
    }

    // Bloc accroché à un coin de l'écran
    private static RectTransform CreatePanel(Transform parent, string name,
        Vector2 corner, Vector2 offset, Vector2 size)
    {
        RectTransform panel = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(parent, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = corner;
        panel.anchoredPosition = offset;
        panel.sizeDelta = size;
        return panel;
    }

    private static void CreateImage(RectTransform panel, string name, Sprite sprite, Color color,
        Rect area, bool preserveAspect)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panel, false);
        Place(imageObject.GetComponent<RectTransform>(), area);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;

        // Image pas encore ajoutée au projet : on la cache
        if (sprite == null)
            imageObject.SetActive(false);
    }

    private Text CreateText(RectTransform panel, string name, Rect area, int size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel, false);
        Place(textObject.GetComponent<RectTransform>(), area);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = font.name == "LegacyRuntime" ? FontStyle.Bold : FontStyle.Normal;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    // Place un élément dans son bloc à partir du coin haut-gauche
    private static void Place(RectTransform rect, Rect area)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(area.x, -area.y);
        rect.sizeDelta = area.size;
    }
}
