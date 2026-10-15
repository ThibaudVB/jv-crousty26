using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Power = PowerUpInventory.PowerUpType;

// Session unlocks are separate from the two consumable inventory slots.
public sealed class PowerUnlockProgression : MonoBehaviour
{
    public DeliveryManager deliveries;
    [Min(0)] public int ricePrice = 130;
    [Min(0)] public int gpsPrice = 130;
    [Min(0)] public int flightPrice = 5000;

    [Header("Progression variée (désactivé = règles d'origine)")]
    [Tooltip("Premier pouvoir tiré au hasard entre PET, GPS et riz (jamais le vol). Décoché : toujours le PET.")]
    public bool randomStartingPower;
    [Tooltip("Le vol n'est proposé qu'une fois les 3 autres pouvoirs débloqués.")]
    public bool flightLast;
    [Tooltip("Prix tirés au hasard à chaque partie pour les 2 pouvoirs à débloquer (le vol garde Flight Price).")]
    public bool randomPrices;
    [Tooltip("Prix du pouvoir le moins cher (min, max)")]
    public Vector2Int cheapPriceRange = new Vector2Int(600, 900);
    [Tooltip("Prix du pouvoir le plus cher (min, max)")]
    public Vector2Int expensivePriceRange = new Vector2Int(1000, 1400);
    public Sprite petSprite;

    [Header("Authored panels")]
    public GameObject choicePanel;
    public GameObject backdrop;
    public RawImage blurredWorld;
    public TMP_Text message;
    public TMP_Text balance;
    public Button[] choices;
    public Image[] artwork;
    public TMP_Text[] titles;
    public TMP_Text[] prices;
    public Image[] pricePaper;
    public GameObject[] confirmHints;
    public Button continueButton;
    public Sprite gpsSprite, riceSprite, flightSprite;
    public Sprite creamPaper, orangePaper;
    public Material blurMaterial;

    public static bool ChoiceOpen { get; private set; }
    private readonly HashSet<Power> unlocked = new HashSet<Power>();
    private readonly Dictionary<Power, int> rolledPrices = new Dictionary<Power, int>();
    private readonly List<Power> offers = new List<Power>();
    private int completedChoices;
    private bool open;
    private float previousTimeScale;
    private CursorLockMode previousLock;
    private bool previousCursor;
    private RenderTexture blurred;
    private int openedFrame;

    public bool IsUnlocked(Power power) => unlocked.Contains(power);
    public int Price(Power power)
    {
        if (power != Power.Flight && rolledPrices.TryGetValue(power, out int rolled)) return rolled;
        return Mathf.Max(0, power == Power.Flight ? flightPrice : power == Power.Rice ? ricePrice : gpsPrice);
    }

    private static readonly Power[] GroundPowers = { Power.Pet, Power.GPS, Power.Rice };

    private void Awake()
    {
        // Premier pouvoir : le PET, ou un des 3 pouvoirs au sol au hasard
        unlocked.Add(randomStartingPower ? GroundPowers[Random.Range(0, GroundPowers.Length)] : Power.Pet);

        // Les 2 autres pouvoirs au sol : un prix bas et un prix haut, dans un ordre au hasard
        if (randomPrices)
        {
            var locked = new List<Power>();
            foreach (var power in GroundPowers) if (!unlocked.Contains(power)) locked.Add(power);
            if (locked.Count == 2 && Random.value < 0.5f) locked.Reverse();
            for (int i = 0; i < locked.Count; i++)
            {
                Vector2Int range = i == 0 ? cheapPriceRange : expensivePriceRange;
                int price = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);
                rolledPrices[locked[i]] = Mathf.RoundToInt(price / 10f) * 10; // prix ronds (650 $, pas 653 $)
            }
        }

        choicePanel.SetActive(false);
        backdrop.SetActive(false);
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            choices[i].onClick.AddListener(() => Purchase(index));
            var hover = choices[i].gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (choices[index].IsInteractable()) choices[index].Select(); });
            hover.triggers.Add(enter);
        }
        continueButton.onClick.AddListener(ContinueWithoutPurchase);
    }

    private void Start()
    {
        if (deliveries == null) deliveries = DeliveryManager.Instance;
        if (deliveries != null) deliveries.RunCompleted += OnRunCompleted;
    }

    public List<Power> GetOffers(int runSize)
    {
        var result = new List<Power>(2);
        int milestone = completedChoices == 0 ? 5 : completedChoices == 1 ? 10 : 15;
        if (completedChoices >= 3 || runSize < milestone) return result;
        if (randomStartingPower && !IsUnlocked(Power.Pet)) result.Add(Power.Pet);
        if (!IsUnlocked(Power.GPS)) result.Add(Power.GPS);
        if (!IsUnlocked(Power.Rice)) result.Add(Power.Rice);
        // Le vol : en dernier obligatoirement (option), sinon dès le 2e choix comme à l'origine
        bool flightAllowed = flightLast ? result.Count == 0 : completedChoices > 0;
        if (flightAllowed && !IsUnlocked(Power.Flight)) result.Add(Power.Flight);
        return result;
    }

    private void OnRunCompleted()
    {
        if (open || deliveries.HasFailed) return;
        offers.Clear();
        offers.AddRange(GetOffers(deliveries.OrdersTotal));
        if (offers.Count == 0) return;
        open = ChoiceOpen = true;
        openedFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        previousLock = Cursor.lockState;
        previousCursor = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshChoices();
        StartCoroutine(ShowPanel());
    }

    private IEnumerator ShowPanel()
    {
        // Capture only the world camera. Overlay HUD canvases stay crisp above the backdrop.
        yield return null;
        if (!open) yield break;
        try { CaptureWorld(); }
        catch (System.Exception error) { Debug.LogException(error, this); }
        backdrop.SetActive(true);
        choicePanel.SetActive(true);
        for (int i = 0; i < offers.Count; i++)
            if (choices[i].interactable) { choices[i].Select(); yield break; }
        continueButton.Select();
    }

    private void RefreshChoices()
    {
        message.text = "Congratulations! Choose the\nnext power for the boxes!";
        balance.text = "$" + deliveries.Money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        bool affordable = false;
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i].gameObject.SetActive(i < offers.Count);
            if (i >= offers.Count) continue;
            var power = offers[i];
            artwork[i].sprite = power == Power.GPS ? gpsSprite : power == Power.Rice ? riceSprite :
                power == Power.Pet ? petSprite : flightSprite;
            titles[i].text = power == Power.GPS ? "Express GPS" : power == Power.Rice ? "Asian Rice" :
                power == Power.Pet ? "Toxic Gas" : "Flying Bus";
            prices[i].text = "$" + Price(power).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            choices[i].interactable = deliveries.Money >= Price(power);
            artwork[i].color = choices[i].interactable ? Color.white : new Color(.55f, .55f, .55f, 1);
            affordable |= choices[i].interactable;
            var rect = (RectTransform)choices[i].transform;
            rect.anchoredPosition = new Vector2(offers.Count == 1 ? 0 : (i == 0 ? -155 : 155), -180);
        }
        continueButton.gameObject.SetActive(!affordable);
        if (!affordable) message.text = "Keep delivering to earn more!\nThis choice will return next run.";
    }

    private void Update()
    {
        if (!open || !choicePanel.activeSelf) return;
        CroustyMenu.EnsureControllerSelection(choicePanel);
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        for (int i = 0; i < offers.Count; i++)
        {
            bool focused = selected == choices[i].gameObject && choices[i].interactable;
            pricePaper[i].sprite = focused ? orangePaper : creamPaper;
            prices[i].color = focused ? Color.white : new Color32(38, 38, 50, 255);
            confirmHints[i].SetActive(focused);
        }
    }

    public bool Purchase(int index)
    {
        if (!open || Time.frameCount <= openedFrame || index < 0 || index >= offers.Count || deliveries.HasFailed) return false;
        var power = offers[index];
        if (IsUnlocked(power) || !deliveries.TrySpendMoney(Price(power))) return false;
        unlocked.Add(power);
        completedChoices++;
        Close();
        deliveries.ContinueAfterPowerChoice();
        return true;
    }

    private void ContinueWithoutPurchase()
    {
        if (!open || offers.Exists(p => deliveries.Money >= Price(p))) return;
        Close();
        deliveries.ContinueAfterPowerChoice();
    }

    private void CaptureWorld()
    {
        if (Camera.main == null || blurMaterial == null) return;
        float scale = Mathf.Max(.5f, 480f / Mathf.Max(1, Screen.width));
        int width = Mathf.RoundToInt(Screen.width * scale), height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * scale));
        var world = RenderTexture.GetTemporary(width, height, 24);
        var temp = RenderTexture.GetTemporary(width, height, 0);
        world.filterMode = temp.filterMode = FilterMode.Bilinear;
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = world });
            blurred = new RenderTexture(width, height, 0) { filterMode = FilterMode.Bilinear };
            for (int i = 0; i < 2; i++)
            {
                Graphics.Blit(world, temp, blurMaterial, 0);
                Graphics.Blit(temp, world, blurMaterial, 1);
            }
            Graphics.Blit(world, blurred);
            blurredWorld.texture = blurred;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.ReleaseTemporary(world);
            RenderTexture.ReleaseTemporary(temp);
        }
    }

    private void Close()
    {
        if (!open) return;
        open = ChoiceOpen = false;
        choicePanel.SetActive(false);
        backdrop.SetActive(false);
        blurredWorld.texture = null;
        if (blurred != null) { blurred.Release(); Destroy(blurred); blurred = null; }
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursor;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDisable() { Close(); }
    private void OnDestroy()
    {
        if (deliveries != null) deliveries.RunCompleted -= OnRunCompleted;
        Close();
    }
}
