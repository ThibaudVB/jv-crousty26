using UnityEngine;
using UnityEngine.UI;

// Affiche le nombre de crousty restants et un message d'aide.
// Si aucun Text n'est assigné, ils sont créés automatiquement.
public class CroustyHUD : MonoBehaviour
{
    public DeliveryManager manager;

    [Header("Textes (optionnels)")]
    public Text counterText;
    public Text messageText;

    public int fontSize = 32;

    private GerantDialogue gerant;

    private void Start()
    {
        if (manager == null)
            manager = DeliveryManager.Instance != null
                ? DeliveryManager.Instance
                : FindAnyObjectByType<DeliveryManager>();

        BuildDefaultUI();
    }

    private void Update()
    {
        if (manager == null)
            return;

        counterText.text = manager.HasStarted
            ? $"Crousty : {manager.CroustyLeft} / {manager.OrdersTotal}"
            : "Crousty : 0";

        string message = GetMessage();
        if (gerant != null)
            gerant.SetMessage(message);
        else
            messageText.text = message;
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

    private void BuildDefaultUI()
    {
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

        if (counterText == null)
            counterText = CreateText(
                canvas.transform, "Compteur",
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                new Vector2(0f, 1f), new Vector2(30f, -30f),
                TextAnchor.UpperLeft, fontSize
            );

        // Sans texte assigné, les messages passent par la boîte du gérant
        if (messageText == null)
        {
            gerant = GetComponent<GerantDialogue>();
            if (gerant == null)
                gerant = gameObject.AddComponent<GerantDialogue>();

            gerant.Build(canvas.transform);
        }
    }

    private static Text CreateText(
        Transform parent, string name, Font font,
        Vector2 anchor, Vector2 offset, TextAnchor alignment, int size)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Text),
            typeof(Outline)
        );
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(1200f, 80f);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }
}
