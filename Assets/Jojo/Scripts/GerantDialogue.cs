using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boîte de dialogue du gérant en bas à gauche de l'écran :
// portrait + étiquette "MANAGER" + bandeau papier avec le texte qui s'écrit lettre par lettre.
// Le gérant réagit à ce qui se passe : début de tournée, livraison réussie, partie perdue,
// poursuite de police, boîte bonus, bus à l'arrêt... Il tire une réplique au hasard
// dans la liste correspondante (toutes les listes se modifient dans l'Inspector).
//
// A placer à la main sur un GameObject vide de la scène (ex : "Gerant").
// Deux façons de l'utiliser :
// 1. Boîte déjà placée dans un Canvas : glisser son texte, sa flèche et son Canvas dans "Boîte placée dans la scène".
// 2. Sinon, la boîte est créée au lancement : glisser les images de Jojo/Resources/Gerant dans "Images".
public class GerantDialogue : MonoBehaviour
{
    public event System.Action MessageChanged;
    [Header("Références")]
    [Tooltip("Si vide : le DeliveryManager de la scène")]
    public DeliveryManager manager;
    [Tooltip("Si vide : le PoliceChaseManager de la scène")]
    public PoliceChaseManager police;

    [Header("Boîte placée dans la scène (optionnel)")]
    [Tooltip("Texte du message. Si rempli, la boîte n'est pas créée par le script.")]
    [SerializeField] private TMP_Text authoredMessage;
    [Tooltip("Petit triangle \"suite\"")]
    [SerializeField] private RectTransform authoredArrow;
    [Tooltip("Canvas de la boîte : caché quand le jeu est en pause")]
    [SerializeField] private Canvas authoredCanvas;

    [Header("Images (portrait, bandeau, etiquette, titre)")]
    public Sprite portrait;
    [Tooltip("Bandeau papier derrière le texte")]
    public Sprite banner;
    [Tooltip("Forme sombre derrière le titre (Vector 1)")]
    public Sprite nameTag;
    [Tooltip("Titre GÉRANT en image. Si vide, il est écrit avec la police.")]
    public Sprite nameTitle;

    [Header("Répliques (une est tirée au hasard dans la liste)")]
    [Tooltip("On récupère la commande au restaurant (une tournée commence)")]
    public string[] deliveryStartLines =
    {
        "Here, go deliver this. And don't touch the drumsticks, I counted them. Well... I counted up to three.",
        "Order for old Mrs. Johnson. She ordered three days ago, but hey, she won't die. Well, hopefully.",
        "The chicken is still hot. Well, it was yesterday.",
        "Careful, this order is worth more than your paycheck. Granted, that's not a high bar.",
        "Drive fast, before the health inspector wakes up.",
        "Honestly? I don't even like Croustys. I'm more of a kebab guy. Keep it under your hat.",
        "Oh shit, the rats got a taste of the order. Oh well... extra protein.",
    };
    [Tooltip("Le gérant parle tout seul quand il n'a rien dit depuis un moment")]
    public string[] managerLines =
    {
        "A hair in the order? Is it short and curly? Oh... well, then that ain't a hair.",
        "The secret recipe? Chicken, breading, and a whole lot of prayers.",
        "I dropped the chicken on the floor. Five-second rule. Or five minutes, I forget.",
        "The customer left five stars. Okay, it was my mom, but it still counts.",
        "I dumped all my special white sauce on it. The customer didn't ask for any, but trust me, they're gonna love it.",
    };
    [Tooltip("Une commande est livrée")]
    public string[] deliverySuccessLines =
    {
        "Great job. You've earned the right to maybe not get fired today.",
        "Incredible. I'm giving you a raise. Two cents. Nah, make it one.",
        "You're faster than Uber Eats. And cleaner than my kitchen.",
    };
    [Tooltip("La partie est perdue (arrêté par la police ou bus détruit)")]
    public string[] deliveryFailedLines =
    {
        "You took so long the chicken had time to hatch.",
        "Even the rats deliver faster than you. And they get paid in crumbs.",
        "Next time, I'm sending my grandma. On a scooter.",
        "The customer called. They're crying. So is the chicken.",
    };
    [Tooltip("La police commence à poursuivre le bus")]
    public string[] policeChaseLines =
    {
        "If the cops ask you questions, you've never worked here. Neither have I, for that matter.",
        "Are they chasing you for speeding... or for my health permits? Good question.",
        "Throwing a chicken at 'em usually calms them down.",
        "The cops are my best customers. Except when they're on duty.",
    };
    [Tooltip("Le bus ramasse une boîte bonus")]
    public string[] bonusBoxLines =
    {
        "A mystery box! I don't even remember what I put in there. It's gonna be a surprise for both of us.",
    };
    [Tooltip("Le bus ne bouge plus depuis un moment (le joueur est lent)")]
    public string[] idleLines =
    {
        "You took so long the chicken had time to hatch.",
        "Even the rats deliver faster than you. And they get paid in crumbs.",
        "Next time, I'm sending my grandma. On a scooter.",
        "Hello? Did the bus stall, or is it just you?",
        "The customer called. They're crying. So is the chicken.",
    };
    [Tooltip("Le joueur reprend la partie après avoir mis pause")]
    public string[] pauseLines =
    {
        "Taking a break? The chicken ain't getting any colder... wait, I mean warmer. Keep moving.",
    };

    [Header("Quand parler")]
    [Tooltip("Le gérant dit une réplique \"Manager Lines\" s'il n'a rien dit depuis ce nombre de secondes")]
    public float randomLineInterval = 20f;
    [Tooltip("Secondes à l'arrêt avant que le gérant se plaigne")]
    public float idleSeconds = 10f;
    [Tooltip("En dessous de cette vitesse (km/h), le bus est considéré à l'arrêt")]
    public float idleSpeedKmh = 3f;

    [Header("Texte")]
    public string speakerName = "MANAGER";
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
    private float arrowBaseY = 38f;     // hauteur du triangle quand il ne sautille pas
    private float lastSpokeTime;        // dernière fois que le gérant a parlé
    private float idleTimer;            // secondes passées à l'arrêt
    private bool saidFailed;            // réplique de défaite déjà dite
    private bool wasChased;             // la police poursuivait déjà le bus
    private bool automaticDialogueEnabled = true;
    private int lastBonusCharges;       // charges de bonus à l'image précédente

    // Bonus du bus (pour savoir quand une boîte bonus est ramassée)
    private Rigidbody busBody;
    private BusPetController pet;
    private BusFlightController flight;
    private BusGPS gps;
    private BusRiceThrower rice;

    // Taille de la boîte, d'après la maquette
    private static readonly Vector2 BoxSize = new Vector2(740f, 410f);

    private void Start()
    {
        if (manager == null)
            manager = DeliveryManager.Instance;
        if (police == null)
            police = FindAnyObjectByType<PoliceChaseManager>();

        if (manager != null)
        {
            // On s'abonne aux événements : la fonction est appelée quand ça arrive
            manager.RunStarted += OnRunStarted;
            manager.DeliveryCompleted += OnDeliveryCompleted;
            if (manager.player != null)
                busBody = manager.player.GetComponentInParent<Rigidbody>();
        }

        CroustyPauseMenu.Resumed += OnResumed;

        pet = FindAnyObjectByType<BusPetController>();
        flight = FindAnyObjectByType<BusFlightController>();
        gps = FindAnyObjectByType<BusGPS>();
        rice = FindAnyObjectByType<BusRiceThrower>();
        lastBonusCharges = BonusCharges();

        if (authoredMessage != null)
        {
            // Boîte déjà dans la scène : rien à créer
            arrow = authoredArrow;
            if (arrow != null)
                arrowBaseY = arrow.anchoredPosition.y;
        }
        else
        {
            Build();
        }

        Say(managerLines);
    }

    private void OnDestroy()
    {
        CroustyPauseMenu.Resumed -= OnResumed;
        if (manager != null)
        {
            manager.RunStarted -= OnRunStarted;
            manager.DeliveryCompleted -= OnDeliveryCompleted;
        }
    }

    private void OnRunStarted() { Say(deliveryStartLines); }
    private void OnDeliveryCompleted(DeliveryPoint point) { Say(deliverySuccessLines); }
    private void OnResumed() { if (manager == null || !manager.HasFailed) Say(pauseLines); }

    public void SetAutomaticDialogueEnabled(bool enabled)
    {
        automaticDialogueEnabled = enabled;
    }

    // Dit une réplique tirée au hasard dans la liste (différente de la réplique actuelle si possible)
    private void Say(string[] lines)
    {
        if (!automaticDialogueEnabled || lines == null || lines.Length == 0)
            return;

        string line = lines[Random.Range(0, lines.Length)];
        if (line == currentMessage && lines.Length > 1)
            line = lines[(System.Array.IndexOf(lines, line) + 1) % lines.Length];

        SetMessage(line);
        lastSpokeTime = Time.time;
    }

    // Regarde ce qui se passe dans le jeu et fait parler le gérant
    private void ReactToGame()
    {
        if (manager != null && manager.HasFailed)
        {
            if (!saidFailed)
                Say(deliveryFailedLines);
            saidFailed = true;
            return;
        }

        // La police commence une poursuite
        bool chased = police != null && police.ChaseStarted;
        if (chased && !wasChased)
            Say(policeChaseLines);
        wasChased = chased;

        // Une boîte bonus vient d'être ramassée : un bonus a gagné des charges
        int charges = BonusCharges();
        if (charges > lastBonusCharges)
            Say(bonusBoxLines);
        lastBonusCharges = charges;

        // Le bus est à l'arrêt depuis trop longtemps
        if (busBody != null && Time.timeScale > 0f)
        {
            Vector3 velocity = busBody.linearVelocity;
            velocity.y = 0f;
            if (velocity.magnitude * 3.6f < idleSpeedKmh)
                idleTimer += Time.deltaTime;
            else
                idleTimer = 0f;

            if (idleTimer >= idleSeconds)
            {
                Say(idleLines);
                idleTimer = 0f;
            }
        }

        // Rien dit depuis un moment : le gérant parle tout seul
        if (Time.time - lastSpokeTime >= randomLineInterval)
            Say(managerLines);
    }

    // Total des charges de tous les bonus du bus
    private int BonusCharges()
    {
        int total = 0;
        if (pet != null) total += pet.UsesRemaining;
        if (flight != null) total += flight.UsesRemaining;
        if (gps != null) total += gps.Charges;
        if (rice != null) total += rice.UsesRemaining;
        return total;
    }

    // Crée tous les éléments de la boîte
    private void Build()
    {
        if (font == null)
            font = (CroustyTypography.LegacyFont != null ? CroustyTypography.LegacyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

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
        if (!HasTextBox() || message == null || message == currentMessage)
            return;

        currentMessage = message;
        shownLetters = lettersPerSecond > 0f ? 0f : message.Length;
        MessageChanged?.Invoke();
    }

    private void Update()
    {
        if (!HasTextBox())
            return;

        // La boîte placée dans la scène se cache pendant la pause
        if (authoredCanvas != null)
            authoredCanvas.enabled = Time.timeScale > 0f;

        ReactToGame();

        // Effet machine à écrire (temps réel : continue pendant le game over, qui met le jeu en pause)
        if (shownLetters < currentMessage.Length)
            shownLetters += lettersPerSecond * Time.unscaledDeltaTime;

        int count = Mathf.Min(currentMessage.Length, Mathf.FloorToInt(shownLetters));
        string visibleText = currentMessage.Substring(0, count);
        if (authoredMessage != null)
            authoredMessage.text = visibleText;
        else
            messageText.text = visibleText;

        if (arrow == null)
            return;

        // Le triangle n'apparaît (et sautille) qu'une fois le texte complet
        bool finished = count >= currentMessage.Length;
        arrow.gameObject.SetActive(finished && currentMessage.Length > 0);
        arrow.anchoredPosition = new Vector2(arrow.anchoredPosition.x,
            arrowBaseY + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * arrowBounce);
    }

    // Vrai s'il y a un texte où écrire (boîte de la scène ou boîte créée par le script)
    private bool HasTextBox()
    {
        return authoredMessage != null || messageText != null;
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
