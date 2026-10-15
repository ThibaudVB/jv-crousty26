using UnityEngine;

// Inertie à la fin du pouvoir de vol : quand le bus retouche le sol en allant plus vite que sa vitesse max,
// il garde son élan et ralentit doucement, au lieu d'être freiné d'un coup à 90 km/h.
// Freiner (S) ou le frein à main coupent l'inertie comme d'habitude.
//
// À mettre sur le bus, à côté de BusFlightController et BusWheelRig.
[RequireComponent(typeof(BusWheelRig))]
[DefaultExecutionOrder(-100)] // avant BusController, pour que l'inertie soit prête dès l'atterrissage
public class BusFlightInertia : MonoBehaviour
{
    [Tooltip("Ralentissement après l'atterrissage (km/h perdus par seconde). Plus petit = plus d'inertie.")]
    [Min(1f)] public float slowdownKmhPerSecond = 6f;

    private BusWheelRig wheelRig;
    private BusFlightController flight;
    private BusController bus;
    private Rigidbody body;
    private bool active;

    private void Awake()
    {
        wheelRig = GetComponent<BusWheelRig>();
        flight = GetComponent<BusFlightController>();
        bus = GetComponent<BusController>();
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (wheelRig == null || flight == null) return;

        // Pendant le vol (et la chute qui suit), l'inertie est déjà prête pour l'atterrissage
        if (flight.IsAirborne)
            active = true;
        // Revenu au sol à une vitesse normale : conduite habituelle
        else if (active && bus != null && body != null &&
                 Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude * 3.6f <= bus.EffectiveMaxSpeedKmh + 1f)
            active = false;

        wheelRig.inertiaDeceleration = active ? slowdownKmhPerSecond / 3.6f : 0f;
    }

    private void OnDisable()
    {
        active = false;
        if (wheelRig != null) wheelRig.inertiaDeceleration = 0f;
    }
}
