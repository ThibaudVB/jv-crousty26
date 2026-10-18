using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates the four bonus cards authored on the delivery HUD canvas.</summary>
[DefaultExecutionOrder(-150)]
public sealed class CroustyBonusHUD : MonoBehaviour
{
    public enum BonusLanguage { English, French }
    public enum SelectedBonus { Pet, GPS, Rice, Flight }
    [SerializeField] private bool singleCardMode;
    [SerializeField] private SelectedBonus selectedBonus = SelectedBonus.Pet;
    [SerializeField, Range(0, 1)] private int inventorySlotIndex;
    private PowerUpInventory inventory;

    public void SelectBonus(SelectedBonus value) { selectedBonus = value; Refresh(); }
    public string SelectedKeyLabel => "E";
    public bool HasSelectedBonus => flightController != null && flightController.CurrentMode == BusFlightController.FlightMode.Flying ||
        Inventory != null && Inventory.HasCharge;
    public void CycleBonus(int direction)
    {
        if (direction == 0) return;
        if (Inventory != null)
        {
            Inventory.SwapSlots();
            Refresh();
        }
        else SelectBonus((SelectedBonus)(((int)selectedBonus + direction + 4) % 4));
    }
    public void ActivateSelected()
    {
        Inventory?.TryActivatePrimary();
    }

    [Serializable]
    public sealed class Card
    {
        public Image artwork;
        [System.NonSerialized] public Sprite filledSprite;
        public TMP_Text nameText;
        public TMP_Text countText;
        public string englishName;
        public string frenchName;
        [HideInInspector] public int previewCharges = 3;
    }

    [Header("Cartes placées dans la scène")]
    [SerializeField] private Card gps = new Card { englishName = "Express GPS", frenchName = "Express GPS" };
    [SerializeField] private Card pet = new Card { englishName = "Toxic Gas", frenchName = "Toxic Gas" };
    [SerializeField] private Card rice = new Card { englishName = "Asian Rice", frenchName = "Asian Rice" };
    [SerializeField] private Card flight = new Card { englishName = "Crousty Flight", frenchName = "Crousty Flight" };
    [SerializeField] private Material grayMaterial;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private BonusLanguage language = BonusLanguage.English;
    [SerializeField, Range(0, 3)] private int riceCharges = 0;
    [SerializeField] private BusGPS gpsController;
    [SerializeField] private BusPetController petController;
    [SerializeField] private BusFlightController flightController;
    [SerializeField] private BusRiceThrower riceController;

    private static readonly Color ReadyTint = Color.white;
    private static readonly Color EmptyTint = new Color(0.7f, 0.7f, 0.7f, 0.76f);

    private PowerUpInventory Inventory
    {
        get
        {
            if (inventory == null)
            {
                BusPetController bus = FindAnyObjectByType<BusPetController>();
                if (bus != null) inventory = bus.GetComponent<PowerUpInventory>();
            }
            return inventory;
        }
    }

    private void Awake()
    {
        foreach (Card card in new[] { gps, pet, rice, flight })
            if (card.artwork != null) card.filledSprite = card.artwork.sprite;
        if (gpsController == null) gpsController = FindAnyObjectByType<BusGPS>();
        if (petController == null) petController = FindAnyObjectByType<BusPetController>();
        if (flightController == null) flightController = FindAnyObjectByType<BusFlightController>();
        if (riceController == null) riceController = FindAnyObjectByType<BusRiceThrower>();
        gps.englishName = "Express GPS"; pet.englishName = "Toxic Gas";
        rice.englishName = "Asian Rice"; flight.englishName = "Crousty Flight";
        SetLanguage(language);
        if (rice.nameText != null)
        {
            rice.nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            rice.nameText.verticalAlignment = VerticalAlignmentOptions.Geometry;
            rice.nameText.margin = Vector4.zero;
        }
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Update()
    {
        if (!singleCardMode) return;
    }

    public void SetLanguage(BonusLanguage value)
    {
        language = BonusLanguage.English;
        foreach (Card card in new[] { gps, pet, rice, flight })
            if (card.nameText != null)
            {
                card.nameText.text = card.englishName;
                card.nameText.fontSizeMax = card.nameText.fontSize;
                card.nameText.fontSizeMin = 12;
                card.nameText.enableAutoSizing = true;
            }
    }

    // Updated whenever a rice charge is spent.
    public void SetRiceCharges(int count)
    {
        riceCharges = Mathf.Clamp(count, 0, 3);
        Refresh();
    }

    public void Refresh()
    {
        Show(gps, gpsController != null ? gpsController.Charges : 0);
        Show(pet, petController != null ? petController.UsesRemaining : 0);
        Show(rice, riceCharges);
        Show(flight, flightController != null ? flightController.UsesRemaining : 0);
        if (singleCardMode)
        {
            PowerUpInventory currentInventory = Inventory;
            if (currentInventory != null)
            {
                if (petController != null) petController.BonusEquipped = currentInventory.PrimaryPower == PowerUpInventory.PowerUpType.Pet;
                if (gpsController != null) gpsController.BonusEquipped = currentInventory.PrimaryPower == PowerUpInventory.PowerUpType.GPS;
                if (flightController != null) flightController.BonusEquipped = currentInventory.PrimaryPower == PowerUpInventory.PowerUpType.Flight;
                if (riceController != null) riceController.BonusEquipped = currentInventory.PrimaryPower == PowerUpInventory.PowerUpType.Rice;
                ShowInventorySlot(currentInventory);
            }
            else
            {
                if (petController != null) petController.BonusEquipped = selectedBonus == SelectedBonus.Pet;
                if (gpsController != null) gpsController.BonusEquipped = selectedBonus == SelectedBonus.GPS;
                if (flightController != null) flightController.BonusEquipped = selectedBonus == SelectedBonus.Flight;
                if (riceController != null) riceController.BonusEquipped = selectedBonus == SelectedBonus.Rice;
                ShowSelected(pet, SelectedBonus.Pet);
                ShowSelected(gps, SelectedBonus.GPS);
                ShowSelected(rice, SelectedBonus.Rice);
                ShowSelected(flight, SelectedBonus.Flight);
            }
        }
    }

    private void OnDisable()
    {
        if (!singleCardMode) return;
        if (petController != null) petController.BonusEquipped = true;
        if (gpsController != null) gpsController.BonusEquipped = true;
        if (flightController != null) flightController.BonusEquipped = true;
        if (riceController != null) riceController.BonusEquipped = true;
    }

    private void ShowSelected(Card card, SelectedBonus kind)
    {
        if (card.artwork != null)
            card.artwork.transform.parent.gameObject.SetActive(selectedBonus == kind);
    }

    private void ShowInventorySlot(PowerUpInventory currentInventory)
    {
        PowerUpInventory.PowerUpType? type = currentInventory.GetPowerAt(inventorySlotIndex);
        Card slotCard = type.HasValue ? GetCard(ToSelectedBonus(type.Value)) : pet;

        foreach (Card card in new[] { gps, pet, rice, flight })
            if (card.artwork != null && card.artwork.transform.parent != null)
                card.artwork.transform.parent.gameObject.SetActive(card == slotCard);

        if (type.HasValue)
        {
            Show(slotCard, currentInventory.GetChargesAt(inventorySlotIndex));
            if (slotCard.nameText != null)
                slotCard.nameText.text = slotCard.englishName;
            return;
        }

        if (slotCard.artwork != null)
        {
            slotCard.artwork.sprite = emptySprite != null ? emptySprite : slotCard.filledSprite;
            slotCard.artwork.color = ReadyTint;
            slotCard.artwork.material = null;
        }
        if (slotCard.nameText != null) slotCard.nameText.text = "";
        if (slotCard.countText != null) slotCard.countText.text = "";
    }

    private Card GetCard(SelectedBonus type)
    {
        switch (type)
        {
            case SelectedBonus.GPS: return gps;
            case SelectedBonus.Rice: return rice;
            case SelectedBonus.Flight: return flight;
            default: return pet;
        }
    }

    private static SelectedBonus ToSelectedBonus(PowerUpInventory.PowerUpType type)
    {
        switch (type)
        {
            case PowerUpInventory.PowerUpType.GPS: return SelectedBonus.GPS;
            case PowerUpInventory.PowerUpType.Rice: return SelectedBonus.Rice;
            case PowerUpInventory.PowerUpType.Flight: return SelectedBonus.Flight;
            default: return SelectedBonus.Pet;
        }
    }

    private void Show(Card card, int charges)
    {
        if (card.countText != null) card.countText.text = "" + Mathf.Max(0, charges);
        if (card.artwork != null)
        {
            bool available = charges > 0;
            card.artwork.sprite = available || emptySprite == null ? card.filledSprite : emptySprite;
            card.artwork.material = available || emptySprite != null ? null : grayMaterial;
            card.artwork.color = available || emptySprite != null ? ReadyTint : EmptyTint;
        }
        if (card.nameText != null) card.nameText.color = charges > 0 ? Color.white : new Color(0.65f, 0.65f, 0.65f);
        if (card.countText != null) card.countText.color = charges > 0 ? Color.white : new Color(0.68f, 0.68f, 0.68f);
    }
}
