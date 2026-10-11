// DÉSACTIVÉ avec le HUD de Jojo (CroustyHUDJojo.cs) : compilé seulement si CROUSTY_HUD_JOJO est défini.
#if CROUSTY_HUD_JOJO
using UnityEngine;
using UnityEngine.UI;

// Boîte de dialogue du gérant en bas à gauche de l'écran :
// portrait + étiquette "GÉRANT" + bandeau papier avec le texte qui s'écrit.
// Les images sont chargées depuis Resources/Gerant si les champs sont vides.
public class GerantDialogue : MonoBehaviour
{
    [Header("Images (auto depuis Resources/Gerant si vides)")]
    public Sprite portrait;
    [Tooltip("Bandeau papier derrière le texte")]
    public Sprite banner;
    [Tooltip("Forme sombre derrière le titre (Vector 1)")]
    public Sprite nameTag;
    [Tooltip("Titre GÉRANT en image. Si vide, il est écrit avec la police.")]
    public Sprite nameTitle;

    [Header("Texte")]
    public string speakerName = "GÉRANT";
    [Tooltip("Police du dialogue. Si vide, police par défaut de Unity.")]
    public Font font;
    public int fontSize = 30;
    public Color textColor = new Color32(0x26, 0x26, 0x31, 0xFF);
    [Tooltip("Lettres par seconde. 0 = texte affiché d'un coup.")]
    public float lettersPerSecond = 45f;

    [Header("Position (depuis le coin bas-gauche)")]
    public float scale = 1f;
    public Vector2 screenOffset = new Vector2(10f, 10f);
    [Tooltip("Décale le bandeau, l'étiquette et le texte vers la droite (pas le portrait)")]
    public float bannerShift = 50f;

    [Header("Couleurs si pas d'image")]
    public Color bannerColor = new Color32(0xEC, 0xE7, 0xDE, 0xFF);
    public Color tagColor = new Color32(0x26, 0x26, 0x31, 0xFF);

    private Text messageText;
    private RectTransform arrow;
    private string currentMessage = "";
    private float shownLetters;

    // Taille de la boîte, d'après la maquette
    private static readonly Vector2 BoxSize = new Vector2(740f, 410f);

    public void Build(Transform canvas)
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (portrait == null) portrait = Resources.Load<Sprite>("Gerant/portrait");
        if (banner == null) banner = Resources.Load<Sprite>("Gerant/bandeau");
        if (nameTag == null) nameTag = Resources.Load<Sprite>("Gerant/etiquette");
        if (nameTitle == null) nameTitle = Resources.Load<Sprite>("Gerant/titre");

        GameObject root = new GameObject("Dialogue Gerant", typeof(RectTransform));
        root.transform.SetParent(canvas, false);

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

    public void SetMessage(string message)
    {
        if (messageText == null || message == currentMessage)
            return;

        currentMessage = message;
        shownLetters = lettersPerSecond > 0f ? 0f : message.Length;
    }

    private void Update()
    {
        if (messageText == null)
            return;

        // Effet machine à écrire
        if (shownLetters < currentMessage.Length)
            shownLetters += lettersPerSecond * Time.deltaTime;

        int count = Mathf.Min(currentMessage.Length, Mathf.FloorToInt(shownLetters));
        messageText.text = currentMessage.Substring(0, count);

        // Le triangle n'apparaît (et sautille) qu'une fois le texte complet
        bool finished = count >= currentMessage.Length;
        arrow.gameObject.SetActive(finished && currentMessage.Length > 0);
        arrow.anchoredPosition = new Vector2(arrow.anchoredPosition.x,
            38f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 5f);
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
#endif
