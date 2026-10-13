using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates the four bonus cards authored on the delivery HUD canvas.</summary>
public sealed class CroustyBonusHUD : MonoBehaviour
{
    public enum BonusLanguage { English, French }
    public enum SelectedBonus { Pet, GPS, Rice, Flight }
    [SerializeField] private bool singleCardMode;
    [SerializeField] private SelectedBonus selectedBonus = SelectedBonus.Pet;

    public void SelectBonus(SelectedBonus value) { selectedBonus = value; Refresh(); }

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

    // Ready for the rice mechanic when it is implemented.
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
        if (singleCardMode)
        {
            if (petController != null) petController.BonusEquipped = selectedBonus == SelectedBonus.Pet;
            if (gpsController != null) gpsController.BonusEquipped = selectedBonus == SelectedBonus.GPS;
            if (flightController != null) flightController.BonusEquipped = selectedBonus == SelectedBonus.Flight;
            ShowSelected(pet, SelectedBonus.Pet, petController != null ? petController.UsesRemaining : 0);
            ShowSelected(gps, SelectedBonus.GPS, gpsController != null ? gpsController.Charges : 0);
            ShowSelected(rice, SelectedBonus.Rice, riceCharges);
            ShowSelected(flight, SelectedBonus.Flight, flightController != null ? flightController.UsesRemaining : 0);
        }
    }

    private void OnDisable()
    {
        if (!singleCardMode) return;
        if (petController != null) petController.BonusEquipped = true;
        if (gpsController != null) gpsController.BonusEquipped = true;
        if (flightController != null) flightController.BonusEquipped = true;
    }

    private void ShowSelected(Card card, SelectedBonus kind, int charges)
    {
        if (card.artwork != null) card.artwork.transform.parent.gameObject.SetActive(selectedBonus == kind && charges > 0);
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
