using UnityEngine;
using UnityEngine.UI;

namespace V1
{
    // Infos de conduite à l'écran :
    // - compteur de vitesse en bas à droite (orange pendant un boost) ;
    // - jauge de dérapage au-dessus, qui prend la couleur du niveau de boost ;
    // - nombre de passagers en haut à gauche, sous le compteur de crousty ;
    // - alerte police en haut au centre, avec la jauge d'arrestation.
    // Ajouté et construit par CroustyHUD.
    public class BusHUD : MonoBehaviour
    {
        [SerializeField] private int speedFontSize = 64;
        [SerializeField] private int fontSize = 28;
        [SerializeField] private Color boostColor = new Color(1f, 0.6f, 0.1f);
        [SerializeField] private Color policeColor = new Color(0.9f, 0.1f, 0.1f);

        private BusController bus;
        private BusPassengers passengers;
        private BusDriftEffects driftEffects;
        private PoliceChaseManager police;

        private Text speedText;
        private Text driftText;
        private RectTransform driftBar;
        private Image driftFill;
        private Text passengersText;
        private RectTransform policeBar;
        private Image policeFill;
        private Text policeText;

        private int shownSpeed = -1;
        private int shownPassengers = -1;

        private const float DriftBarWidth = 260f;
        private const float PoliceBarWidth = 420f;

        public void Build(Canvas canvas, BusController busController, PoliceChaseManager chase)
        {
            bus = busController;
            police = chase;
            if (bus != null)
            {
                passengers = bus.GetComponent<BusPassengers>();
                driftEffects = bus.GetComponent<BusDriftEffects>();
            }

            Transform root = canvas.transform;
            Font font = JojoVisuals.DefaultFont;
            Vector2 bottomRight = new Vector2(1f, 0f);

            speedText = JojoVisuals.CreateOutlinedText(root, "Vitesse", font,
                bottomRight, new Vector2(-40f, 30f), TextAnchor.LowerRight, speedFontSize);

            driftBar = CreateBar(root, "Jauge derapage", bottomRight, new Vector2(-40f, 115f),
                DriftBarWidth, 14f, out driftFill);
            driftText = JojoVisuals.CreateOutlinedText(root, "Derapage", font,
                bottomRight, new Vector2(-40f, 135f), TextAnchor.LowerRight, fontSize);

            passengersText = JojoVisuals.CreateOutlinedText(root, "Passagers", font,
                new Vector2(0f, 1f), new Vector2(30f, -75f), TextAnchor.UpperLeft, fontSize);

            Vector2 top = new Vector2(0.5f, 1f);
            policeBar = CreateBar(root, "Jauge police", top, new Vector2(0f, -75f),
                PoliceBarWidth, 18f, out policeFill);
            policeFill.color = policeColor;
            policeText = JojoVisuals.CreateOutlinedText(root, "Police", font,
                top, new Vector2(0f, -25f), TextAnchor.UpperCenter, fontSize + 4);
            policeText.color = policeColor;
        }

        private void Update()
        {
            if (speedText == null)
                return;

            UpdateSpeed();
            UpdateDrift();
            UpdatePassengers();
            UpdatePolice();
        }

        private void UpdateSpeed()
        {
            bool visible = bus != null;
            speedText.gameObject.SetActive(visible);
            if (!visible)
                return;

            int speed = Mathf.RoundToInt(bus.SpeedKmh);
            if (speed != shownSpeed)
            {
                shownSpeed = speed;
                speedText.text = $"{speed} km/h";
            }

            speedText.color = bus.IsBoosting ? boostColor : Color.white;
        }

        private void UpdateDrift()
        {
            bool drifting = bus != null && bus.IsDrifting;
            bool boosting = bus != null && bus.IsBoosting;

            driftBar.gameObject.SetActive(drifting);
            driftText.gameObject.SetActive(drifting || boosting);

            if (boosting)
            {
                driftText.text = $"BOOST x{bus.BoostLevel}";
                driftText.color = LevelColor(bus.BoostLevel);
                return;
            }

            if (!drifting)
                return;

            float[] charges = bus.boostChargeTimes;
            float full = charges != null && charges.Length > 0 ? charges[charges.Length - 1] : 1f;
            float ratio = Mathf.Clamp01(bus.DriftTime / Mathf.Max(0.01f, full));

            driftFill.rectTransform.sizeDelta = new Vector2(DriftBarWidth * ratio, driftFill.rectTransform.sizeDelta.y);
            driftFill.color = bus.DriftLevel > 0 ? LevelColor(bus.DriftLevel) : Color.white;

            driftText.text = bus.DriftLevel > 0 ? $"DÉRAPAGE x{bus.DriftLevel}" : "DÉRAPAGE";
            driftText.color = driftFill.color;
        }

        private void UpdatePassengers()
        {
            // Le compteur peut être ajouté au bus pendant la partie (premier arrêt)
            if (passengers == null && bus != null)
                passengers = bus.GetComponent<BusPassengers>();

            int count = passengers != null ? passengers.passengerCount : 0;
            passengersText.gameObject.SetActive(passengers != null);

            if (count != shownPassengers)
            {
                shownPassengers = count;
                passengersText.text = $"Passagers : {count}";
            }
        }

        private void UpdatePolice()
        {
            bool chasing = police != null && police.ChaseStarted;
            policeBar.gameObject.SetActive(chasing);
            policeText.gameObject.SetActive(chasing);

            if (!chasing)
                return;

            float progress = police.CatchProgress01;
            policeFill.rectTransform.sizeDelta = new Vector2(PoliceBarWidth * progress, policeFill.rectTransform.sizeDelta.y);

            // Le texte clignote de plus en plus vite quand la police se rapproche
            float blink = Mathf.Lerp(2f, 8f, progress);
            bool on = police.IsEscaping || Mathf.Repeat(Time.unscaledTime * blink, 1f) < 0.6f;
            policeText.enabled = on;
            policeText.text = police.IsEscaping ? "POLICE — tu les sèmes !" : "POLICE — ACCÉLÈRE !";
        }

        private Color LevelColor(int level)
        {
            if (driftEffects != null && level > 0 && driftEffects.levelColors.Length > 0)
                return driftEffects.levelColors[Mathf.Min(level, driftEffects.levelColors.Length) - 1];
            return boostColor;
        }

        // Barre horizontale : fond sombre + remplissage qui grandit vers la droite
        private static RectTransform CreateBar(Transform parent, string name, Vector2 anchor, Vector2 offset,
            float width, float height, out Image fill)
        {
            GameObject background = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            background.transform.SetParent(parent, false);

            RectTransform rect = background.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(width, height);

            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
            backgroundImage.raycastTarget = false;
            background.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            GameObject fillObject = new GameObject("Remplissage", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(background.transform, false);

            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 0f);

            fill = fillObject.GetComponent<Image>();
            fill.raycastTarget = false;

            background.SetActive(false);
            return rect;
        }
    }
}
