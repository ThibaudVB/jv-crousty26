using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Affiche le nombre de crousty restants, un message d'aide,
// et des repères à l'écran (visibles à travers les bâtiments) vers les objectifs.
// Si aucun Text n'est assigné, ils sont créés automatiquement.
public class CroustyHUD : MonoBehaviour
{
    public DeliveryManager manager;

    [Header("Textes (optionnels)")]
    public Text counterText;
    public Text messageText;

    public int fontSize = 32;

    [Header("Repères à l'écran")]
    public bool showWaypoints = true;
    public float waypointSize = 44f;
    public float screenEdgeMargin = 60f;
    public float waypointHeightOffset = 4f;

    private Canvas canvas;
    private Font font;
    private GerantDialogue gerant;
    private readonly List<Waypoint> waypoints = new List<Waypoint>();

    private class Waypoint
    {
        public RectTransform root;
        public Image icon;
        public RectTransform arrow;
        public Text label;
    }

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

        UpdateWaypoints();
    }

    // Répliques du gérant
    private string GetMessage()
    {
        string key = manager.deliverKey.ToString().ToUpper();

        if (!manager.HasStarted)
            return "Hop hop hop ! Les commandes attendent, on se bouge !";

        if (manager.AllDelivered)
            return "Bien joué ! Reviens au magasin, y'a d'autres commandes !";

        if (manager.PointInRange != null && !manager.autoDeliver)
            return $"Appuie sur {key} pour lancer le crousty !";

        if (manager.CroustyLeft > 0)
            return manager.CroustyLeft == 1
                ? "Plus qu'une commande, on lâche rien !"
                : $"Encore {manager.CroustyLeft} commandes, les clients ont faim !";

        return "";
    }

    // ---------- REPERES ----------

    private void UpdateWaypoints()
    {
        int used = 0;
        Camera cam = Camera.main;

        if (showWaypoints && cam != null && manager.player != null)
        {
            // Magasin : seulement quand il faut y aller
            if (manager.shop != null && (!manager.HasStarted || manager.AllDelivered))
            {
                ShowWaypoint(used++, cam, manager.shop.transform.position,
                    manager.shop.markerColor, manager.shop.screenLabel);
            }

            // Adresses avec une commande en attente
            foreach (DeliveryPoint point in manager.deliveryPoints)
            {
                if (point != null && point.IsWaiting)
                    ShowWaypoint(used++, cam, point.transform.position, point.markerColor, null);
            }
        }

        // Cache les repères en trop
        for (int i = used; i < waypoints.Count; i++)
            waypoints[i].root.gameObject.SetActive(false);
    }

    private void ShowWaypoint(int index, Camera cam, Vector3 worldPosition, Color color, string title)
    {
        while (waypoints.Count <= index)
            waypoints.Add(CreateWaypoint());

        Waypoint waypoint = waypoints[index];
        waypoint.root.gameObject.SetActive(true);

        Vector3 screen = cam.WorldToScreenPoint(worldPosition + Vector3.up * waypointHeightOffset);
        bool behind = screen.z < 0f;

        float margin = screenEdgeMargin * canvas.scaleFactor;
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 position = new Vector2(screen.x, screen.y);

        // Derrière la caméra, le point est projeté en miroir : on le retourne
        if (behind)
            position = center - (position - center);

        bool onScreen =
            !behind &&
            position.x > margin && position.x < Screen.width - margin &&
            position.y > margin && position.y < Screen.height - margin;

        if (!onScreen)
        {
            // Colle le repère au bord de l'écran, dans la direction de la cible
            Vector2 direction = position - center;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.down;

            Vector2 halfSize = center - Vector2.one * margin;
            float scale = Mathf.Min(
                halfSize.x / Mathf.Max(Mathf.Abs(direction.x), 0.001f),
                halfSize.y / Mathf.Max(Mathf.Abs(direction.y), 0.001f)
            );
            position = center + direction * scale;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            waypoint.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            waypoint.arrow.anchoredPosition =
                (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector3.up) * waypointSize * 0.85f;
        }

        waypoint.arrow.gameObject.SetActive(!onScreen);
        waypoint.root.position = position;

        waypoint.icon.color = color;
        waypoint.arrow.GetComponent<Image>().color = color;

        float distance = Vector3.Distance(manager.player.position, worldPosition);
        waypoint.label.text = string.IsNullOrEmpty(title)
            ? $"{distance:0} m"
            : $"{title}\n{distance:0} m";
    }

    private Waypoint CreateWaypoint()
    {
        Waypoint waypoint = new Waypoint();

        GameObject root = new GameObject("Repere", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        waypoint.root = root.GetComponent<RectTransform>();
        waypoint.root.sizeDelta = Vector2.one * waypointSize;

        // Rond de couleur
        GameObject icon = new GameObject("Icone", typeof(RectTransform), typeof(Image), typeof(Outline));
        icon.transform.SetParent(root.transform, false);
        waypoint.icon = icon.GetComponent<Image>();
        waypoint.icon.sprite = JojoVisuals.CircleSprite;
        waypoint.icon.raycastTarget = false;
        icon.GetComponent<RectTransform>().sizeDelta = Vector2.one * waypointSize;
        icon.GetComponent<Outline>().effectColor = Color.black;

        // Flèche quand la cible est hors de l'écran
        GameObject arrow = new GameObject("Fleche", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(root.transform, false);
        Image arrowImage = arrow.GetComponent<Image>();
        arrowImage.sprite = JojoVisuals.ArrowSprite;
        arrowImage.raycastTarget = false;
        waypoint.arrow = arrow.GetComponent<RectTransform>();
        waypoint.arrow.sizeDelta = Vector2.one * waypointSize * 0.6f;

        // Distance
        waypoint.label = CreateText(root.transform, "Distance", font,
            new Vector2(0.5f, 0f), new Vector2(0f, -8f), TextAnchor.UpperCenter, fontSize - 10);
        waypoint.label.rectTransform.pivot = new Vector2(0.5f, 1f);
        waypoint.label.rectTransform.sizeDelta = new Vector2(300f, 80f);

        return waypoint;
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

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (counterText == null)
            counterText = CreateText(
                canvas.transform, "Compteur", font,
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
