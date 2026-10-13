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
    [Range(0, 3)] public int startingCharges = 3;
    public int UsesRemaining { get; private set; } = 3;
    public bool BonusEquipped { get; set; } = true;
    [Min(1f)] public float flightDuration = 15f;

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
    public float RemainingFlightSeconds => timeRemaining;

    private Rigidbody rb;
    private BusController busController;
    private BusWheelRig wheelRig;

    private WheelCollider[] wheelColliders;
    private bool[] originalWheelStates;

    private bool originalBusEnabled;
    private bool originalWheelRigEnabled;
    private bool originalGravity;

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
        UsesRemaining = startingCharges;
        rb = GetComponent<Rigidbody>();
        busController = GetComponent<BusController>();
        wheelRig = GetComponent<BusWheelRig>();
        if (GetComponent<BusFallSpeedEffect>() == null)
            gameObject.AddComponent<BusFallSpeedEffect>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            turnInput = pitchInput = 0f;
            boostInput = false;
            return;
        }

        // Chaque envol consomme une charge.
        if (!IsAirborne &&
            keyboard[activationKey].wasPressedThisFrame)
        {
            ActivateFlight();
        }

        if (!IsAirborne)
            return;

        bool left =
            keyboard.qKey.isPressed ||
            keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed;

        bool right =
            keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed;

        bool noseDown =
            keyboard.zKey.isPressed ||
            keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed;

        bool noseUp =
            keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed;

        turnInput =
            (right ? 1f : 0f) -
            (left ? 1f : 0f);

        pitchInput =
            (noseUp ? 1f : 0f) -
            (noseDown ? 1f : 0f);

        boostInput = keyboard.spaceKey.isPressed;
    }

    private void FixedUpdate()
    {
        if (!IsAirborne)
            return;

        float dt = Time.fixedDeltaTime;

        // Les 15 secondes sont entierement pilotables.
        if (mode == FlightMode.Flying)
        {
            timeRemaining = Mathf.Max(
                0f,
                timeRemaining - dt
            );

            if (timeRemaining <= 0f)
            {
                StartFalling();
                return;
            }
        }

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

    private void ActivateFlight()
    {
        if (!BonusEquipped || IsAirborne || UsesRemaining <= 0 || !isActiveAndEnabled || Time.timeScale <= 0f ||
            rb.isKinematic || !busController.enabled)
            return;

        // Sauvegarde des reglages physiques du bus.
        originalBusEnabled = busController.enabled;

        originalWheelRigEnabled =
            wheelRig != null && wheelRig.enabled;

        originalGravity = rb.useGravity;
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

        timeRemaining = Mathf.Max(0.1f, flightDuration);
        CroustySoundDesignManager.Play(CroustySound.Flight);

        boostRemaining = boostDuration;
        // Une impulsion unique degage les roues, sans trajectoire ni delai imposes.
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, launchUpSpeed);
        rb.linearVelocity = velocity;
        UsesRemaining--;
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
        RestoreWheelColliders();
    }

    private void CheckGroundContact()
    {
        // Wheel suspension reports real contact, not a raycast landing above the road.
        if (wheelColliders == null) return;
        foreach (var wheel in wheelColliders)
        {
            if (wheel != null && wheel.enabled && wheel.GetGroundHit(out var hit) &&
                IsGroundSurface(hit.collider, hit.normal))
            {
                FinishLanding(hit.point, hit.normal, hit.collider);
                return;
            }
        }
        velocityBeforeFallStep = rb.linearVelocity;
    }

    private bool IsGroundSurface(Collider surface, Vector3 normal)
    {
        return surface != null && surface.attachedRigidbody != rb &&
            (groundLayers.value & (1 << surface.gameObject.layer)) != 0 && normal.y >= 0.5f;
    }

    private void OnCollisionEnter(Collision collision) => CheckBodyContact(collision);
    private void OnCollisionStay(Collision collision) => CheckBodyContact(collision);

    private void CheckBodyContact(Collision collision)
    {
        if (mode != FlightMode.Falling) return;
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
        if (mode != FlightMode.Falling) return;
        Vector3 surfaceVelocity = surface.attachedRigidbody != null
            ? surface.attachedRigidbody.GetPointVelocity(point) : Vector3.zero;
        float speed = Mathf.Max(0f, -Vector3.Dot(velocityBeforeFallStep - surfaceVelocity, normal));
        float strength = Mathf.InverseLerp(minimumImpactSpeed,
            Mathf.Max(minimumImpactSpeed + 0.1f, fullImpactSpeed), speed);
        mode = FlightMode.Ground;
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
    }

    // =====================================================
    // RESTAURATION DE LA PHYSIQUE TERRESTRE
    // =====================================================

    private void RestoreGroundPhysics()
    {
        if (!physicsSaved)
            return;

        rb.useGravity = originalGravity;
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
