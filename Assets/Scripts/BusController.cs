using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BusController : MonoBehaviour
{
    [Header("Vitesse")]
    public float maxSpeedKmh = 120f;
    public float maxReverseSpeedKmh = 30f;

    [Header("Acceleration")]
    public float acceleration = 18f;
    public float reverseAcceleration = 8f;
    public float naturalDeceleration = 2.5f;

    [Header("Freinage")]
    public float brakeDeceleration = 25f;
    public float handbrakeDeceleration = 35f;

    [Header("Direction")]
    public float maxSteeringSpeed = 55f;
    public float steeringResponse = 5f;

    [Range(0.1f, 1f)]
    public float highSpeedSteeringMultiplier = 0.35f;

    [Header("Adherence")]
    public float lateralGrip = 14f;
    public float handbrakeGrip = 3f;

    [Header("Debug")]
    [SerializeField] private float currentSpeedKmh;
    [SerializeField] private float signedSpeedKmh;
    [SerializeField] private bool isReversing;
    [SerializeField] private bool isHandbraking;

    private Rigidbody rb;

    private float throttleInput;
    private float steeringInput;
    private float currentSteering;

    private bool handbrakeInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        ReadInputs();

        currentSteering = Mathf.MoveTowards(
            currentSteering,
            steeringInput,
            steeringResponse * Time.deltaTime
        );
    }

    private void FixedUpdate()
    {
        UpdateDebug();

        ApplyLongitudinalMovement();
        ApplySteering();
        ApplyLateralGrip();
    }

    private void ReadInputs()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        handbrakeInput = false;

        if (Keyboard.current == null)
            return;

        // ACCELERER
        if (
            Keyboard.current.zKey.isPressed ||
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed
        )
        {
            throttleInput = 1f;
        }

        // FREIN / MARCHE ARRIERE
        if (
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed
        )
        {
            throttleInput = -1f;
        }

        // GAUCHE
        if (
            Keyboard.current.qKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed
        )
        {
            steeringInput = -1f;
        }

        // DROITE
        if (
            Keyboard.current.dKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed
        )
        {
            steeringInput = 1f;
        }

        // FREIN A MAIN
        handbrakeInput = Keyboard.current.spaceKey.isPressed;
    }

    private void ApplyLongitudinalMovement()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        float forwardSpeed = localVelocity.z;

        float maxForwardSpeed = maxSpeedKmh / 3.6f;
        float maxReverseSpeed = maxReverseSpeedKmh / 3.6f;

        // =========================
        // FREIN A MAIN
        // =========================

        if (handbrakeInput)
        {
            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                0f,
                handbrakeDeceleration * Time.fixedDeltaTime
            );
        }

        // =========================
        // Z / AVANT
        // =========================

        else if (throttleInput > 0f)
        {
            // Si on recule, Z sert d'abord à freiner.
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
                    acceleration * Time.fixedDeltaTime
                );
            }
        }

        // =========================
        // S / FREIN + ARRIERE
        // =========================

        else if (throttleInput < 0f)
        {
            // Si on roule vers l'avant,
            // S commence par freiner.
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
                // Une fois presque arrêté :
                // marche arrière.
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    -maxReverseSpeed,
                    reverseAcceleration * Time.fixedDeltaTime
                );
            }
        }

        // =========================
        // AUCUNE TOUCHE
        // =========================

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
            transform.TransformDirection(localVelocity);

        // On laisse Unity gérer la gravité.
        worldVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = worldVelocity;
    }

    private void ApplySteering()
    {
        float forwardSpeed =
            Vector3.Dot(
                rb.linearVelocity,
                transform.forward
            );

        float speedKmh =
            Mathf.Abs(forwardSpeed) * 3.6f;

        // Très peu de direction presque à l'arrêt.
        float lowSpeedFactor =
            Mathf.Clamp01(speedKmh / 8f);

        // Réduction progressive du braquage
        // quand le bus va vite.
        float highSpeedFactor = Mathf.Lerp(
            1f,
            highSpeedSteeringMultiplier,
            Mathf.Clamp01(speedKmh / maxSpeedKmh)
        );

        // En marche arrière, la direction
        // fonctionne naturellement à l'envers.
        float reverseDirection =
            forwardSpeed >= 0f ? 1f : -1f;

        float rotationAmount =
            currentSteering *
            maxSteeringSpeed *
            lowSpeedFactor *
            highSpeedFactor *
            reverseDirection *
            Time.fixedDeltaTime;

        Quaternion targetRotation =
            rb.rotation *
            Quaternion.Euler(
                0f,
                rotationAmount,
                0f
            );

        rb.MoveRotation(targetRotation);
    }

    private void ApplyLateralGrip()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(
                rb.linearVelocity
            );

        float grip =
            handbrakeInput
            ? handbrakeGrip
            : lateralGrip;

        // Réduit progressivement la vitesse latérale.
        // Avec le frein à main, l'arrière glisse davantage.
        localVelocity.x = Mathf.MoveTowards(
            localVelocity.x,
            0f,
            grip * Time.fixedDeltaTime
        );

        Vector3 correctedVelocity =
            transform.TransformDirection(localVelocity);

        correctedVelocity.y =
            rb.linearVelocity.y;

        rb.linearVelocity =
            correctedVelocity;
    }

    private void UpdateDebug()
    {
        float forwardSpeed =
            Vector3.Dot(
                rb.linearVelocity,
                transform.forward
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