using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BusController))]
public class BusFlightController : MonoBehaviour
{
    public enum FlightMode
    {
        Ground = 0,
        Flying = 2,
        Falling = 3
    }

    [Header("Activation")]
    public Key activationKey = Key.F;
    [Range(0, 1)] public int startingCharges = 0;
    public int UsesRemaining { get; private set; }
    public bool BonusEquipped { get; set; } = true;
    public bool CanReceiveCharges => UsesRemaining < 1 &&
        (inventory == null || inventory.CanAddPower(PowerUpInventory.PowerUpType.Flight));
    [Range(0.1f, 15f)] public float flightDuration = 15f;

    [Header("Impulsion initiale")]
    [Min(0f)] public float launchUpSpeed = 8f;

    [Header("Vitesse aerienne")]
    public float flightSpeedKmh = 160f;
    public float boostSpeedKmh = 220f;
    public float acceleration = 25f;
    public float boostDuration = 3f;

    [Header("Pilotage")]
    public float yawSpeed = 65f;
    public float pitchSpeed = 45f;
    public float maxPitchUp = 35f;
    public float maxPitchDown = 35f;
    public float automaticPitchLeveling = 8f;

    [Header("Roulis")]
    public float maxBankAngle = 35f;
    public float bankSpeed = 100f;

    [Header("Contact au sol")]
    public LayerMask groundLayers = ~0;

    [Header("Impact visuel (vitesse normale en m/s)")]
    [Min(0f)] public float minimumImpactSpeed = 3f;
    [Min(0.1f)] public float fullImpactSpeed = 22f;
    public event System.Action<float> Landed;
    private Vector3 velocityBeforeFallStep;

    [Header("Debug")]
    [SerializeField] private FlightMode mode = FlightMode.Ground;
    [SerializeField] private float timeRemaining;
    [SerializeField] private float boostRemaining;

    public bool IsAirborne => mode != FlightMode.Ground;
    public bool IsFalling => mode == FlightMode.Falling;
    public FlightMode CurrentMode => mode;
    public float RemainingFlightSeconds => mode == FlightMode.Flying ? timeRemaining : 0f;
    public float FlightDurationSeconds => Mathf.Clamp(flightDuration, .1f, 15f);
    public float RemainingFlightFraction => Mathf.Clamp01(RemainingFlightSeconds / FlightDurationSeconds);
    private float launchedAt;

    private Rigidbody rb;
    private BusController busController;
    private BusWheelRig wheelRig;
    private PowerUpInventory inventory;
    private ProceduralCity city;

    private WheelCollider[] wheelColliders;
    private bool[] originalWheelStates;

    private bool originalBusEnabled;
    private bool originalWheelRigEnabled;
    private bool originalGravity;
    private float originalLinearDamping;

    private RigidbodyConstraints originalConstraints;
    private CollisionDetectionMode originalCollisionMode;

    private bool physicsSaved;

    private float yaw;
    private float pitch;
    private float bank;

    private float turnInput;
    private float pitchInput;
    private bool boostInput;



    private void Awake()
    {
        var hud = GetComponent<BusFlightHUD>();
        if (hud == null) hud = gameObject.AddComponent<BusFlightHUD>();
        hud.flightController = this;
        UsesRemaining = Mathf.Clamp(startingCharges, 0, 1);
        inventory = PowerUpInventory.EnsureFor(gameObject);
        if (inventory != null && UsesRemaining > 0)
            inventory.TryAddPower(PowerUpInventory.PowerUpType.Flight, UsesRemaining);
        rb = GetComponent<Rigidbody>();
        city = FindAnyObjectByType<ProceduralCity>();
        busController = GetComponent<BusController>();
        wheelRig = GetComponent<BusWheelRig>();
        if (GetComponent<BusFallSpeedEffect>() == null)
            gameObject.AddComponent<BusFallSpeedEffect>();
    }

    public bool AddCharges(int amount, int max)
    {
        int addedAmount = Mathf.Min(amount, Mathf.Min(max, 1) - UsesRemaining);
        if (addedAmount <= 0) return false;
        if (inventory != null && !inventory.TryAddPower(PowerUpInventory.PowerUpType.Flight, addedAmount))
            return false;
        UsesRemaining += addedAmount;
        BonusEquipped = true;
        return true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || CroustyPauseMenu.ToggledThisFrame)
            return;

        Keyboard keyboard = CroustyInput.Keyboard;

        var pad = CroustyInput.Pad;

        if (!IsAirborne)
            return;

        bool left = keyboard != null && (keyboard.qKey.isPressed ||
            keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed);

        bool right = keyboard != null && (keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed);

        bool noseDown = keyboard != null && (keyboard.zKey.isPressed ||
            keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed);

        bool noseUp = keyboard != null && (keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed);

        turnInput =
            (right ? 1f : 0f) -
            (left ? 1f : 0f);

        pitchInput =
            (noseUp ? 1f : 0f) -
            (noseDown ? 1f : 0f);

        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > Mathf.Abs(turnInput)) turnInput = stick.x;
            if (Mathf.Abs(stick.y) > Mathf.Abs(pitchInput)) pitchInput = -stick.y;
        }
        boostInput = (keyboard != null && keyboard.spaceKey.isPressed) ||
            (pad != null && (pad.buttonSouth.isPressed || pad.rightTrigger.isPressed));
    }

    private void FixedUpdate()
    {
        if (!IsAirborne || Time.timeScale <= 0f)
            return;

        if (mode == FlightMode.Flying && Time.time - launchedAt >= .35f)
        {
            CheckWheelContact();
            if (!IsAirborne) return;
        }
        float dt = Time.fixedDeltaTime;
        if (mode == FlightMode.Flying)
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - dt);
            if (timeRemaining <= 0f) StartFalling();
            // Streaming may delay steering, but must never erase airborne momentum or stop the timer.
            else if (city != null && !city.IsMovementReady(rb.position, rb.linearVelocity)) return;
        }
        if (mode == FlightMode.Flying) velocityBeforeFallStep = rb.linearVelocity;

        switch (mode)
        {
            case FlightMode.Flying:
                UpdateFlying(dt);
                break;

            case FlightMode.Falling:
                CheckGroundContact();
                break;
        }
    }

    // =====================================================
    // ACTIVATION DU BONUS
    // =====================================================

    public void ActivateFlight()
    {
        if (IsAirborne || UsesRemaining <= 0 || !isActiveAndEnabled || Time.timeScale <= 0f ||
            (inventory != null && inventory.PrimaryPower != PowerUpInventory.PowerUpType.Flight) ||
            rb.isKinematic || !busController.enabled)
            return;

        // Sauvegarde des reglages physiques du bus.
        originalBusEnabled = busController.enabled;

        originalWheelRigEnabled =
            wheelRig != null && wheelRig.enabled;

        originalGravity = rb.useGravity;
        originalLinearDamping = rb.linearDamping;
        originalConstraints = rb.constraints;
        originalCollisionMode = rb.collisionDetectionMode;

        physicsSaved = true;

        // Arrete le controleur terrestre.
        busController.enabled = false;

        // On arrete le WheelRig AVANT les WheelColliders.
        // Son OnDisable appelle ResetWheels().
        if (wheelRig != null)
            wheelRig.enabled = false;

        wheelColliders =
            GetComponentsInChildren<WheelCollider>(true);

        originalWheelStates =
            new bool[wheelColliders.Length];

        for (int i = 0; i < wheelColliders.Length; i++)
        {
            originalWheelStates[i] =
                wheelColliders[i].enabled;

            wheelColliders[i].enabled = false;
        }

        // Le mode avion prend le controle du Rigidbody.
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        rb.angularVelocity = Vector3.zero;

        yaw = rb.rotation.eulerAngles.y;
        pitch = Mathf.Clamp(-Mathf.DeltaAngle(0f, rb.rotation.eulerAngles.x), -maxPitchDown, maxPitchUp);
        bank = 0f;

        turnInput = 0f;
        pitchInput = 0f;
        boostInput = false;

        timeRemaining = FlightDurationSeconds;
        launchedAt = Time.time;

        boostRemaining = boostDuration;
        // Une impulsion unique degage les roues, sans trajectoire ni delai imposes.
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, launchUpSpeed);
        rb.linearVelocity = velocity;
        UsesRemaining--;
        inventory?.Consume(PowerUpInventory.PowerUpType.Flight);
        mode = FlightMode.Flying;
    }

    // =====================================================
    // PILOTAGE AERIEN
    // =====================================================

    private void UpdateFlying(float dt)
    {
        // Direction horizontale.
        yaw += turnInput * yawSpeed * dt;

        // Monter ou descendre.
        pitch += pitchInput * pitchSpeed * dt;

        pitch = Mathf.Clamp(
            pitch,
            -maxPitchDown,
            maxPitchUp
        );

        // Retour progressif vers l'horizontale.
        if (Mathf.Abs(pitchInput) < 0.01f)
        {
            pitch = Mathf.MoveTowards(
                pitch,
                0f,
                automaticPitchLeveling * dt
            );
        }

        // Inclinaison du bus dans les virages.
        float targetBank =
            -turnInput * maxBankAngle;

        bank = Mathf.MoveTowards(
            bank,
            targetBank,
            bankSpeed * dt
        );

        Quaternion targetRotation = Quaternion.Euler(
            -pitch,
            yaw,
            bank
        );

        rb.MoveRotation(targetRotation);

        // Boost limite a 3 secondes cumulees.
        bool boosting =
            boostInput && boostRemaining > 0f;

        if (boosting)
        {
            boostRemaining = Mathf.Max(
                0f,
                boostRemaining - dt
            );
        }

        float targetSpeedKmh = boosting
            ? boostSpeedKmh
            : flightSpeedKmh;

        Vector3 forward =
            targetRotation * Vector3.forward;

        Vector3 targetVelocity =
            forward * (targetSpeedKmh / 3.6f);

        // Acceleration progressive.
        rb.linearVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            acceleration * dt
        );

        rb.angularVelocity = Vector3.zero;
    }

    // =====================================================
    // FIN DU BONUS
    // =====================================================

    private void StartFalling()
    {
        mode = FlightMode.Falling;
        timeRemaining = 0f;
        turnInput = pitchInput = 0f;
        boostInput = false;

        // No glide, velocity cap or auto-leveling: preserve momentum and let gravity act.
        rb.useGravity = true;
        velocityBeforeFallStep = rb.linearVelocity;
        // Keep wheel physics and road controls suspended until actual contact.
        // Re-enabling tires here can apply ground friction before landing is handled.
    }

    public bool StopFlight()
    {
        if (mode != FlightMode.Flying || Time.timeScale <= 0f) return false;
        StartFalling();
        return true;
    }

    private void CheckWheelContact()
    {
        if (wheelColliders == null) return;
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            var wheel = wheelColliders[i];
            if (wheel == null || !originalWheelStates[i]) continue;
            // Airborne modes disable wheel colliders. Probe only the actual tire footprint,
            // not the suspension travel, so touching the road ends flight without snapping down.
            Vector3 center = wheel.transform.TransformPoint(wheel.center);
            float radius = wheel.radius * Mathf.Abs(wheel.transform.lossyScale.y);
            foreach (var hit in Physics.RaycastAll(center, -wheel.transform.up, radius + .03f,
                groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (!IsGroundSurface(hit.collider, hit.normal)) continue;
                velocityBeforeFallStep = rb.linearVelocity;
                FinishLanding(hit.point, hit.normal, hit.collider);
                return;
            }
        }
    }

    private void CheckGroundContact()
    {
        // Probe the tire footprint while wheels remain disabled, just as during flight.
        // Body collision callbacks still handle roof/chassis impacts.
        CheckWheelContact();
        velocityBeforeFallStep = rb.linearVelocity;
    }

    private bool IsGroundSurface(Collider surface, Vector3 normal)
    {
        if (surface == null) return false;
        var prop = surface.GetComponentInParent<MapImpactProp>();
        if (prop != null && prop.passThroughBus) return false;
        return surface.attachedRigidbody != rb &&
            (groundLayers.value & (1 << surface.gameObject.layer)) != 0 && normal.y >= 0.5f;
    }

    private void OnCollisionEnter(Collision collision) => CheckBodyContact(collision);
    private void OnCollisionStay(Collision collision) => CheckBodyContact(collision);

    private void CheckBodyContact(Collision collision)
    {
        if (!IsAirborne || (mode == FlightMode.Flying && Time.time - launchedAt < .35f)) return;
        // Also return control after a chassis/roof impact, so R can recover an overturned bus.
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (IsGroundSurface(contact.otherCollider, contact.normal))
            {
                FinishLanding(contact.point, contact.normal, contact.otherCollider);
                return;
            }
        }
    }

    private void FinishLanding(Vector3 point, Vector3 normal, Collider surface)
    {
        // Do not teleport, straighten or erase the impact velocity.
        if (!IsAirborne) return;
        Vector3 surfaceVelocity = surface.attachedRigidbody != null
            ? surface.attachedRigidbody.GetPointVelocity(point) : Vector3.zero;
        float speed = Mathf.Max(0f, -Vector3.Dot(velocityBeforeFallStep - surfaceVelocity, normal));
        float strength = Mathf.InverseLerp(minimumImpactSpeed,
            Mathf.Max(minimumImpactSpeed + 0.1f, fullImpactSpeed), speed);
        mode = FlightMode.Ground;
        timeRemaining = 0f;
        RestoreGroundPhysics();
        if (strength > 0f)
        {
            GetComponent<SceneBaseDriftSmoke>()?.EmitLandingImpact(point, normal, strength);
            Landed?.Invoke(strength);
        }
    }

    private void RestoreWheelColliders()
    {
        if (wheelColliders == null) return;
        for (int i = 0; i < wheelColliders.Length; i++)
            if (wheelColliders[i] != null)
                wheelColliders[i].enabled = originalWheelStates[i];
        GetComponent<BusPropImpact>()?.IgnoreActiveProps();
    }

    // =====================================================
    // RESTAURATION DE LA PHYSIQUE TERRESTRE
    // =====================================================

    private void RestoreGroundPhysics()
    {
        if (!physicsSaved)
            return;

        rb.useGravity = originalGravity;
        rb.linearDamping = originalLinearDamping;
        rb.constraints = originalConstraints;
        rb.collisionDetectionMode = originalCollisionMode;

        RestoreWheelColliders();

        // Reactiver le WheelRig apres ses colliders.
        if (wheelRig != null)
            wheelRig.enabled = originalWheelRigEnabled;

        if (busController != null)
            busController.enabled = originalBusEnabled;

        physicsSaved = false;
    }

    private void OnDisable()
    {
        RestoreGroundPhysics();
        mode = FlightMode.Ground;
        timeRemaining = boostRemaining = 0f;
        turnInput = pitchInput = 0f;
        boostInput = false;
    }
}
