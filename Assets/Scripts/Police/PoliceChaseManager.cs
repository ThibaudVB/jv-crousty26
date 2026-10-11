using UnityEngine;

public class PoliceChaseManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody busRigidbody;
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private PoliceCatchZone catchZone;

    // ==========================================
    // MODE AVION
    // ==========================================

    [Header("Bus Fighter")]
    [SerializeField]
    private BusFlightController flightController;

    [SerializeField, Min(0f)]
    private float landingGraceSeconds = 1f;

    // ==========================================
    // PARAMETRES POLICE
    // ==========================================

    [Header("Allure du bus")]

    [SerializeField, Min(1f)]
    private float safeSpeedKmh = 100f;

    [SerializeField, Min(0f)]
    private float startupGraceSeconds = 3f;

    [Header("Distance derriere le bus (metres)")]

    [SerializeField, Min(1f)]
    private float appearanceDistance = 8f;

    [SerializeField, Min(1f)]
    private float disappearanceDistance = 18f;

    [SerializeField, Min(0.1f)]
    private float retreatSpeed = 6f;

    [SerializeField]
    private float appearanceSideOffset = 1.4f;

    // ==========================================
    // VARIABLES INTERNES
    // ==========================================

    public float BlindRemaining { get; private set; }
    public bool IsBlinded => BlindRemaining > 0f;

    public bool TryBlindWithPet(Rigidbody source, float duration, float range)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || source != busRigidbody ||
            ActivePolice == null || catchZone == null || catchZone.IsCaught) return false;
        Vector3 offset = ActivePolice.transform.position - source.position;
        if (Vector3.Dot(offset, source.rotation * Vector3.forward) > 0f ||
            distance > Mathf.Max(0f, range)) return false;
        BlindRemaining = Mathf.Clamp(duration, 0f, 5f);
        if (!IsBlinded) return false;
        catchZone.AdvanceCapture(false, 0f);
        approachProgress = captureAlignment = 0f;
        return true;
    }

    private float distance;
    private float approachStartDistance;
    private float approachProgress;
    private float graceRemaining;
    private float captureAlignment;

    public bool ChaseStarted => ActivePolice != null;

    public PoliceCarAI ActivePolice { get; private set; }

    public float PoliceDistance =>
        ActivePolice != null
            ? distance
            : disappearanceDistance;

    // ==========================================
    // INITIALISATION
    // ==========================================

    private void Awake()
    {
        if (busRigidbody == null ||
            policePrefab == null ||
            policePrefab.GetComponent<PoliceCarAI>() == null)
        {
            Debug.LogError(
                "Police : renseigner le bus et le prefab de voiture.",
                this
            );

            enabled = false;
            return;
        }

        if (catchZone == null)
        {
            catchZone =
                busRigidbody.GetComponentInChildren<PoliceCatchZone>();
        }

        if (catchZone == null)
        {
            Debug.LogError(
                "Police : zone de capture et evenement de defaite manquants.",
                this
            );

            enabled = false;
            return;
        }

        // Recuperation automatique du mode avion.
        if (flightController == null)
        {
            flightController =
                busRigidbody.GetComponent<BusFlightController>();
        }

        graceRemaining = startupGraceSeconds;
    }

    // ==========================================
    // VITESSE DU BUS
    // ==========================================

    private float ForwardSpeed()
    {
        Vector3 forward = Vector3.ProjectOnPlane(
            busRigidbody.rotation * Vector3.forward,
            Vector3.up
        ).normalized;

        return Mathf.Max(
            0f,
            Vector3.Dot(
                busRigidbody.linearVelocity,
                forward
            )
        ) * 3.6f;
    }

    // ==========================================
    // POURSUITE
    // ==========================================

    private void FixedUpdate()
    {
        if (busRigidbody == null ||
            catchZone == null ||
            catchZone.IsCaught ||
            Time.timeScale <= 0f)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;

        // ======================================
        // BONUS BUS FIGHTER
        // ======================================

        if (flightController != null &&
            flightController.IsAirborne)
        {
            // Annule la progression de capture.
            catchZone.AdvanceCapture(false, dt);

            // La police disparait pendant le vol.
            RemovePolice();

            // Protection apres l'atterrissage.
            graceRemaining = Mathf.Max(
                graceRemaining,
                landingGraceSeconds
            );

            return;
        }

        if (IsBlinded)
        {
            BlindRemaining = Mathf.Max(0f, BlindRemaining - dt);
            catchZone.AdvanceCapture(false, dt);
            approachProgress = captureAlignment = 0f;
            if (ActivePolice != null)
            {
                distance = Mathf.MoveTowards(distance, Mathf.Max(disappearanceDistance + 8f,
                    appearanceDistance + 8f), retreatSpeed * dt);
                ActivePolice.SetFollowingDistance(distance, appearanceSideOffset, 0f);
                ActivePolice.UpdateFollowingPose(dt);
            }
            return;
        }

        // ======================================
        // COMPORTEMENT NORMAL
        // ======================================

        float speed = ForwardSpeed();

        bool recovering = speed >= safeSpeedKmh;

        // Periode de securite.
        if (graceRemaining > 0f)
        {
            graceRemaining = Mathf.Max(
                0f,
                graceRemaining - dt
            );

            catchZone.AdvanceCapture(false, dt);

            return;
        }

        // ======================================
        // APPARITION DE LA POLICE
        // ======================================

        if (ActivePolice == null)
        {
            catchZone.AdvanceCapture(false, dt);

            // Pas de police si le bus est assez rapide.
            if (recovering)
                return;

            var instance = Instantiate(policePrefab);

            instance.SetActive(false);

            ActivePolice =
                instance.GetComponent<PoliceCarAI>();

            ActivePolice.SetTarget(
                busRigidbody.transform,
                busRigidbody
            );

            // Initialise la geometrie.
            ActivePolice.InitializeBody();

            distance = Mathf.Max(
                ActivePolice.GetCaptureGap(
                    catchZone.CaptureCollider
                ) + 1f,
                appearanceDistance
            );

            approachStartDistance = distance;
            approachProgress = 0f;
            captureAlignment = 0f;

            ActivePolice.SetFollowingDistance(
                distance,
                appearanceSideOffset
            );

            ActivePolice.UpdateFollowingPose(dt);

            instance.SetActive(true);

            // Le compteur commence apres l'apparition.
            return;
        }

        // ======================================
        // DISTANCES DE POURSUITE
        // ======================================

        float captureGap =
            ActivePolice.GetCaptureGap(
                catchZone.CaptureCollider
            );

        float farDistance = Mathf.Max(
            disappearanceDistance,
            appearanceDistance + 1f,
            captureGap + 1f
        );

        // ======================================
        // BUS ASSEZ RAPIDE
        // ======================================

        if (recovering)
        {
            // Arrete la capture.
            catchZone.AdvanceCapture(false, dt);

            approachProgress = 0f;

            distance = Mathf.MoveTowards(
                distance,
                farDistance,
                retreatSpeed * dt
            );

            captureAlignment = Mathf.MoveTowards(
                captureAlignment,
                0f,
                dt
            );
        }

        // ======================================
        // BUS TROP LENT
        // ======================================

        else
        {
            if (catchZone.CatchProgress01 <= 0f)
            {
                approachStartDistance = distance;
            }

            float clock = Mathf.Clamp01(
                catchZone.CatchProgress01 +
                dt / catchZone.CaptureDurationSeconds
            );

            // Plus le bus est lent,
            // plus la police gagne rapidement du terrain.
            float curve = Mathf.Lerp(
                0.7f,
                1.4f,
                Mathf.Clamp01(speed / safeSpeedKmh)
            );

            approachProgress = Mathf.Max(
                approachProgress,
                Mathf.Pow(clock, curve)
            );

            distance = Mathf.Lerp(
                approachStartDistance,
                captureGap,
                approachProgress
            );

            // Alignement progressif pour la capture.
            captureAlignment = Mathf.SmoothStep(
                0f,
                1f,
                approachProgress
            );
        }

        // ======================================
        // POSITIONNEMENT POLICE
        // ======================================

        float sideOffset =
            appearanceSideOffset *
            (1f - approachProgress);

        ActivePolice.SetFollowingDistance(
            distance,
            sideOffset,
            captureAlignment
        );

        ActivePolice.UpdateFollowingPose(dt);

        // ======================================
        // CAPTURE
        // ======================================

        if (!recovering)
        {
            catchZone.AdvanceCapture(
                true,
                dt,
                ActivePolice.IsTouchingCaptureZone(
                    catchZone.CaptureCollider
                )
            );
        }

        // ======================================
        // DISPARITION DE LA POLICE
        // ======================================

        if (recovering &&
            distance >= farDistance - 0.001f)
        {
            RemovePolice();
        }
    }

    // ==========================================
    // SUPPRESSION POLICE
    // ==========================================

    private void RemovePolice()
    {
        BlindRemaining = 0f;
        if (ActivePolice == null)
            return;

        ActivePolice.gameObject.SetActive(false);

        Destroy(ActivePolice.gameObject);

        ActivePolice = null;
    }

    // ==========================================
    // DESACTIVATION
    // ==========================================

    private void OnDisable()
    {
        RemovePolice();

        if (catchZone != null)
        {
            catchZone.AdvanceCapture(false, 0f);
        }
    }
}