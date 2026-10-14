using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    [Header("Vitesse")]
    public float maxSpeedKmh = 130f;
    public float maxReverseSpeedKmh = 40f;

    [Header("Acceleration")]
    public float acceleration = 32f;
    public float reverseAcceleration = 16f;
    public float naturalDeceleration = 4f;

    [Header("Freinage")]
    public float brakeDeceleration = 50f;
    public float handbrakeDeceleration = 42f;

    [Header("Direction")]
    public float maxSteeringSpeed = 85f;
    public float steeringResponse = 8f;

    [Range(0.1f, 1f)]
    public float highSpeedSteeringMultiplier = 0.55f;

    [Min(1f)]
    public float handbrakeSteeringMultiplier = 1.2f;

    [Header("Adherence")]
    public float lateralGrip = 50f;
    public float handbrakeGrip = 6f;

    [Header("Tolerance aux bordures")]
    [Min(0f)] public float maxStepHeight = 0.6f;
    [Min(0f)] public float stepProbeDistance = 0.15f;
    private BoxCollider busCollider;
    private readonly RaycastHit[] stepHits = new RaycastHit[32];
    private readonly Collider[] stepOverlaps = new Collider[32];

    [Header("Reset vehicule")]
    public Key resetKey = Key.R;
    public float resetHeight = 2f;

    [Header("Debug")]
    [SerializeField] private float currentSpeedKmh;
    [SerializeField] private float signedSpeedKmh;
    [SerializeField] private bool isReversing;
    [SerializeField] private bool isHandbraking;

    private BusPetController pet;
    private BusDamage damage;
    public float EffectiveMaxSpeedKmh => maxSpeedKmh * (pet != null && pet.IsBoosting ? pet.speedMultiplier : 1f)
        * (damage != null ? damage.SpeedMultiplier : 1f);
    public float PetAccelerationMultiplier => pet != null && pet.IsBoosting ? pet.accelerationMultiplier : 1f;
    private Rigidbody rb;
    private BusWheelRig wheelRig;
    private ProceduralCity city;

    private float throttleInput;
    public float ThrottleInput => throttleInput;
    private float steeringInput;
    private float currentSteering;

    private bool handbrakeInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        city = FindAnyObjectByType<ProceduralCity>();
        damage = GetComponent<BusDamage>();
        pet = GetComponent<BusPetController>();
        if (pet == null) pet = gameObject.AddComponent<BusPetController>();
        busCollider = GetComponent<BoxCollider>();
        if (GetComponent<BusPropImpact>() == null)
            gameObject.AddComponent<BusPropImpact>();
        rb.angularVelocity = Vector3.zero;
        wheelRig = GetComponent<BusWheelRig>();
        if (wheelRig != null)
        {
            wheelRig.ApplyArcadeTuningUpdate(this);
            wheelRig.Initialize();
        }
        // Le resultat reste identique quel que soit l'ordre des Awake des deux composants.
        if (wheelRig != null && wheelRig.IsReady)
            rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        else
            rb.constraints |= RigidbodyConstraints.FreezeRotationY;
        if (GetComponent<BusNightLights>() == null) gameObject.AddComponent<BusNightLights>();
    }

    private void OnDisable()
    {
        throttleInput = steeringInput = currentSteering = 0f;
        handbrakeInput = isHandbraking = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || CroustyPauseMenu.ToggledThisFrame) return;
        ReadInputs();

        currentSteering = Mathf.MoveTowards(
            currentSteering,
            steeringInput,
            steeringResponse * Time.deltaTime
        );

        // RESET DU BUS
        if ((CroustyInput.Keyboard != null && CroustyInput.Keyboard[resetKey].wasPressedThisFrame) ||
            (CroustyInput.Pad != null && CroustyInput.Pad.buttonNorth.wasPressedThisFrame))
        {
            ResetVehicle();
        }
    }

    private void FixedUpdate()
    {
        if (city != null && !city.IsMovementReady(rb.position, rb.linearVelocity))
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            if (wheelRig != null && wheelRig.IsReady) wheelRig.Drive(0f, 0f, true, this);
            return;
        }
        UpdateDebug();
        // PET supplies thrust; braking and handbrake retain priority.
        float driveThrottle = pet != null && pet.IsBoosting && throttleInput >= 0f && !handbrakeInput
            ? 1f : throttleInput;

        if (wheelRig != null && wheelRig.IsReady)
        {
            wheelRig.Drive(driveThrottle, steeringInput, handbrakeInput, this);
            ApplyCurbTolerance(rb.rotation);
            return;
        }

        float originalThrottle = throttleInput;
        throttleInput = driveThrottle;
        ApplyLongitudinalMovement();
        throttleInput = originalThrottle;
        Quaternion steeringRotation = ApplySteering();
        ApplyLateralGrip(steeringRotation);
        ApplyCurbTolerance(steeringRotation);
    }

    private void ReadInputs()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        handbrakeInput = false;

        var pad = CroustyInput.Pad;
        if (pad != null)
        {
            throttleInput = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            steeringInput = pad.leftStick.ReadValue().x;
            handbrakeInput = pad.buttonSouth.isPressed;
        }

        if (CroustyInput.Keyboard == null)
            return;

        // AVANCER
        if (
            CroustyInput.Keyboard.zKey.isPressed ||
            CroustyInput.Keyboard.wKey.isPressed ||
            CroustyInput.Keyboard.upArrowKey.isPressed
        )
        {
            throttleInput = 1f;
        }

        // FREIN / MARCHE ARRIERE
        if (
            CroustyInput.Keyboard.sKey.isPressed ||
            CroustyInput.Keyboard.downArrowKey.isPressed
        )
        {
            throttleInput = -1f;
        }

        // GAUCHE
        if (
            CroustyInput.Keyboard.qKey.isPressed ||
            CroustyInput.Keyboard.aKey.isPressed ||
            CroustyInput.Keyboard.leftArrowKey.isPressed
        )
        {
            steeringInput = -1f;
        }

        // DROITE
        if (
            CroustyInput.Keyboard.dKey.isPressed ||
            CroustyInput.Keyboard.rightArrowKey.isPressed
        )
        {
            steeringInput = 1f;
        }

        // FREIN A MAIN
        handbrakeInput |= CroustyInput.Keyboard.spaceKey.isPressed;
    }

    private void ApplyLongitudinalMovement()
    {
        Vector3 localVelocity =
            Quaternion.Inverse(rb.rotation) * rb.linearVelocity;

        float forwardSpeed = localVelocity.z;

        float maxForwardSpeed = EffectiveMaxSpeedKmh / 3.6f;
        float maxReverseSpeed = maxReverseSpeedKmh / 3.6f;

        // FREIN A MAIN
        if (handbrakeInput)
        {
            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                0f,
                handbrakeDeceleration * Time.fixedDeltaTime
            );
        }

        // AVANCER
        else if (throttleInput > 0f)
        {
            // Si on recule, Z freine d'abord
            if (forwardSpeed < -0.5f)
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    0f,
                    brakeDeceleration * Time.fixedDeltaTime
                );
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    maxForwardSpeed,
                    acceleration * PetAccelerationMultiplier * Time.fixedDeltaTime
                );
            }
        }

        // FREIN / MARCHE ARRIERE
        else if (throttleInput < 0f)
        {
            // Si on avance, S freine d'abord
            if (forwardSpeed > 0.5f)
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    0f,
                    brakeDeceleration * Time.fixedDeltaTime
                );
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    -maxReverseSpeed,
                    reverseAcceleration * Time.fixedDeltaTime
                );
            }
        }

        // ROUE LIBRE
        else
        {
            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                0f,
                naturalDeceleration * Time.fixedDeltaTime
            );
        }

        localVelocity.z = forwardSpeed;

        Vector3 worldVelocity =
            rb.rotation * localVelocity;

        // Laisse Unity gérer la chute / gravité
        worldVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = worldVelocity;
    }

    private Quaternion ApplySteering()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, rb.rotation * Vector3.forward);
        float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

        // Une direction vive des les premiers metres, sans pivoter a l'arret.
        if (speedKmh < 0.5f)
            return rb.rotation;

        float lowSpeedFactor = Mathf.Clamp01(speedKmh / 5f);
        float highSpeedFactor = Mathf.Lerp(
            1f,
            highSpeedSteeringMultiplier,
            Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxSpeedKmh))
        );
        float reverseDirection = forwardSpeed >= 0f ? 1f : -1f;
        float handbrakeFactor = handbrakeInput ? handbrakeSteeringMultiplier : 1f;
        float rotationAmount = currentSteering * maxSteeringSpeed * lowSpeedFactor *
            highSpeedFactor * reverseDirection * handbrakeFactor * Time.fixedDeltaTime;
        Quaternion turn = Quaternion.Euler(0f, rotationAmount, 0f);
        Quaternion targetRotation = turn * rb.rotation;

        // La vitesse suit le virage pour garder une trajectoire arcade precise.
        // Le frein a main laisse une partie de l'inertie pour deraper volontairement.
        float velocityFollow = handbrakeInput
            ? Mathf.Clamp01(handbrakeGrip / Mathf.Max(0.01f, lateralGrip))
            : 1f;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        horizontalVelocity = Quaternion.Slerp(Quaternion.identity, turn, velocityFollow) * horizontalVelocity;
        rb.linearVelocity = horizontalVelocity + Vector3.up * rb.linearVelocity.y;
        rb.MoveRotation(targetRotation);
        return targetRotation;
    }

    private void ApplyLateralGrip(Quaternion steeringRotation)
    {
        // Utilise la rotation demandee pour ce pas physique, meme avec interpolation.
        Vector3 localVelocity = Quaternion.Inverse(steeringRotation) * rb.linearVelocity;
        float grip = handbrakeInput ? handbrakeGrip : lateralGrip;
        // Accroche immediate hors frein a main, meme apres une impulsion de collision.
        localVelocity.x = handbrakeInput
            ? Mathf.MoveTowards(localVelocity.x, 0f, grip * Time.fixedDeltaTime)
            : 0f;
        Vector3 correctedVelocity = steeringRotation * localVelocity;
        correctedVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = correctedVelocity;
    }

    private bool IsStaticObstacle(Collider candidate)
        => candidate != null && candidate.attachedRigidbody == null &&
           !candidate.transform.IsChildOf(transform);

    private bool CastStepBox(Vector3 center, Vector3 extents, Quaternion rotation,
        Vector3 direction, float distance, out RaycastHit nearest)
    {
        nearest = default;
        float closest = float.PositiveInfinity;
        int count = Physics.BoxCastNonAlloc(center, extents, direction, stepHits,
            rotation, distance, ~0, QueryTriggerInteraction.Ignore);
        if (count == stepHits.Length) return true; // Pas assez de place pour verifier tous les obstacles.
        for (int i = 0; i < count; i++)
            if (IsStaticObstacle(stepHits[i].collider) && stepHits[i].distance < closest)
            {
                closest = stepHits[i].distance;
                nearest = stepHits[i];
            }
        return !float.IsPositiveInfinity(closest);
    }

    private void ApplyCurbTolerance(Quaternion rotation)
    {
        if (maxStepHeight <= 0f || busCollider == null || !busCollider.enabled) return;
        Vector3 velocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        if (velocity.sqrMagnitude < 0.04f || rb.linearVelocity.y > 1f) return;
        Vector3 scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 extents = Vector3.Scale(busCollider.size * 0.5f, scale);
        Vector3 center = rb.position + rotation * Vector3.Scale(busCollider.center, scale);
        float bottom = wheelRig != null && wheelRig.IsReady
            ? rb.position.y + wheelRig.RestBottom
            : center.y - extents.y;
        // Ne pas grimper sur un obstacle pendant une chute ou un saut.
        int groundCount = Physics.RaycastNonAlloc(new Vector3(center.x, bottom + 0.05f, center.z),
            Vector3.down, stepHits, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        bool grounded = wheelRig != null && wheelRig.IsReady && wheelRig.IsGrounded;
        for (int i = 0; i < groundCount; i++)
            if (IsStaticObstacle(stepHits[i].collider) && stepHits[i].normal.y > 0.65f)
                grounded = true;
        if (!grounded) return;

        Vector3 direction = velocity.normalized;
        float distance = velocity.magnitude * Time.fixedDeltaTime + stepProbeDistance;
        // Mesure le dessus des bordures, meme si leur mesh n'a pas de face laterale detectable.
        Vector3 castExtents = Vector3.Max(extents - Vector3.one * 0.025f, Vector3.one * 0.01f);
        Vector3 nextCenter = center + direction * distance;
        float top = float.NegativeInfinity;
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 probe = nextCenter + rotation * new Vector3(x * castExtents.x, 0f, z * castExtents.z);
                probe.y = bottom + maxStepHeight + 0.05f;
                int count = Physics.RaycastNonAlloc(probe, Vector3.down, stepHits,
                    maxStepHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore);
                if (count == stepHits.Length) return;
                for (int i = 0; i < count; i++)
                    if (IsStaticObstacle(stepHits[i].collider) && stepHits[i].normal.y > 0.65f)
                        top = Mathf.Max(top, stepHits[i].point.y);
            }
        float rise = top - bottom;
        if (rise <= 0.02f || rise > maxStepHeight) return;

        Vector3 lift = Vector3.up * (rise + 0.025f);
        // Le bus entier doit pouvoir passer : un mur ou un plafond bloque toujours.
        int overlapCount = Physics.OverlapBoxNonAlloc(center + lift, castExtents,
            stepOverlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (overlapCount == stepOverlaps.Length) return;
        for (int i = 0; i < overlapCount; i++)
            if (IsStaticObstacle(stepOverlaps[i])) return;
        overlapCount = Physics.OverlapBoxNonAlloc(nextCenter + lift, castExtents,
            stepOverlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (overlapCount == stepOverlaps.Length) return;
        for (int i = 0; i < overlapCount; i++)
            if (IsStaticObstacle(stepOverlaps[i])) return;
        if (CastStepBox(center + lift, castExtents, rotation, direction, distance, out _)) return;
        rb.position += lift;
        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity = velocity;
    }

    private void ResetVehicle()
    {
        // Conserve uniquement la direction horizontale actuelle
        float currentYaw = transform.eulerAngles.y;

        // Stop complet
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Remonte légèrement le bus
        rb.position =
            transform.position +
            Vector3.up * resetHeight;

        // Remet X et Z à zéro
        rb.rotation =
            Quaternion.Euler(
                0f,
                currentYaw,
                0f
            );

        if (wheelRig != null) wheelRig.ResetWheels();
        GetComponent<BusAirTricks>()?.ResetTrick();

        // Reset également la direction
        currentSteering = 0f;
    }

    private void UpdateDebug()
    {
        float forwardSpeed =
            Vector3.Dot(
                rb.linearVelocity,
                rb.rotation * Vector3.forward
            );

        signedSpeedKmh =
            forwardSpeed * 3.6f;

        currentSpeedKmh =
            Mathf.Abs(signedSpeedKmh);

        isReversing =
            signedSpeedKmh < -0.5f;

        isHandbraking =
            handbrakeInput;
    }
}
