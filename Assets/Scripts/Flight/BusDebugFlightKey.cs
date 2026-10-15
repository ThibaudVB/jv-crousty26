using UnityEngine;
using UnityEngine.InputSystem;

// Touche de test : appuyer sur Y fait voler le bus tout de suite, sans avoir besoin du pouvoir.
// À mettre sur le bus (à côté de BusFlightController). À retirer ou décocher pour la version finale du jeu.
[RequireComponent(typeof(BusFlightController))]
public class BusDebugFlightKey : MonoBehaviour
{
    [Tooltip("Touche qui lance le vol")]
    public Key flyKey = Key.Y;

    private BusFlightController flight;

    private void Awake()
    {
        flight = GetComponent<BusFlightController>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || flight == null || Time.timeScale <= 0f) return;
        if (keyboard[flyKey].wasPressedThisFrame)
            flight.ActivateFlightForTesting();
    }
}
