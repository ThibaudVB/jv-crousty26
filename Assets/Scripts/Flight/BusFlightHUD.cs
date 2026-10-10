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
            timerText.text = "BUS FIGHTER - CHUTE";
            return;
        }

        // Compte a rebours.
        int seconds = Mathf.CeilToInt(
            flightController.RemainingFlightSeconds
        );

        timerText.text =
            $"BUS FIGHTER - {seconds} s";
    }
}