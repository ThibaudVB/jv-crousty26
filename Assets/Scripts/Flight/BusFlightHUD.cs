using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class BusFlightHUD : MonoBehaviour
{
    public BusFlightController flightController;
    public TMP_Text timerText;
    private GameObject panel;
    private RectTransform fill;
    private GameObject canvasObject;

    private void Start()
    {
        if (flightController == null) flightController = GetComponent<BusFlightController>();
        canvasObject = new GameObject("Flight duration HUD", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        panel = new GameObject("Flight timer", typeof(RectTransform), typeof(Image));
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
        rect.anchoredPosition = new Vector2(0, 150);
        rect.sizeDelta = new Vector2(360, 64);
        panel.GetComponent<Image>().color = new Color(.08f, .07f, .1f, .9f);
        panel.GetComponent<Image>().raycastTarget = false;

        var track = new GameObject("Duration track", typeof(RectTransform), typeof(Image));
        var trackRect = track.GetComponent<RectTransform>();
        trackRect.SetParent(rect, false);
        trackRect.anchorMin = new Vector2(0, 0);
        trackRect.anchorMax = new Vector2(1, 0);
        trackRect.pivot = new Vector2(.5f, 0);
        trackRect.offsetMin = new Vector2(12, 10);
        trackRect.offsetMax = new Vector2(-12, 20);
        track.GetComponent<Image>().color = new Color(.3f, .28f, .3f);
        track.GetComponent<Image>().raycastTarget = false;
        var bar = new GameObject("Remaining flight", typeof(RectTransform), typeof(Image));
        fill = bar.GetComponent<RectTransform>();
        fill.SetParent(trackRect, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        bar.GetComponent<Image>().color = new Color32(233, 132, 36, 255);
        bar.GetComponent<Image>().raycastTarget = false;

        if (timerText == null)
        {
            var label = new GameObject("Flight seconds", typeof(RectTransform), typeof(TextMeshProUGUI));
            timerText = label.GetComponent<TextMeshProUGUI>();
            var textRect = label.GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 24);
            textRect.offsetMax = new Vector2(-10, -4);
            timerText.fontSize = 23;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            timerText.raycastTarget = false;
        }
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || timerText == null) return;
        bool flying = flightController != null && flightController.isActiveAndEnabled &&
            flightController.CurrentMode == BusFlightController.FlightMode.Flying;
        panel.SetActive(flying);
        timerText.enabled = flying;
        if (!flying) return;
        fill.anchorMax = new Vector2(flightController.RemainingFlightFraction, 1);
        timerText.text = $"FLIGHT  {Mathf.CeilToInt(flightController.RemainingFlightSeconds)} s";
    }

    private void OnDisable()
    {
        if (panel != null) panel.SetActive(false);
        if (timerText != null) timerText.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }
}
