using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates the four bonus cards authored on the delivery HUD canvas.</summary>
public sealed class CroustyBonusHUD : MonoBehaviour
{
    public enum BonusLanguage { English, French }

    [Serializable]
    public sealed class Card
    {
        public Image artwork;
        public TMP_Text nameText;
        public TMP_Text countText;
        public string englishName;
        public string frenchName;
        [HideInInspector] public int previewCharges = 3;
    }

    [Header("Cartes placées dans la scène")]
    [SerializeField] private Card gps = new Card { englishName = "GPS", frenchName = "GPS" };
    [SerializeField] private Card pet = new Card { englishName = "PET", frenchName = "PET" };
    [SerializeField] private Card rice = new Card { englishName = "RICE", frenchName = "RIZ" };
    [SerializeField] private Card flight = new Card { englishName = "FLIGHT", frenchName = "VOL" };
    [SerializeField] private Material grayMaterial;
    [SerializeField] private BonusLanguage language = BonusLanguage.English;
    [SerializeField, Range(0, 3)] private int riceCharges = 3;
    [SerializeField] private BusGPS gpsController;
    [SerializeField] private BusPetController petController;
    [SerializeField] private BusFlightController flightController;

    private static readonly Color ReadyTint = Color.white;
    private static readonly Color EmptyTint = new Color(0.7f, 0.7f, 0.7f, 0.76f);

    private void Awake()
    {
        if (gpsController == null) gpsController = FindAnyObjectByType<BusGPS>();
        if (petController == null) petController = FindAnyObjectByType<BusPetController>();
        if (flightController == null) flightController = FindAnyObjectByType<BusFlightController>();
        SetLanguage(language);
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void SetLanguage(BonusLanguage value)
    {
        language = value;
        foreach (Card card in new[] { gps, pet, rice, flight })
            if (card.nameText != null)
                card.nameText.text = language == BonusLanguage.English ? card.englishName : card.frenchName;
    }

    // Called by BusRiceThrower when the rice charges change.
    public void SetRiceCharges(int count)
    {
        riceCharges = Mathf.Clamp(count, 0, 3);
        Refresh();
    }

    public void Refresh()
    {
        Show(gps, gpsController != null ? gpsController.Charges : 3);
        Show(pet, petController != null ? petController.UsesRemaining : 3);
        Show(rice, riceCharges);
        Show(flight, flightController != null ? flightController.UsesRemaining : 3);
    }

    private void Show(Card card, int charges)
    {
        if (card.countText != null) card.countText.text = "x" + Mathf.Max(0, charges);
        if (card.artwork != null)
        {
            card.artwork.material = charges > 0 ? null : grayMaterial;
            card.artwork.color = charges > 0 ? ReadyTint : EmptyTint;
        }
        if (card.nameText != null) card.nameText.color = charges > 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        if (card.countText != null) card.countText.color = charges > 0 ? Color.white : new Color(0.68f, 0.68f, 0.68f);
    }
}
