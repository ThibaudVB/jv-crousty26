using UnityEngine;
using UnityEngine.UI;

// SceneBase-only wiring. The source gameplay components remain reusable in their own scenes.
[DefaultExecutionOrder(1000)]
public sealed class SceneBaseSession : MonoBehaviour
{
    public DeliveryManager deliveries;
    public CroustyHUD deliveryHUD;
    public BusPassengers passengers;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Text passengerText;
    private bool suspendedDeliveries;
    private int displayedCount = -1;

#if UNITY_EDITOR
    public void PrepareUI()
    {
        if (passengerText != null || deliveryHUD == null) return;
        hudCanvas = deliveryHUD.GetComponentInChildren<Canvas>(true);
        if (hudCanvas == null) return;
        var label = new GameObject("Passagers", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(hudCanvas.transform, false);
        passengerText = label.GetComponent<Text>();
        passengerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        passengerText.fontSize = 26;
        passengerText.fontStyle = FontStyle.Bold;
        passengerText.color = Color.white;
        passengerText.alignment = TextAnchor.UpperLeft;
        passengerText.raycastTarget = false;
        var rect = passengerText.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(30, -78);
        rect.sizeDelta = new Vector2(500, 50);
        label.GetComponent<Outline>().effectColor = Color.black;
    }

#endif

    private void LateUpdate()
    {
        bool paused = Time.timeScale <= 0;
        if (paused && deliveries.enabled)
        {
            suspendedDeliveries = true;
            deliveries.enabled = false;
        }
        else if (!paused && suspendedDeliveries)
        {
            suspendedDeliveries = false;
            deliveries.enabled = true;
        }
        if (hudCanvas != null) hudCanvas.enabled = !paused;
        if (passengerText != null && displayedCount != passengers.passengerCount)
        {
            displayedCount = passengers.passengerCount;
            passengerText.text = $"Passagers : {displayedCount}";
        }
    }

    private void OnDisable()
    {
        if (suspendedDeliveries && deliveries != null)
        {
            deliveries.enabled = true;
            suspendedDeliveries = false;
        }
    }
}
