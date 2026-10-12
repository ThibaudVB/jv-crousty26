using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Affiche le nombre de crousty restants,
// et des repères à l'écran (visibles à travers les bâtiments) vers les objectifs.
// Les textes et le modèle de repère sont placés dans la scène.
public class CroustyHUD : MonoBehaviour
{
    public DeliveryManager manager;

    [Header("Panels placés dans le Canvas")]
    [SerializeField] private TMP_Text deliveryValue;
    [SerializeField] private TMP_Text objectiveValue;
    [SerializeField] private TMP_Text speedValue;
    [SerializeField] private Image speedFill;
    [SerializeField] private TMP_Text timeValue;
    [SerializeField] private TMP_Text timerValue;
    [SerializeField] private TMP_Text scoreValue;
    [SerializeField] private TMP_Text scoreGain;

    public int fontSize = 32;

    [Header("Repères à l'écran")]
    public bool showWaypoints = true;
    public float waypointSize = 44f;
    public float screenEdgeMargin = 60f;
    public float waypointHeightOffset = 4f;

    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform waypointTemplate;
    private Font font;
    private Rigidbody busBody;
    private Vector3 scoreBaseScale;
    private Vector2 gainBasePosition;
    private float shownScore, scoreVelocity, scorePulse = 10;
    private int pendingGain, displayedScore = -1, displayedSeconds = -1;
    private int displayedRunState = -1;
    private Color timerColor;
    private int displayedMinute = -1;
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

        if (canvas == null || waypointTemplate == null || deliveryValue == null ||
            objectiveValue == null || speedValue == null || timeValue == null ||
            timerValue == null || scoreValue == null || scoreGain == null)
        {
            Debug.LogError("Assigner les panels du HUD dans l’Inspector.", this);
            enabled = false;
            return;
        }
        FindBusBody();
        UpdateClock();
        PrepareRunHUD();
    }

    private void Update()
    {
        if (manager == null)
            return;
        UpdateClock();
        UpdateRunHUD();

        int total = manager.HasStarted ? manager.OrdersTotal : manager.NextRunOrders;
        int done = manager.HasStarted ? manager.Delivered : 0;
        if (deliveryValue != null) deliveryValue.text = $"{done}/{total}";
        if (objectiveValue != null)
        {
            int remaining = Mathf.Max(0, manager.OrdersTotal - manager.Delivered);
            objectiveValue.text = manager.HasFailed
                ? "Run failed!"
                : !manager.HasStarted
                ? "Pick up your orders at the restaurant"
                : remaining > 0
                    ? $"Deliver your orders ({remaining} remaining)"
                    : "Run complete! Return to the restaurant";
        }
        if (busBody == null) FindBusBody();
        float kmh = busBody != null ? Vector3.ProjectOnPlane(busBody.linearVelocity, Vector3.up).magnitude * 3.6f : 0f;
        if (speedValue != null) speedValue.text = $"{Mathf.RoundToInt(kmh)} <size=48%>km/h</size>";
        if (speedFill != null) speedFill.fillAmount = Mathf.Clamp01(kmh / 160f);

        UpdateWaypoints();
    }

    private void PrepareRunHUD()
    {
        // All panels and text objects are authored in the Canvas and assigned in the Inspector.
        scoreBaseScale = scoreValue.rectTransform.localScale;
        gainBasePosition = scoreGain.rectTransform.anchoredPosition;
        timerColor = timerValue.color;
        shownScore = manager != null ? manager.Score : 0;
        if (manager != null) manager.ScoreChanged += OnScoreChanged;
    }
    private void OnScoreChanged(int total, int gain)
    {
        scorePulse = 0;
        pendingGain += gain;
        if (scoreGain != null) scoreGain.text = "+" + pendingGain;
    }
    private void UpdateRunHUD()
    {
        float dt = Time.unscaledDeltaTime;
        shownScore = Mathf.SmoothDamp(shownScore,manager.Score,ref scoreVelocity,.2f,Mathf.Infinity,dt);
        if (Mathf.Abs(shownScore - manager.Score) < .5f) shownScore = manager.Score;
        int value = Mathf.RoundToInt(shownScore);
        if (scoreValue != null)
        {
            if (value != displayedScore) { displayedScore = value; scoreValue.text = value.ToString(); }
            scoreValue.rectTransform.localScale = scoreBaseScale * (1 + Mathf.Sin(Mathf.Clamp01(scorePulse / .35f) * Mathf.PI) * .14f);
        }
        if (scoreGain != null)
        {
            Color color = scoreGain.color;
            color.a = 1 - Mathf.Clamp01(scorePulse / .85f);
            scoreGain.color = color;
            scoreGain.rectTransform.anchoredPosition = gainBasePosition + new Vector2(0,Mathf.Min(scorePulse,.85f) * 35);
            if (scorePulse > .85f) pendingGain = 0;
        }
        scorePulse += dt;
        if (timerValue == null) return;
        int seconds = Mathf.CeilToInt(manager.RunActive ? manager.RunRemaining : manager.HasStarted ? 0 : manager.RunTimeLimit);
        int state = manager.RunActive ? 1 : manager.AllDelivered ? 2 : manager.HasFailed ? 3 : 0;
        if (seconds != displayedSeconds || state != displayedRunState)
        {
            displayedSeconds = seconds;
            displayedRunState = state;
            string label = manager.RunActive ? "TIME LEFT" : manager.AllDelivered ? "RUN COMPLETE" : manager.HasFailed ? "OUT OF TIME" : "DELIVERY RUN";
            timerValue.text = $"{seconds / 60:00}:{seconds % 60:00}\n<size=48%>{label}</size>";
        }
        bool urgent = manager.RunActive && seconds <= 15;
        timerValue.color = urgent ? Color.Lerp(timerColor,new Color(1,.65f,.2f),.5f + .5f * Mathf.Sin(Time.unscaledTime * 8)) : timerColor;
    }
    private void OnDestroy()
    {
        if (manager != null) manager.ScoreChanged -= OnScoreChanged;
    }

    private void UpdateClock()
    {
        var cycle = CroustyDayNightCycle.Instance;
        if (cycle == null || timeValue == null) return;
        int minute = Mathf.FloorToInt(cycle.Hour * 60f) % 1440;
        if (minute == displayedMinute) return;
        displayedMinute = minute;
        int hour = minute / 60;
        int twelveHour = hour % 12;
        if (twelveHour == 0) twelveHour = 12;
        timeValue.text = $"{twelveHour:00}:{minute % 60:00} {(hour < 12 ? "AM" : "PM")}\n<size=52%>{cycle.Phase.ToString().ToUpperInvariant()}</size>";
    }

#if UNITY_EDITOR
    private void PrepareRunPanelsInEditor()
    {
        if (canvas == null || speedValue == null) return;
        if (timeValue == null) timeValue = PlaceClockPanel("Time of day", -86, 26, "06:00 AM\n<size=52%>MORNING</size>");
        if (timerValue == null) timerValue = PlaceClockPanel("Delivery countdown", -172, 28, "01:30\n<size=48%>DELIVERY RUN</size>");
        var card = canvas.transform.Find("Carte score");
        if (card == null && deliveryValue != null)
        {
            var source = deliveryValue.transform.parent as RectTransform;
            if (source != null)
            {
                var copy = Instantiate(source, source.parent);
                copy.name = "Carte score";
                copy.anchoredPosition = new Vector2(14,-14);
                card = copy;
                foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name.Contains("Titre")) text.text = "SCORE";
            }
        }
        if (card == null) return;
        if (scoreValue == null)
        {
            var value = card.Find("Valeur score") ?? card.Find("Progression livraisons");
            if (value != null) scoreValue = value.GetComponent<TMP_Text>();
        }
        if (scoreValue == null) return;
        if (scoreGain == null)
        {
            var existing = card.Find("Animated score gain");
            if (existing != null) scoreGain = existing.GetComponent<TMP_Text>();
            else
            {
                scoreGain = Instantiate(scoreValue,scoreValue.transform.parent);
                scoreGain.name = "Animated score gain";
                scoreGain.fontSize = Mathf.Max(16,scoreValue.fontSize * .5f);
                scoreGain.enableAutoSizing = false;
                scoreGain.text = "";
                scoreGain.raycastTarget = false;
                var color = scoreGain.color;
                color.a = 0;
                scoreGain.color = color;
                scoreGain.rectTransform.anchoredPosition = scoreValue.rectTransform.anchoredPosition + new Vector2(105,-5);
                scoreGain.rectTransform.sizeDelta = new Vector2(110,35);
                scoreGain.alignment = TextAlignmentOptions.Center;
            }
            scoreValue.fontSizeMax = scoreValue.fontSize;
            scoreValue.fontSizeMin = Mathf.Min(20,scoreValue.fontSize);
            scoreValue.enableAutoSizing = true;
            scoreValue.text = "0";
        }
    }

    private TMP_Text PlaceClockPanel(string panelName, float offset, float size, string initialText)
    {
        var existing = canvas.transform.Find(panelName);
        if (existing != null) return existing.GetComponentInChildren<TMP_Text>(true);
        var source = speedValue.transform.parent as RectTransform;
        if (source == null) return null;
        var panel = Instantiate(source,source.parent);
        panel.name = panelName;
        panel.anchoredPosition = source.anchoredPosition + new Vector2(0,offset);
        foreach (var image in panel.GetComponentsInChildren<Image>(true))
        {
            image.raycastTarget = false;
            if (image.type == Image.Type.Filled || image.name == "Rail vitesse" ||
                image.name.IndexOf("jauge", System.StringComparison.OrdinalIgnoreCase) >= 0)
                DestroyImmediate(image.gameObject);
        }
        var label = panel.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return null;
        label.name = panelName == "Time of day" ? "Clock" : "Countdown";
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.lineSpacing = -4;
        label.raycastTarget = false;
        label.text = initialText;
        return label;
    }

    public void PrepareUI()
    {
        if (Application.isPlaying) return;
        if (canvas == null) BuildDefaultUI();
        PrepareRunPanelsInEditor();
        if (waypointTemplate == null)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var template = CreateWaypoint();
            waypointTemplate = template.root;
            waypointTemplate.name = "Modèle de repère";
            template.icon.sprite = CroustyUISpriteAssets.Save(template.icon.sprite, "RepereCercle");
            var arrow = template.arrow.GetComponent<Image>();
            arrow.sprite = CroustyUISpriteAssets.Save(arrow.sprite, "RepereFleche");
            waypointTemplate.gameObject.SetActive(false);
        }
    }
#endif

    private void FindBusBody()
    {
        if (manager != null && manager.player != null)
            busBody = manager.player.GetComponentInParent<Rigidbody>();
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
        {
            var root = Instantiate(waypointTemplate, canvas.transform);
            root.name = "Repère";
            waypoints.Add(new Waypoint {
                root = root,
                icon = root.Find("Icone").GetComponent<Image>(),
                arrow = root.Find("Fleche").GetComponent<RectTransform>(),
                label = root.Find("Distance").GetComponent<Text>()
            });
        }

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

#if UNITY_EDITOR
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
#endif

    // ---------- CREATION DE L'UI ----------

#if UNITY_EDITOR
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

    }
#endif

#if UNITY_EDITOR
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
#endif
}
