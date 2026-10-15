using UnityEngine;
using TMPro;

public class BusFlightHUD : MonoBehaviour
{
    [Header("References")]
    public BusFlightController flightController;
    public TMP_Text timerText;

    private void Update()
    {
        if (flightController == null ||
            timerText == null)
        {
            return;
        }

        bool flying = flightController.IsAirborne;

        // Affiche ou masque le compteur.
        if (timerText.enabled != flying)
        {
            timerText.enabled = flying;
        }

        if (!flying)
            return;

        // Fin du bonus : chute libre.
        if (flightController.IsFalling)
        {
            timerText.text = "BUS FIGHTER - FREE FALL";
            return;
        }

        timerText.text = CroustyInput.UsesController ? "BUS FIGHTER - PRESS X TO STOP" : "BUS FIGHTER - PRESS E TO STOP";
    }
}
