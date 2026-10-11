using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class BusCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;

    [Header("Position")]
    public float distance = 8f;
    public float height = 3.3f;
    public float lookHeight = 2f;
    public float lookAheadDistance = 2f;

    [Header("Vue de la poursuite")]
    public PoliceChaseManager chaseManager;
    public float chaseDistance = 9.5f;
    public float chaseHeight = 3.5f;

    [Range(0f, 0.5f)]
    public float policeFramingWeight = 0.1f;

    public float chaseFramingSpeed = 4f;

    private float chaseBlend;

    [Header("Mouse Orbit")]
    public float mouseSensitivity = 0.12f;
    public float minPitch = -5f;
    public float maxPitch = 35f;
    public bool invertY = false;

    [Header("Follow")]
    public float positionSmoothTime = 0.08f;
    public float rotationSmoothSpeed = 12f;

    [Header("Auto Recenter")]
    public bool autoRecenter = true;
    public float recenterDelay = 1.2f;
    public float recenterSpeed = 3.5f;
    public float minSpeedForRecenter = 5f;

    [Header("Dynamic FOV")]
    public float baseFOV = 65f;
    public float maxFOV = 78f;
    public float fovAtSpeedKmh = 120f;
    public float fovSmoothSpeed = 5f;

    // ==========================================
    // MODE AVION
    // ==========================================

    [Header("Mode avion")]
    public BusFlightController flightController;

    public float flightDistance = 12f;
    public float flightHeight = 4.5f;
    public float flightLookAhead = 9f;
    public float flightFOV = 85f;

    [Range(0f, 1f)]
    public float flightBankInfluence = 0.25f;

    private bool wasFlying;

    [Header("Debug")]
    [SerializeField] private float currentSpeedKmh;
    [SerializeField] private float currentYaw;
    [SerializeField] private float currentPitch;

    [Header("Camera Lock")]
    public bool cameraLocked = false;
    public float defaultPitch = 12f;

    private Camera cam;
    private Rigidbody targetRb;

    private float yaw;
    private float pitch = 12f;

    private float lastManualOrbitTime;

    private Vector3 positionVelocity;

    // ==========================================
    // INITIALISATION
    // ==========================================

    [Header("Secousse a l’impact du Bus Fighter")]
    [Range(0f, 0.5f)] public float landingShakeDistance = 0.16f;
    [Range(0f, 5f)] public float landingShakeAngle = 1.2f;
    [Min(0.01f)] public float landingShakeDuration = 0.28f;
    private float landingShakeRemaining;
    private float landingShakeStrength;
    private bool shakeApplied;
    private Vector3 unshakenPosition;
    private Quaternion unshakenRotation;

    private void BeginLandingShake(float strength)
    {
        if (!isActiveAndEnabled) return;
        landingShakeStrength = Mathf.Clamp01(strength);
        landingShakeRemaining = landingShakeDuration;
    }

    private void RemoveLandingShake()
    {
        if (!shakeApplied) return;
        transform.SetPositionAndRotation(unshakenPosition, unshakenRotation);
        shakeApplied = false;
    }

    private void ApplyLandingShake(float dt)
    {
        if (landingShakeRemaining <= 0f) return;
        landingShakeRemaining = Mathf.Max(0f, landingShakeRemaining - dt);
        float progress = 1f - landingShakeRemaining / Mathf.Max(0.01f, landingShakeDuration);
        float envelope = landingShakeStrength * (1f - progress) * (1f - progress);
        float wave = Mathf.Cos(progress * Mathf.PI * 10f);
        unshakenPosition = transform.position;
        unshakenRotation = transform.rotation;
        transform.position += transform.up * (wave * landingShakeDistance * envelope);
        transform.rotation *= Quaternion.Euler(wave * landingShakeAngle * envelope, 0f,
            Mathf.Sin(progress * Mathf.PI * 8f) * landingShakeAngle * envelope * 0.4f);
        shakeApplied = true;
    }

    private void OnDisable()
    {
        RemoveLandingShake();
        landingShakeRemaining = 0f;
    }

    private void OnDestroy()
    {
        if (flightController != null) flightController.Landed -= BeginLandingShake;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        // The menu stores a multiplier; 0.75 keeps the original 0.12 setting.
        mouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(CroustyMenu.SensitivityKey, mouseSensitivity / 0.16f),
            0.2f, 1.5f) * 0.16f;

        if (target == null)
        {
            Debug.LogError(
                "BusCameraFollow : aucun Target assigne."
            );

            enabled = false;
            return;
        }

        targetRb = targetRigidbody;

        if (targetRb == null)
        {
            targetRb = target.GetComponentInParent<Rigidbody>();
        }

        // Recherche automatique du mode avion.
        if (flightController == null)
        {
            flightController = targetRb != null
                ? targetRb.GetComponent<BusFlightController>()
                : target.GetComponentInParent<BusFlightController>();
        }

        if (flightController != null) flightController.Landed += BeginLandingShake;

        // Recherche automatique du manager de police.
        if (chaseManager == null)
        {
            chaseManager =
                FindFirstObjectByType<PoliceChaseManager>();
        }

        yaw = target.eulerAngles.y;
        pitch = defaultPitch;

        lastManualOrbitTime = -999f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SnapCamera();
    }

    // ==========================================
    // UPDATE
    // ==========================================

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        HandleCursor();
        HandleMouseOrbit();

        UpdateSpeed();
        UpdateFOV();

        currentYaw = yaw;
        currentPitch = pitch;
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f) return;
        RemoveLandingShake();
        UpdateCamera(Time.deltaTime);
        ApplyLandingShake(Time.deltaTime);
    }

    // ==========================================
    // GESTION PRINCIPALE CAMERA
    // ==========================================

    private void UpdateCamera(float deltaTime)
    {
        if (target == null || Time.timeScale <= 0f)
            return;

        bool flying =
            flightController != null &&
            flightController.IsAirborne;

        // MODE AVION
        if (flying)
        {
            wasFlying = true;

            UpdateFlightCamera(deltaTime);
            return;
        }

        // RETOUR AU SOL
        if (wasFlying)
        {
            yaw = target.eulerAngles.y;
            pitch = defaultPitch;

            lastManualOrbitTime = -999f;

            positionVelocity = Vector3.zero;

            wasFlying = false;
        }

        // MODE TERRESTRE NORMAL
        if (cameraLocked)
        {
            yaw = target.eulerAngles.y;
            pitch = defaultPitch;
        }
        else
        {
            HandleAutoRecenter();
        }

        // Cadrage de la police.
        chaseBlend = Mathf.MoveTowards(
            chaseBlend,
            chaseManager != null &&
            chaseManager.ChaseStarted ? 1f : 0f,
            chaseFramingSpeed * deltaTime
        );

        UpdateCameraPosition(deltaTime);
    }

    // ==========================================
    // SOURIS
    // ==========================================

    private void HandleMouseOrbit()
    {
        // Pas d'orbite manuelle pendant le vol.
        if (flightController != null &&
            flightController.IsAirborne)
        {
            return;
        }

        if (cameraLocked)
            return;

        if (Mouse.current == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        if (mouseDelta.sqrMagnitude > 0.01f)
        {
            yaw += mouseDelta.x * mouseSensitivity;

            float verticalInput =
                mouseDelta.y * mouseSensitivity;

            if (invertY)
                pitch += verticalInput;
            else
                pitch -= verticalInput;

            pitch = Mathf.Clamp(
                pitch,
                minPitch,
                maxPitch
            );

            lastManualOrbitTime = Time.time;
        }
    }

    // ==========================================
    // RECENTRAGE AUTOMATIQUE
    // ==========================================

    private void HandleAutoRecenter()
    {
        if (!autoRecenter)
            return;

        if (currentSpeedKmh < minSpeedForRecenter)
            return;

        if (Time.time - lastManualOrbitTime < recenterDelay)
            return;

        float targetYaw = target.eulerAngles.y;

        yaw = Mathf.LerpAngle(
            yaw,
            targetYaw,
            recenterSpeed * Time.deltaTime
        );
    }

    // ==========================================
    // CAMERA MODE AVION
    // ==========================================

    private void UpdateFlightCamera(float deltaTime)
    {
        // Desactive progressivement le cadrage policier.
        chaseBlend = Mathf.MoveTowards(
            chaseBlend,
            0f,
            chaseFramingSpeed * deltaTime
        );

        bool falling = flightController != null && flightController.IsFalling;
        // During the fall keep the horizon level, even if the bus tumbles.
        Vector3 direction = falling && targetRb != null &&
            Vector3.ProjectOnPlane(targetRb.linearVelocity, Vector3.up).sqrMagnitude > 1f
            ? targetRb.linearVelocity : target.forward;
        Vector3 flatForward = Vector3.ProjectOnPlane(direction, Vector3.up);

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();

        // Position de la camera derriere le bus.
        Vector3 desiredPosition =
            target.position
            - flatForward * (falling ? distance : flightDistance)
            + Vector3.up * (falling ? height : flightHeight);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime,
            Mathf.Infinity,
            deltaTime
        );

        // Regarde vers l'avant du bus.
        Vector3 lookTarget =
            target.position
            + (falling ? flatForward * lookAheadDistance : target.forward * flightLookAhead)
            + Vector3.up * (falling ? lookHeight : 0.8f);

        // Influence du roulis sur la camera.
        Vector3 cameraUp = Vector3.Slerp(
            Vector3.up,
            target.up,
            falling ? 0f : flightBankInfluence
        );

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                cameraUp
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * deltaTime
        );
    }

    // ==========================================
    // CAMERA TERRESTRE
    // ==========================================

    private void UpdateCameraPosition(float deltaTime)
    {
        float speedRatio = Mathf.Clamp01(
            currentSpeedKmh / fovAtSpeedKmh
        );

        Vector3 lookAhead =
            target.forward *
            lookAheadDistance *
            speedRatio;

        Vector3 policeFocus = Vector3.zero;

        if (chaseManager != null &&
            chaseManager.ActivePolice != null)
        {
            policeFocus = Vector3.ProjectOnPlane(
                chaseManager.ActivePolice.transform.position
                - target.position,
                Vector3.up
            ) * policeFramingWeight * chaseBlend;
        }

        lookAhead *= 1f - chaseBlend;

        float framedDistance = Mathf.Lerp(
            distance,
            chaseDistance,
            chaseBlend
        );

        float framedHeight = Mathf.Lerp(
            height,
            chaseHeight,
            chaseBlend
        );

        Vector3 pivot =
            target.position
            + policeFocus
            + Vector3.up * framedHeight
            + lookAhead;

        Quaternion orbitRotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        Vector3 desiredPosition =
            pivot
            - orbitRotation *
            Vector3.forward *
            framedDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime,
            Mathf.Infinity,
            deltaTime
        );

        Vector3 lookTarget =
            target.position
            + Vector3.up * lookHeight
            + policeFocus
            + lookAhead;

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmoothSpeed * deltaTime
        );
    }

    // ==========================================
    // VITESSE
    // ==========================================

    private void UpdateSpeed()
    {
        if (targetRb == null)
        {
            currentSpeedKmh = 0f;
            return;
        }

        Vector3 horizontalVelocity =
            Vector3.ProjectOnPlane(
                targetRb.linearVelocity,
                Vector3.up
            );

        currentSpeedKmh =
            horizontalVelocity.magnitude * 3.6f;
    }

    // ==========================================
    // FOV DYNAMIQUE
    // ==========================================

    private void UpdateFOV()
    {
        // MODE AVION
        if (flightController != null &&
            flightController.IsAirborne)
        {
            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                flightController.IsFalling ? baseFOV : flightFOV,
                fovSmoothSpeed * Time.deltaTime
            );

            return;
        }

        // MODE TERRESTRE
        float speedRatio = Mathf.Clamp01(
            currentSpeedKmh / fovAtSpeedKmh
        );

        float targetFOV = Mathf.Lerp(
            baseFOV,
            maxFOV,
            speedRatio
        );

        cam.fieldOfView = Mathf.Lerp(
            cam.fieldOfView,
            targetFOV,
            fovSmoothSpeed * Time.deltaTime
        );
    }

    // ==========================================
    // CURSEUR
    // ==========================================

    private void HandleCursor()
    {
        if (!CroustyPauseMenu.ToggledThisFrame && Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ==========================================
    // POSITION INITIALE
    // ==========================================

    private void SnapCamera()
    {
        Vector3 pivot =
            target.position +
            Vector3.up * height;

        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        transform.position =
            pivot
            - rotation *
            Vector3.forward *
            distance;

        Vector3 lookTarget =
            target.position +
            Vector3.up * lookHeight;

        transform.rotation = Quaternion.LookRotation(
            lookTarget - transform.position,
            Vector3.up
        );

        cam.fieldOfView = baseFOV;
    }
}
