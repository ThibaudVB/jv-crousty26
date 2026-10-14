using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD de la tournée : livraisons, objectif, vitesse, heure, chrono, score,
// et des repères à l'écran (visibles à travers les bâtiments) vers le magasin et les adresses.
//
// A placer à la main sur un GameObject de la scène.
// Les panels sont déjà dans le Canvas : il suffit de glisser chaque texte dans son champ.
// (Menu Tools/Crousty peut les recréer si besoin, voir PrepareUI en bas du fichier.)
public class CroustyHUD : MonoBehaviour
{
    [Tooltip("Si vide : le DeliveryManager de la scène")]
    public DeliveryManager manager;

    [Header("Panels placés dans le Canvas")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text deliveryValue;
    [SerializeField] private TMP_Text objectiveValue;
    [SerializeField] private TMP_Text speedValue;
    [SerializeField] private Image speedFill;
    [SerializeField] private TMP_Text timeValue;
    [SerializeField] private TMP_Text timerValue;
    [SerializeField] private TMP_Text scoreValue;
    [SerializeField] private TMP_Text scoreGain;

    [Header("Style maquette (textes en français, heure 24 h, score en k)")]
    [Tooltip("Coché par le menu Tools/Crousty qui place le HUD de la maquette")]
    [SerializeField] private bool referenceHudStyle;
    [SerializeField, HideInInspector] private int referenceLayoutRevision; // utilisé par l'outil de l'éditeur

    [Header("Textes de l'objectif ({0} = commandes restantes)")]
    public string objectivePickup = "Pick up your orders at the restaurant";
    public string objectiveDeliver = "Deliver your orders ({0} remaining)";
    public string objectiveComplete = "Run complete! Return to the restaurant";
    public string objectiveFailed = "Run failed!";

    [Header("Textes de l'objectif, style maquette ({0} = livrées, {1} = total)")]
    [TextArea] public string referencePickup = "Pick up your orders at\nCrousty's restaurant (0/1)";
    [TextArea] public string referenceDeliver = "Deliver your orders\n({0}/{1})";
    [TextArea] public string referenceComplete = "Run complete! Return to\nCrousty's restaurant.";
    [TextArea] public string referenceFailed = "Run failed! Return to the restaurant.";

    [Header("Vitesse")]
    [Tooltip("Vitesse (km/h) qui remplit complètement la jauge")]
    public float speedGaugeMaxKmh = 160f;

    [Header("Chrono")]
    [Tooltip("En dessous de ce nombre de secondes, le chrono clignote en orange")]
    public float urgentSeconds = 15f;
    public Color urgentColor = new Color(1f, 0.65f, 0.2f);

    [Header("Score")]
    [Tooltip("Durée de l'animation du \"+points\" (s)")]
    public float scoreGainDuration = 0.85f;
    [Tooltip("Le \"+points\" monte de cette distance (pixels)")]
    public float scoreGainRise = 35f;

    [Header("Repères à l'écran")]
    public bool showWaypoints = true;
    public float waypointSize = 44f;
    [Tooltip("Marge avec le bord de l'écran (pixels)")]
    public float screenEdgeMargin = 60f;
    [Tooltip("Le repère est placé à cette hauteur au-dessus de la cible (m)")]
    public float waypointHeightOffset = 4f;
    [Tooltip("Modèle copié pour chaque repère (enfants : Icone, Fleche, Distance)")]
    [SerializeField] private RectTransform waypointTemplate;
    [Tooltip("Taille du texte des repères créés par l'outil de l'éditeur")]
    public int fontSize = 32;
    [Tooltip("Optionnel : icône des adresses de livraison. Si vide : rond de la couleur de l'adresse.")]
    public Sprite deliveryWaypointIcon;
    [Tooltip("Optionnel : icône du restaurant. Si vide : rond de la couleur du restaurant.")]
    public Sprite shopWaypointIcon;

    // Un repère à l'écran : un rond, une flèche (si la cible est hors écran) et la distance
    private class Waypoint
    {
        public RectTransform root;
        public Image icon;
        public Sprite defaultSprite; // rond du modèle, utilisé quand il n'y a pas d'icône
        public Outline outline;
        public RectTransform arrow;
        public Text label;
    }
    private readonly List<Waypoint> waypoints = new List<Waypoint>();

    private Rigidbody busBody;
    private Color timerNormalColor;

    // Animation du score
    private Vector3 scoreStartScale;
    private Vector2 gainStartPosition;
    private float shownScore;           // score affiché (rattrape le vrai score en douceur)
    private float scoreSpeed;           // utilisé par Mathf.SmoothDamp
    private float timeSinceGain = 10f;  // secondes depuis les derniers points gagnés
    private int pendingGain;            // points affichés dans le "+points"

    private void Start()
    {
        if (manager == null)
            manager = DeliveryManager.Instance;

        if (canvas == null || waypointTemplate == null || deliveryValue == null ||
            objectiveValue == null || speedValue == null || timeValue == null ||
            scoreValue == null || scoreGain == null)
        {
            Debug.LogError("CroustyHUD : assigner tous les panels du HUD dans l'Inspector.", this);
            enabled = false;
            return;
        }

        scoreStartScale = scoreValue.rectTransform.localScale;
        gainStartPosition = scoreGain.rectTransform.anchoredPosition;
        if (timerValue != null) timerNormalColor = timerValue.color;
        UpdateTimer();

        if (manager != null)
        {
            shownScore = manager.Score;
            manager.ScoreChanged += OnScoreChanged;
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        if (manager == null)
            return;

        UpdateDeliveries();
        UpdateObjective();
        UpdateSpeed();
        UpdateClock();
        UpdateTimer();
        UpdateScore();
        UpdateWaypoints();
    }

    // ---------- PANELS ----------

    private void UpdateDeliveries()
    {
        if (manager.HasStarted)
            deliveryValue.text = manager.Delivered + "/" + manager.OrdersTotal;
        else
            deliveryValue.text = "0/" + manager.NextRunOrders;
    }

    private void UpdateObjective()
    {
        int remaining = Mathf.Max(0, manager.OrdersTotal - manager.Delivered);

        if (referenceHudStyle)
        {
            if (manager.HasFailed)
                objectiveValue.text = referenceFailed;
            else if (!manager.HasStarted)
                objectiveValue.text = referencePickup;
            else if (remaining > 0)
                objectiveValue.text = string.Format(referenceDeliver, manager.Delivered, manager.OrdersTotal);
            else
                objectiveValue.text = referenceComplete;
            return;
        }

        if (manager.HasFailed)
            objectiveValue.text = objectiveFailed;
        else if (!manager.HasStarted)
            objectiveValue.text = objectivePickup;
        else if (remaining > 0)
            objectiveValue.text = string.Format(objectiveDeliver, remaining);
        else
            objectiveValue.text = objectiveComplete;
    }

    private void UpdateSpeed()
    {
        if (busBody == null && manager.player != null)
            busBody = manager.player.GetComponentInParent<Rigidbody>();

        float kmh = 0f;
        if (busBody != null)
        {
            Vector3 velocity = busBody.linearVelocity;
            velocity.y = 0f;               // on ne compte pas la vitesse verticale
            kmh = velocity.magnitude * 3.6f; // m/s -> km/h
        }

        // Style maquette : "km/h" passe sous le chiffre
        string separator = referenceHudStyle ? "\n" : " ";
        speedValue.text = Mathf.RoundToInt(kmh) + separator + "<size=48%>km/h</size>";
        if (speedFill != null)
            speedFill.fillAmount = Mathf.Clamp01(kmh / speedGaugeMaxKmh);
    }

    // Heure du cycle jour / nuit, en format 12 h (ex : 06:30 AM)
    private void UpdateClock()
    {
        CroustyDayNightCycle cycle = CroustyDayNightCycle.Instance;
        if (cycle == null)
            return;

        int totalMinutes = Mathf.FloorToInt(cycle.Hour * 60f) % 1440;
        int hour = totalMinutes / 60;
        int minute = totalMinutes % 60;

        // Style maquette : heure sur 24 h, sans la période de la journée
        if (referenceHudStyle)
        {
            timeValue.text = $"{hour:00}:{minute:00}";
            return;
        }

        int twelveHour = hour % 12;
        if (twelveHour == 0)
            twelveHour = 12;
        string amPm = hour < 12 ? "AM" : "PM";
        string phase = cycle.Phase.ToString().ToUpperInvariant();

        timeValue.text = $"{twelveHour:00}:{minute:00} {amPm}\n<size=52%>{phase}</size>";
    }

    private void UpdateTimer()
    {
        if (timerValue == null) return;
        var panel = timerValue.transform.parent;
        if (panel != null && panel.name == "Delivery countdown") panel.gameObject.SetActive(false);
        else timerValue.gameObject.SetActive(false);
    }

    // ---------- SCORE ----------

    // Appelé par le DeliveryManager à chaque gain de points
    private void OnScoreChanged(int total, int gain)
    {
        timeSinceGain = 0f;
        pendingGain += gain;
        scoreGain.text = "+" + pendingGain;
    }

    private void UpdateScore()
    {
        float dt = Time.unscaledDeltaTime;
        timeSinceGain += dt;

        // Le chiffre du score monte en douceur jusqu'au vrai score
        shownScore = Mathf.SmoothDamp(shownScore, manager.Score, ref scoreSpeed, 0.2f, Mathf.Infinity, dt);
        if (Mathf.Abs(shownScore - manager.Score) < 0.5f)
            shownScore = manager.Score;
        scoreValue.text = FormatScore(Mathf.RoundToInt(shownScore));

        // Le score grossit un peu juste après un gain
        float pulse = Mathf.Sin(Mathf.Clamp01(timeSinceGain / 0.35f) * Mathf.PI) * 0.14f;
        scoreValue.rectTransform.localScale = scoreStartScale * (1f + pulse);

        // Le "+points" monte et s'efface
        float progress = Mathf.Clamp01(timeSinceGain / scoreGainDuration);
        Color color = scoreGain.color;
        color.a = 1f - progress;
        scoreGain.color = color;
        scoreGain.rectTransform.anchoredPosition = gainStartPosition + new Vector2(0f, progress * scoreGainRise);

        if (progress >= 1f)
            pendingGain = 0;
    }

    // Style maquette : 6767 s'affiche "6,8k"
    private string FormatScore(int score)
    {
        if (!referenceHudStyle || score < 1000)
            return score.ToString();

        decimal thousands = score / 1000m;
        return thousands.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
    }

    // ---------- REPERES ----------

    private void UpdateWaypoints()
    {
        int used = 0;
        Camera cam = Camera.main;

        if (showWaypoints && cam != null && manager.player != null)
        {
            // Magasin : seulement quand il faut y aller
            if (manager.shop != null && manager.NeedsPickup)
            {
                ShowWaypoint(used, cam, manager.shop.transform.position, manager.shop.markerColor, manager.shop.screenLabel, shopWaypointIcon);
                used++;
            }

            // Adresses qui attendent une commande
            foreach (DeliveryPoint point in manager.VisibleDestinations)
            {
                if (point != null && point.IsWaiting)
                {
                    ShowWaypoint(used, cam, point.transform.position, point.markerColor, null, deliveryWaypointIcon);
                    used++;
                }
            }
        }

        // Cache les repères qui ne servent pas
        for (int i = used; i < waypoints.Count; i++)
            waypoints[i].root.gameObject.SetActive(false);
    }

    private void ShowWaypoint(int index, Camera cam, Vector3 worldPosition, Color color, string title, Sprite customIcon)
    {
        // Crée un nouveau repère (copie du modèle) s'il en manque
        if (index >= waypoints.Count)
        {
            RectTransform copy = Instantiate(waypointTemplate, canvas.transform);
            copy.name = "Repère";
            Waypoint created = new Waypoint();
            created.root = copy;
            created.icon = copy.Find("Icone").GetComponent<Image>();
            created.defaultSprite = created.icon.sprite;
            created.outline = created.icon.GetComponent<Outline>();
            created.arrow = copy.Find("Fleche").GetComponent<RectTransform>();
            created.label = copy.Find("Distance").GetComponent<Text>();
            waypoints.Add(created);
        }

        Waypoint waypoint = waypoints[index];
        waypoint.root.gameObject.SetActive(true);

        // Position 3D -> position sur l'écran (en pixels)
        Vector3 screen = cam.WorldToScreenPoint(worldPosition + Vector3.up * waypointHeightOffset);
        Vector2 position = new Vector2(screen.x, screen.y);
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        float margin = screenEdgeMargin * canvas.scaleFactor;

        // Cible derrière la caméra : la position est inversée, on la retourne
        bool behind = screen.z < 0f;
        if (behind)
            position = center - (position - center);

        bool onScreen = !behind &&
            position.x > margin && position.x < Screen.width - margin &&
            position.y > margin && position.y < Screen.height - margin;

        if (!onScreen)
        {
            // On colle le repère au bord de l'écran, dans la direction de la cible
            Vector2 direction = position - center;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.down;

            Vector2 halfSize = center - Vector2.one * margin;
            float scaleX = halfSize.x / Mathf.Max(Mathf.Abs(direction.x), 0.001f);
            float scaleY = halfSize.y / Mathf.Max(Mathf.Abs(direction.y), 0.001f);
            position = center + direction * Mathf.Min(scaleX, scaleY);

            // La flèche tourne vers la cible
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            waypoint.arrow.localRotation = rotation;
            waypoint.arrow.anchoredPosition = (Vector2)(rotation * Vector3.up) * waypointSize * 0.85f;
        }

        waypoint.arrow.gameObject.SetActive(!onScreen);
        waypoint.root.position = position;
        // Icône fournie : affichée avec ses vraies couleurs, sans le contour noir du rond
        bool hasIcon = customIcon != null;
        waypoint.icon.sprite = hasIcon ? customIcon : waypoint.defaultSprite;
        waypoint.icon.color = hasIcon ? Color.white : color;
        if (waypoint.outline != null) waypoint.outline.enabled = !hasIcon;
        waypoint.arrow.GetComponent<Image>().color = color;

        float distance = Vector3.Distance(manager.player.position, worldPosition);
        if (string.IsNullOrEmpty(title))
            waypoint.label.text = $"{distance:0} m";
        else
            waypoint.label.text = $"{title}\n{distance:0} m";
    }

    // =====================================================================
    // OUTILS DE L'EDITEUR (ne tournent pas en jeu)
    // Utilisés par les menus Tools/Crousty pour créer les panels manquants
    // dans le Canvas. Pas besoin d'y toucher pour modifier le HUD.
    // =====================================================================
#if UNITY_EDITOR
    public void PrepareUI()
    {
        if (Application.isPlaying)
            return;

        if (canvas == null)
            CreateCanvas();

        CreateMissingRunPanels();

        if (waypointTemplate == null)
        {
            Waypoint template = CreateWaypointTemplate();
            waypointTemplate = template.root;
            waypointTemplate.name = "Modèle de repère";
            template.icon.sprite = CroustyUISpriteAssets.Save(template.icon.sprite, "RepereCercle");
            Image arrowImage = template.arrow.GetComponent<Image>();
            arrowImage.sprite = CroustyUISpriteAssets.Save(arrowImage.sprite, "RepereFleche");
            waypointTemplate.gameObject.SetActive(false);
        }
    }

    private void CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Crousty HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
    }

    // Crée l'heure, le chrono et la carte du score en copiant les panels existants
    private void CreateMissingRunPanels()
    {
        if (canvas == null || speedValue == null)
            return;

        if (timeValue == null)
            timeValue = CopySpeedPanel("Time of day", -86f, 26f, "06:00 AM\n<size=52%>MORNING</size>");
        if (timerValue == null)
            timerValue = CopySpeedPanel("Delivery countdown", -172f, 28f, "01:30\n<size=48%>DELIVERY RUN</size>");

        Transform card = canvas.transform.Find("Carte score");
        if (card == null && deliveryValue != null)
        {
            RectTransform source = deliveryValue.transform.parent as RectTransform;
            if (source != null)
            {
                RectTransform copy = Instantiate(source, source.parent);
                copy.name = "Carte score";
                copy.anchoredPosition = new Vector2(14f, -14f);
                foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name.Contains("Titre"))
                        text.text = "SCORE";
                card = copy;
            }
        }
        if (card == null)
            return;

        if (scoreValue == null)
        {
            Transform value = card.Find("Valeur score");
            if (value == null)
                value = card.Find("Progression livraisons");
            if (value != null)
                scoreValue = value.GetComponent<TMP_Text>();
        }
        if (scoreValue == null || scoreGain != null)
            return;

        Transform existing = card.Find("Animated score gain");
        if (existing != null)
        {
            scoreGain = existing.GetComponent<TMP_Text>();
        }
        else
        {
            scoreGain = Instantiate(scoreValue, scoreValue.transform.parent);
            scoreGain.name = "Animated score gain";
            scoreGain.fontSize = Mathf.Max(16f, scoreValue.fontSize * 0.5f);
            scoreGain.enableAutoSizing = false;
            scoreGain.text = "";
            scoreGain.raycastTarget = false;
            Color color = scoreGain.color;
            color.a = 0f;
            scoreGain.color = color;
            scoreGain.rectTransform.anchoredPosition = scoreValue.rectTransform.anchoredPosition + new Vector2(105f, -5f);
            scoreGain.rectTransform.sizeDelta = new Vector2(110f, 35f);
            scoreGain.alignment = TextAlignmentOptions.Center;
        }
        scoreValue.fontSizeMax = scoreValue.fontSize;
        scoreValue.fontSizeMin = Mathf.Min(20f, scoreValue.fontSize);
        scoreValue.enableAutoSizing = true;
        scoreValue.text = "0";
    }

    // Copie le panel de vitesse (sans sa jauge) pour faire un nouveau panel texte
    private TMP_Text CopySpeedPanel(string panelName, float offsetY, float textSize, string initialText)
    {
        Transform existing = canvas.transform.Find(panelName);
        if (existing != null)
            return existing.GetComponentInChildren<TMP_Text>(true);

        RectTransform source = speedValue.transform.parent as RectTransform;
        if (source == null)
            return null;

        RectTransform panel = Instantiate(source, source.parent);
        panel.name = panelName;
        panel.anchoredPosition = source.anchoredPosition + new Vector2(0f, offsetY);

        foreach (Image image in panel.GetComponentsInChildren<Image>(true))
        {
            image.raycastTarget = false;
            bool isGauge = image.type == Image.Type.Filled || image.name == "Rail vitesse" ||
                image.name.IndexOf("jauge", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (isGauge)
                DestroyImmediate(image.gameObject);
        }

        TMP_Text label = panel.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return null;

        label.name = panelName == "Time of day" ? "Clock" : "Countdown";
        label.fontSize = textSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.lineSpacing = -4f;
        label.raycastTarget = false;
        label.text = initialText;
        return label;
    }

    private Waypoint CreateWaypointTemplate()
    {
        Waypoint waypoint = new Waypoint();

        GameObject root = new GameObject("Repere", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        waypoint.root = root.GetComponent<RectTransform>();
        waypoint.root.sizeDelta = Vector2.one * waypointSize;

        // Rond de couleur
        GameObject icon = new GameObject("Icone", typeof(RectTransform), typeof(Image), typeof(Outline));
        icon.transform.SetParent(root.transform, false);
        icon.GetComponent<RectTransform>().sizeDelta = Vector2.one * waypointSize;
        icon.GetComponent<Outline>().effectColor = Color.black;
        waypoint.icon = icon.GetComponent<Image>();
        waypoint.icon.sprite = JojoVisuals.CircleSprite;
        waypoint.icon.raycastTarget = false;

        // Flèche quand la cible est hors de l'écran
        GameObject arrow = new GameObject("Fleche", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(root.transform, false);
        Image arrowImage = arrow.GetComponent<Image>();
        arrowImage.sprite = JojoVisuals.ArrowSprite;
        arrowImage.raycastTarget = false;
        waypoint.arrow = arrow.GetComponent<RectTransform>();
        waypoint.arrow.sizeDelta = Vector2.one * waypointSize * 0.6f;

        // Distance
        GameObject label = new GameObject("Distance", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(root.transform, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -8f);
        rect.sizeDelta = new Vector2(300f, 80f);

        Text text = label.GetComponent<Text>();
        text.font = (CroustyTypography.LegacyFont != null ? CroustyTypography.LegacyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        text.fontSize = fontSize - 10;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.UpperCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;

        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);
        waypoint.label = text;

        return waypoint;
    }
#endif
}
