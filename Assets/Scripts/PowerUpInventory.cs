using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PowerUpInventory : MonoBehaviour
{
    public enum PowerUpType { Pet, GPS, Rice, Flight }

    public static int ChargeCapacity(PowerUpType powerUp) =>
        powerUp == PowerUpType.Pet || powerUp == PowerUpType.Rice ? 3 : 1;

    [System.Serializable]
    public sealed class Slot
    {
        [Tooltip("L'Image du canvas (Atouts / SecondAtouts)")]
        public Image image;
        public Sprite filledSprite;
        public Sprite emptySprite;
        [Tooltip("Optionnel : affiche le nombre de charges")]
        public Text counter;
        public PowerUpType powerUp;
        [System.NonSerialized] public bool occupied;
        [System.NonSerialized] public int charges;

        public void Refresh()
        {
            bool filled = occupied && charges > 0;
            if (image != null)
            {
                var sprite = filled ? filledSprite : emptySprite;
                if (sprite != null) image.sprite = sprite;
            }
            if (counter != null) counter.text = filled ? charges.ToString() : "";
        }
    }

    // A randomized bag shared by all boxes for this bus prevents repeated two-item streaks.
    private readonly System.Collections.Generic.List<CroustyBonusHUD.SelectedBonus> dropBag =
        new System.Collections.Generic.List<CroustyBonusHUD.SelectedBonus>();

    public int ChooseBoxReward(System.Collections.Generic.List<CroustyBonusHUD.SelectedBonus> eligible)
    {
        if (eligible.Count == 0) return -1;
        var choices = new System.Collections.Generic.List<CroustyBonusHUD.SelectedBonus>();
        foreach (var type in eligible) if (dropBag.Contains(type)) choices.Add(type);
        if (choices.Count == 0)
        {
            dropBag.Clear();
            dropBag.AddRange(eligible);
            choices.AddRange(eligible);
        }
        var selected = choices[Random.Range(0, choices.Count)];
        dropBag.Remove(selected);
        return eligible.IndexOf(selected);
    }

    public Slot primary = new Slot();
    public Slot secondary = new Slot();
    [Range(0, 3)] public int startingCharges = 0;

    public bool HasCharge => primary != null && primary.charges > 0;
    public PowerUpType? PrimaryPower => primary != null && primary.occupied ? primary.powerUp : (PowerUpType?)null;
    public int TotalCharges => (primary != null ? primary.charges : 0) + (secondary != null ? secondary.charges : 0);
    public bool CanAddCharges => !primary.occupied || !secondary.occupied;

    public static PowerUpInventory EnsureFor(GameObject owner)
    {
        PowerUpInventory inventory = owner.GetComponent<PowerUpInventory>();
        if (inventory == null) inventory = owner.AddComponent<PowerUpInventory>();
        inventory.EnsureSlots();
        return inventory;
    }

    public void Initialize()
    {
        EnsureSlots();
        Clear(primary);
        Clear(secondary);
        RefreshAll();
    }

    private void EnsureSlots()
    {
        if (primary == null) primary = new Slot();
        if (secondary == null) secondary = new Slot();
    }

    public bool HasPower(PowerUpType powerUp)
    {
        return (primary.occupied && primary.powerUp == powerUp) ||
               (secondary.occupied && secondary.powerUp == powerUp);
    }

    public bool CanAddPower(PowerUpType powerUp)
    {
        Slot existing = FindPower(powerUp);
        return existing != null ? existing.charges < ChargeCapacity(powerUp) :
            !primary.occupied || !secondary.occupied;
    }

    public PowerUpType? GetPowerAt(int index)
    {
        Slot slot = GetSlot(index);
        return slot != null && slot.occupied ? slot.powerUp : (PowerUpType?)null;
    }

    public int GetChargesAt(int index)
    {
        Slot slot = GetSlot(index);
        return slot != null && slot.occupied ? slot.charges : 0;
    }

    public bool TryAddPower(PowerUpType powerUp, int amount)
    {
        if (amount <= 0) return false;

        Slot slot = FindPower(powerUp) ?? (!primary.occupied ? primary : !secondary.occupied ? secondary : null);
        if (slot == null || amount > ChargeCapacity(powerUp) - (slot.occupied ? slot.charges : 0)) return false;
        slot.powerUp = powerUp;
        slot.occupied = true;
        slot.charges += amount;
        RefreshAll();
        return true;
    }

    public bool TryActivatePrimary()
    {
        var activeFlight = GetComponent<BusFlightController>();
        if (activeFlight != null && activeFlight.CurrentMode == BusFlightController.FlightMode.Flying)
            return activeFlight.StopFlight();
        if (!HasCharge) return false;
        switch (primary.powerUp)
        {
            case PowerUpType.Pet:
                BusPetController pet = GetComponent<BusPetController>();
                return pet != null && pet.TryActivate();
            case PowerUpType.GPS:
                BusGPS gps = GetComponent<BusGPS>();
                if (gps == null)
                    gps = FindAnyObjectByType<BusGPS>();
                return gps != null && gps.SelectNext();
            case PowerUpType.Rice:
                BusRiceThrower rice = GetComponent<BusRiceThrower>();
                return rice != null && rice.TryActivate();
            case PowerUpType.Flight:
                BusFlightController flight = GetComponent<BusFlightController>();
                if (flight == null) return false;
                int chargesBeforeActivation = flight.UsesRemaining;
                flight.ActivateFlight();
                return flight.UsesRemaining < chargesBeforeActivation;
            default:
                return false;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || CroustyPauseMenu.ToggledThisFrame || CroustyInput.Keyboard == null)
            return;
        if (CroustyInput.Keyboard.tabKey.wasPressedThisFrame)
            SwapSlots();
        if (CroustyInput.Keyboard.eKey.wasPressedThisFrame)
            TryActivatePrimary();
    }

    // Keep the visual slot objects in place; only exchange their contents.
    public bool SwapSlots()
    {
        if (Time.timeScale <= 0f || !secondary.occupied) return false;
        var type = primary.powerUp;
        bool occupied = primary.occupied;
        int charges = primary.charges;
        primary.powerUp = secondary.powerUp;
        primary.occupied = secondary.occupied;
        primary.charges = secondary.charges;
        secondary.powerUp = type;
        secondary.occupied = occupied;
        secondary.charges = charges;
        RefreshAll();
        return true;
    }

    private Slot FindPower(PowerUpType powerUp) =>
        primary.occupied && primary.powerUp == powerUp ? primary :
        secondary.occupied && secondary.powerUp == powerUp ? secondary : null;

    public void Consume(PowerUpType powerUp)
    {
        Slot slot = primary.occupied && primary.powerUp == powerUp ? primary :
                    secondary.occupied && secondary.powerUp == powerUp ? secondary : null;
        if (slot == null || slot.charges <= 0) return;

        slot.charges--;
        if (slot.charges == 0)
        {
            Clear(slot);
            if (slot == primary && secondary.occupied)
            {
                primary.powerUp = secondary.powerUp;
                primary.occupied = true;
                primary.charges = secondary.charges;
                Clear(secondary);
            }
        }
        RefreshAll();
    }

    private Slot GetSlot(int index) => index == 0 ? primary : index == 1 ? secondary : null;

    private static void Clear(Slot slot)
    {
        slot.occupied = false;
        slot.charges = 0;
    }

    private void RefreshAll()
    {
        primary?.Refresh();
        secondary?.Refresh();
    }
}
