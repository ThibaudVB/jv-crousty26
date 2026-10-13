using UnityEngine;
using UnityEngine.UI;

// Boîte de dialogue du gérant en bas à gauche de l'écran :
// portrait + étiquette "GÉRANT" + bandeau papier avec le texte qui s'écrit lettre par lettre.
// Le gérant commente la tournée en lisant le DeliveryManager, et râle quand le bus se cogne.
//
// A placer à la main sur un GameObject vide de la scène (ex : "Gerant").
// Glisser les images du dossier Jojo/Resources/Gerant dans les champs "Images".
// La boîte crée son propre Canvas au lancement.
public class GerantDialogue : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Si vide : le DeliveryManager de la scène")]
    public DeliveryManager manager;
    [Tooltip("Optionnel : le BusDamage du bus, pour que le gérant réagisse aux chocs")]
    public BusDamage busDamage;

    [Header("Images (portrait, bandeau, etiquette, titre)")]
    public Sprite portrait;
    [Tooltip("Bandeau papier derrière le texte")]
    public Sprite banner;
    [Tooltip("Forme sombre derrière le titre (Vector 1)")]
    public Sprite nameTag;
    [Tooltip("Titre GÉRANT en image. Si vide, il est écrit avec la police.")]
    public Sprite nameTitle;

    [Header("Répliques ({0} = nombre ou points)")]
    public string startLine = "Hop hop hop! The orders are waiting, get moving!";
    public string ordersLeftLine = "{0} orders left, the customers are hungry!";
    public string lastOrderLine = "Just one order left, don't give up!";
    [Tooltip("Quand il reste peu de temps")]
    public string hurryLine = "Hurry up, the clock is ticking!";
    [Tooltip("{0} = points gagnés pendant la tournée")]
    public string runCompleteLine = "Nice job! {0} points! Come back to the shop for more orders!";
    [Tooltip("Temps écoulé ou arrêté par la police")]
    public string failedLine = "What a disaster... The customers are still waiting!";
    [Tooltip("Après un choc contre un mur, une voiture ou une grosse chute")]
    public string crashLine = "Hey! Watch my car!";
    [Tooltip("Durée d'affichage d'une réplique de choc")]
    public float crashLineSeconds = 3f;
    [Tooltip("Secondes restantes à partir desquelles le gérant presse le joueur")]
    public float hurrySeconds = 15f;

    [Header("Texte")]
    public string speakerName = "GÉRANT";
    [Tooltip("Police du dialogue. Si vide, police par défaut de Unity.")]
    public Font font;
    public int fontSize = 30;
    public Color textColor = new Color32(0x26, 0x26, 0x31, 0xFF);
    [Tooltip("Lettres par seconde. 0 = texte affiché d'un coup.")]
    public float lettersPerSecond = 45f;
    [Tooltip("Hauteur du petit sautillement du triangle \"suite\"")]
    public float arrowBounce = 5f;

    [Header("Position (depuis le coin bas-gauche)")]
    public float scale = 1f;
    public Vector2 screenOffset = new Vector2(10f, 10f);
    [Tooltip("Décale le bandeau, l'étiquette et le texte vers la droite (pas le portrait)")]
    public float bannerShift = 50f;
    [Tooltip("Au-dessus du HUD (100) et de la mini map (110), sous les menus (600)")]
    public int sortingOrder = 105;

    [Header("Couleurs si pas d'image")]
    public Color bannerColor = new Color32(0xEC, 0xE7, 0xDE, 0xFF);
    public Color tagColor = new Color32(0x26, 0x26, 0x31, 0xFF);

    private Text messageText;
    private RectTransform arrow;
    private string currentMessage = "";
    private float shownLetters;         // nombre de lettres déjà écrites
    private float crashMessageUntil;    // la réplique de choc reste affichée jusqu'à ce moment

    // Taille de la boîte, d'après la maquette
    private static readonly Vector2 BoxSize = new Vector2(740f, 410f);

    private void Start()
    {
        if (manager == null)
            manager = DeliveryManager.Instance;

        // On s'abonne à l'événement : OnDamaged sera appelé à chaque choc du bus
        if (busDamage != null)
            busDamage.Damaged += OnDamaged;

        Build();
    }

    private void OnDestroy()
    {
        if (busDamage != null)
            busDamage.Damaged -= OnDamaged;
    }

    private void OnDamaged()
    {
        crashMessageUntil = Time.time + crashLineSeconds;
    }

    // Crée tous les éléments de la boîte
    private void Build()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas à part pour ne pas toucher au HUD enregistré dans la scène
        GameObject canvasObject = new GameObject("Canvas Gerant", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject root = new GameObject("Dialogue Gerant", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = BoxSize + new Vector2(bannerShift, 0f);
        rect.anchoredPosition = screenOffset;
        rect.localScale = Vector3.one * scale;

        // Positions en pixels depuis le coin bas-gauche, d'après la maquette.
        // Tout sauf le portrait est décalé de bannerShift vers la droite.
        // Ordre d'affichage : bandeau, étiquette, gérant devant, puis titre par-dessus tout.
        float x = bannerShift;

        CreateImage(root.transform, "Bandeau", banner, bannerColor,
            new Rect(x + 15f, 0f, 705f, 165f), banner != null);

        CreateImage(root.transform, "Etiquette", nameTag, tagColor,
            new Rect(x + 148f, 152f, 262f, 90f), nameTag != null);

        CreateImage(root.transform, "Portrait", portrait, Color.clear,
            new Rect(-10f, 2f, 326f, 370f), true);

        if (nameTitle != null)
        {
            CreateImage(root.transform, "Titre", nameTitle, Color.white,
                new Rect(x + 182f, 182f, 158f, 42f), true);
        }
        else
        {
            Text title = CreateText(root.transform, "Titre",
                new Rect(x + 178f, 174f, 220f, 60f), fontSize + 8, TextAnchor.MiddleLeft);
            title.color = bannerColor;
            title.text = speakerName;
        }

        messageText = CreateText(root.transform, "Message",
            new Rect(x + 228f, 35f, 460f, 110f), fontSize, TextAnchor.MiddleLeft);
        messageText.color = textColor;

        // Petit triangle "suite" en bas à droite
        Image arrowImage = CreateImage(root.transform, "Suite", JojoVisuals.ArrowSprite, textColor,
            new Rect(x + 645f, 28f, 24f, 20f), false);
        arrow = arrowImage.rectTransform;
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
    }

    // Affiche une nouvelle réplique (elle s'écrit lettre par lettre)
    public void SetMessage(string message)
    {
        if (messageText == null || message == null || message == currentMessage)
            return;

        currentMessage = message;
        shownLetters = lettersPerSecond > 0f ? 0f : message.Length;
    }

    // Réplique selon l'état de la tournée. null = on garde la réplique actuelle.
    private string GetMessage()
    {
        if (manager.HasFailed)
            return failedLine;

        if (Time.time < crashMessageUntil)
            return crashLine;

        if (!manager.HasStarted)
            return startLine;

        if (manager.AllDelivered)
            return string.Format(runCompleteLine, manager.LastRunScore);

        if (manager.RunActive && manager.RunRemaining <= hurrySeconds)
            return hurryLine;

        if (manager.CroustyLeft == 1)
            return lastOrderLine;

        if (manager.CroustyLeft > 1)
            return string.Format(ordersLeftLine, manager.CroustyLeft);

        return null;
    }

    private void Update()
    {
        if (messageText == null)
            return;

        if (manager != null)
            SetMessage(GetMessage());

        // Effet machine à écrire (temps réel : continue pendant le game over, qui met le jeu en pause)
        if (shownLetters < currentMessage.Length)
            shownLetters += lettersPerSecond * Time.unscaledDeltaTime;

        int count = Mathf.Min(currentMessage.Length, Mathf.FloorToInt(shownLetters));
        messageText.text = currentMessage.Substring(0, count);

        // Le triangle n'apparaît (et sautille) qu'une fois le texte complet
        bool finished = count >= currentMessage.Length;
        arrow.gameObject.SetActive(finished && currentMessage.Length > 0);
        arrow.anchoredPosition = new Vector2(arrow.anchoredPosition.x,
            38f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * arrowBounce);
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color fallbackColor,
        Rect area, bool preserveAspect)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Place(imageObject.GetComponent<RectTransform>(), area);

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        image.color = sprite != null && sprite != JojoVisuals.ArrowSprite ? Color.white : fallbackColor;
        image.preserveAspect = preserveAspect;

        if (sprite == null && fallbackColor.a <= 0f)
            imageObject.SetActive(false);

        return image;
    }

    private Text CreateText(Transform parent, string name, Rect area, int size, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Place(textObject.GetComponent<RectTransform>(), area);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = font.name == "LegacyRuntime" ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.05f;
        text.raycastTarget = false;
        return text;
    }

    // Place un élément dans la boîte à partir du coin bas-gauche
    private static void Place(RectTransform rect, Rect area)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = area.position;
        rect.sizeDelta = area.size;
    }
}
