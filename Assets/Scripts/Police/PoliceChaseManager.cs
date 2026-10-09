using UnityEngine;

public class PoliceChaseManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody busRigidbody;
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private PoliceCatchZone catchZone;

    [Header("Allure du bus")]
    [SerializeField, Min(1f)] private float safeSpeedKmh = 100f;
    [SerializeField, Min(0f)] private float startupGraceSeconds = 3f;

    [Header("Distance derriere le bus (metres)")]
    [SerializeField, Min(1f)] private float appearanceDistance = 8f;
    [SerializeField, Min(1f)] private float disappearanceDistance = 18f;
    [SerializeField, Min(0.1f)] private float retreatSpeed = 6f;
    [SerializeField] private float appearanceSideOffset = 1.4f;

    private float distance;
    private float approachStartDistance;
    private float approachProgress;
    private float graceRemaining;
    private float captureAlignment;
    public bool ChaseStarted => ActivePolice != null;
    public PoliceCarAI ActivePolice { get; private set; }
    public float PoliceDistance => ActivePolice != null ? distance : disappearanceDistance;

    private void Awake()
    {
        if (busRigidbody == null || policePrefab == null || policePrefab.GetComponent<PoliceCarAI>() == null)
        {
            Debug.LogError("Police : renseigner le bus et le prefab de voiture.", this);
            enabled = false;
            return;
        }
        if (catchZone == null) catchZone = busRigidbody.GetComponentInChildren<PoliceCatchZone>();
        if (catchZone == null)
        {
            Debug.LogError("Police : zone de capture et evenement de defaite manquants.", this);
            enabled = false;
            return;
        }
        graceRemaining = startupGraceSeconds;
    }

    private float ForwardSpeed()
    {
        Vector3 forward = Vector3.ProjectOnPlane(busRigidbody.rotation * Vector3.forward, Vector3.up).normalized;
        return Mathf.Max(0f, Vector3.Dot(busRigidbody.linearVelocity, forward)) * 3.6f;
    }

    private void FixedUpdate()
    {
        if (busRigidbody == null || catchZone == null || catchZone.IsCaught || Time.timeScale <= 0f) return;
        float dt = Time.fixedDeltaTime;
        float speed = ForwardSpeed();
        bool recovering = speed >= safeSpeedKmh;
        if (graceRemaining > 0f)
        {
            graceRemaining = Mathf.Max(0f, graceRemaining - dt);
            catchZone.AdvanceCapture(false, dt);
            return;
        }
        if (ActivePolice == null)
        {
            catchZone.AdvanceCapture(false, dt);
            if (recovering) return;
            var instance = Instantiate(policePrefab);
            instance.SetActive(false);
            ActivePolice = instance.GetComponent<PoliceCarAI>();
            ActivePolice.SetTarget(busRigidbody.transform, busRigidbody);
            // Initialise la geometrie avant de rendre le vehicule visible.
            ActivePolice.InitializeBody();
            distance = Mathf.Max(ActivePolice.GetCaptureGap(catchZone.CaptureCollider) + 1f, appearanceDistance);
            approachStartDistance = distance;
            approachProgress = 0f;
            captureAlignment = 0f;
            ActivePolice.SetFollowingDistance(distance, appearanceSideOffset);
            ActivePolice.UpdateFollowingPose(dt);
            instance.SetActive(true);
            // Le compteur commence a l'apparition, pas un pas physique avant.
            return;
        }
        float captureGap = ActivePolice.GetCaptureGap(catchZone.CaptureCollider);
        float farDistance = Mathf.Max(disappearanceDistance, appearanceDistance + 1f, captureGap + 1f);
        if (recovering)
        {
            catchZone.AdvanceCapture(false, dt);
            approachProgress = 0f;
            distance = Mathf.MoveTowards(distance, farDistance, retreatSpeed * dt);
            captureAlignment = Mathf.MoveTowards(captureAlignment, 0f, dt);
        }
        else
        {
            if (catchZone.CatchProgress01 <= 0f) approachStartDistance = distance;
            float clock = Mathf.Clamp01(catchZone.CatchProgress01 + dt / catchZone.CaptureDurationSeconds);
            // A temps egal, un bus plus lent laisse la police gagner plus vite du terrain.
            // Toutes les courbes atteignent la zone a exactement cinq secondes.
            float curve = Mathf.Lerp(0.7f, 1.4f, Mathf.Clamp01(speed / safeSpeedKmh));
            approachProgress = Mathf.Max(approachProgress, Mathf.Pow(clock, curve));
            distance = Mathf.Lerp(approachStartDistance, captureGap, approachProgress);
            // Rejoindre progressivement la zone pour conserver le contact a cinq secondes.
            captureAlignment = Mathf.SmoothStep(0f, 1f, approachProgress);
        }
        float sideOffset = appearanceSideOffset * (1f - approachProgress);
        ActivePolice.SetFollowingDistance(distance, sideOffset, captureAlignment);
        ActivePolice.UpdateFollowingPose(dt);
        if (!recovering)
            catchZone.AdvanceCapture(true, dt, ActivePolice.IsTouchingCaptureZone(catchZone.CaptureCollider));
        if (recovering && distance >= farDistance - 0.001f) RemovePolice();
    }

    private void RemovePolice()
    {
        if (ActivePolice == null) return;
        ActivePolice.gameObject.SetActive(false);
        Destroy(ActivePolice.gameObject);
        ActivePolice = null;
    }

    private void OnDisable()
    {
        RemovePolice();
        if (catchZone != null) catchZone.AdvanceCapture(false, 0f);
    }
}
