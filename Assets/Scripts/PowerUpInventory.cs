using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PowerUpInventory : MonoBehaviour
{
    [System.Serializable]
    public sealed class Slot
    {
        [Tooltip("L'Image du canvas (Atouts / SecondAtouts)")]
        public Image image;
        public Sprite filledSprite;
        public Sprite emptySprite;
        [Tooltip("Optionnel : affiche le nombre de charges")]
        public Text counter;
        [System.NonSerialized] public int charges;

        public void Refresh()
        {
            bool filled = charges > 0;
            if (image != null)
            {
                var sprite = filled ? filledSprite : emptySprite;
                if (sprite != null) image.sprite = sprite;
            }
            if (counter != null) counter.text = filled ? charges.ToString() : "";
        }
    }

    public Slot primary = new Slot();
    public Slot secondary = new Slot();
    [Range(0, 3)] public int startingCharges = 0;

    public bool HasCharge => primary != null && primary.charges > 0;
    public int TotalCharges => (primary != null ? primary.charges : 0) +
                               (secondary != null ? secondary.charges : 0);
    public bool CanAddCharges => TotalCharges < 3;

    private void Awake()
    {
        Initialize(startingCharges);
    }

    public void Initialize(int charges)
    {
        if (primary == null) primary = new Slot();
        if (secondary == null) secondary = new Slot();
        primary.charges = Mathf.Clamp(charges, 0, 3);
        secondary.charges = 0;
        RefreshAll();
    }

    // Appelé par les Power Box. Renvoie false si aucun emplacement n'est disponible.
    public bool TryAdd(int amount)
    {
        if (amount <= 0 || TotalCharges + amount > 3) return false;
        if (primary.charges <= 0) primary.charges = amount;
        else if (primary.charges + amount <= 3) primary.charges += amount;
        else if (secondary.charges <= 0) secondary.charges = amount;
        else if (secondary.charges + amount <= 3) secondary.charges += amount;
        else return false;
        RefreshAll();
        return true;
    }

    // Appelé par BusPetController quand le power-up est utilisé.
    public void Consume()
    {
        if (primary.charges <= 0) return;
        primary.charges--;
        if (primary.charges <= 0)
        {
            // Le slot 2 passe dans le slot 1
            primary.charges = secondary.charges;
            secondary.charges = 0;
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        primary?.Refresh();
        secondary?.Refresh();
    }
}