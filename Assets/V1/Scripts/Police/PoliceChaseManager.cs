using UnityEngine;

namespace V1
{
    // La police apparaît quand le bus roule trop lentement, se rapproche,
    // et arrête le bus si on ne réaccélère pas à temps.
    public class PoliceChaseManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody busRigidbody;
        [SerializeField] private GameObject policePrefab;
        [SerializeField] private PoliceCatchZone catchZone;

        [Header("Allure du bus")]
        [SerializeField, Min(1f)] private float safeSpeedKmh = 100f;
        [SerializeField, Min(0f)] private float startupGraceSeconds = 0f;
        [SerializeField, Min(0f)] private float minimumRecoverySpeedKmh = 20f;
        [SerializeField, Min(0f)] private float accelerationRecoverySeconds = 0.75f;

        [Header("Distance derriere le bus (metres)")]
        [SerializeField, Min(1f)] private float appearanceDistance = 50f;
        [SerializeField, Min(0.1f)] private float captureDistance = 3f;
        [SerializeField, Min(1f)] private float disappearanceDistance = 65f;
        [SerializeField, Min(0.1f)] private float retreatSpeed = 12f;

        private float distance;
        private float approachStartDistance;
        private float graceRemaining;
        private float previousSpeed;
        private float recoveryRemaining;
        private bool recovering;

        public bool ChaseStarted => ActivePolice != null;
        public PoliceCarAI ActivePolice { get; private set; }
        public float PoliceDistance => ActivePolice != null ? distance : disappearanceDistance;
        public float SafeSpeedKmh => safeSpeedKmh;
        // 0 = pas de danger, 1 = arrêté
        public float CatchProgress01 => catchZone != null ? catchZone.CatchProgress01 : 0f;
        // Le bus roule assez vite : la police recule
        public bool IsEscaping => recovering;

        private void Awake()
        {
            if (busRigidbody == null)
            {
                BusController bus = FindAnyObjectByType<BusController>();
                if (bus != null)
                    busRigidbody = bus.GetComponent<Rigidbody>();
            }

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
            previousSpeed = ForwardSpeed();
        }

        private float ForwardSpeed()
        {
            Vector3 forward = Vector3.ProjectOnPlane(busRigidbody.transform.forward, Vector3.up).normalized;
            return Mathf.Max(0f, Vector3.Dot(busRigidbody.linearVelocity, forward)) * 3.6f;
        }

        private void FixedUpdate()
        {
            if (busRigidbody == null || catchZone == null || catchZone.IsCaught || Time.timeScale <= 0f) return;
            float dt = Time.fixedDeltaTime;
            float speed = ForwardSpeed();
            bool accelerating = speed >= minimumRecoverySpeedKmh && (speed - previousSpeed) / dt > 3f;
            bool slowingDown = speed < previousSpeed - 0.05f;
            previousSpeed = speed;
            recoveryRemaining = accelerating ? accelerationRecoverySeconds :
                slowingDown ? 0f : Mathf.Max(0f, recoveryRemaining - dt);
            recovering = speed >= safeSpeedKmh || recoveryRemaining > 0f;
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
                // Instantiate inactive so the original prefab position is never visible for a frame.
                GameObject staging = new GameObject("Police spawn");
                staging.SetActive(false);
                GameObject instance = Instantiate(policePrefab, staging.transform);
                ActivePolice = instance.GetComponent<PoliceCarAI>();
                ActivePolice.SetTarget(busRigidbody.transform, busRigidbody);
                distance = Mathf.Max(captureDistance, appearanceDistance);
                ActivePolice.SetFollowingDistance(distance);
                instance.transform.SetParent(null, true);
                Destroy(staging);
            }
            float farDistance = Mathf.Max(disappearanceDistance, appearanceDistance + 1f, captureDistance + 1f);
            if (recovering)
            {
                distance = Mathf.MoveTowards(distance, farDistance, retreatSpeed * dt);
            }
            else
            {
                // One clock covers the entire approach, rather than starting five more seconds on arrival.
                if (catchZone.CatchProgress01 <= 0f) approachStartDistance = distance;
                float progress = Mathf.Clamp01(catchZone.CatchProgress01 + dt / catchZone.CaptureDurationSeconds);
                distance = Mathf.Lerp(approachStartDistance, captureDistance, progress);
            }
            ActivePolice.SetFollowingDistance(distance);
            catchZone.AdvanceCapture(!recovering, dt);
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
}
