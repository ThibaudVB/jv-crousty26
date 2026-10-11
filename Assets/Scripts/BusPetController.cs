using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BusPetController : MonoBehaviour
{
    public Key activationKey = Key.P;
    [Range(0f, 5f)] public float blindDuration = 5f;
    [Min(0.1f)] public float boostDuration = 3f;
    [Min(1f)] public float speedMultiplier = 1.85f;
    [Min(1f)] public float accelerationMultiplier = 1.8f;
    [Min(0f)] public float cloudRange = 40f;
    [Range(0, 3)] public int startingCharges = 3;
    public int UsesRemaining { get; private set; } = 3;
    public float BoostRemaining { get; private set; }
    public bool IsBoosting => isActiveAndEnabled && BoostRemaining > 0f && drive != null &&
        drive.enabled && (flight == null || !flight.IsAirborne);
    private BusController drive;
    private BusFlightController flight;
    private PoliceChaseManager police;
    [SerializeField] private Text label;
    [SerializeField] private GameObject hud;

    private void Start()
    {
        UsesRemaining = startingCharges;
        drive = GetComponent<BusController>();
        flight = GetComponent<BusFlightController>();
        police = FindAnyObjectByType<PoliceChaseManager>();
        if (hud != null) hud.SetActive(false);
    }

#if UNITY_EDITOR
    public void PrepareUI()
    {
        if (hud != null) return;
        hud = new GameObject("PET - HUD", typeof(Canvas), typeof(CanvasScaler));
        hud.transform.SetParent(transform, false);
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var text = new GameObject("Charges PET", typeof(RectTransform), typeof(Text), typeof(Outline));
        text.transform.SetParent(hud.transform, false);
        label = text.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.color = new Color(0.7f, 1f, 0.45f);
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(30, 30);
        rect.sizeDelta = new Vector2(650, 50);
    }

#endif

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame) TryActivate();
    }

    public bool TryActivate()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || drive == null || !drive.enabled ||
            UsesRemaining <= 0 || BoostRemaining > 0f || (flight != null && flight.IsAirborne) ||
            GetComponent<Rigidbody>().isKinematic) return false;
        UsesRemaining--;
        BoostRemaining = Mathf.Max(0.1f, boostDuration);
        if (police != null) police.TryBlindWithPet(GetComponent<Rigidbody>(), blindDuration, cloudRange);
        GetComponent<SceneBaseDriftSmoke>()?.EmitPetCloud();
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsBoosting) { BoostRemaining = 0f; return; }
        BoostRemaining = Mathf.Max(0f, BoostRemaining - Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        BoostRemaining = 0f;
    }

}
