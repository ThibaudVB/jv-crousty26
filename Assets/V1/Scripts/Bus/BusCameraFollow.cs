using UnityEngine;
using UnityEngine.InputSystem;

namespace V1
{
    // Caméra qui suit le bus, façon GTA : orbite à la souris, recentrage automatique,
    // champ de vision qui s'élargit avec la vitesse, coup de zoom pendant le boost,
    // léger décalage pendant les dérapages et tremblement lors des chocs.
    [RequireComponent(typeof(Camera))]
    public class BusCameraFollow : MonoBehaviour
    {
        [Header("Target (auto si vide)")]
        public Transform target;
        public Rigidbody targetRigidbody;

        [Header("Position")]
        public float distance = 10f;
        public float height = 4.5f;
        public float lookHeight = 2f;
        public float lookAheadDistance = 2f;

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
        [Tooltip("Degrés ajoutés au champ de vision pendant un boost")]
        public float boostFOV = 10f;

        [Header("Dérapage")]
        [Tooltip("La caméra se décale vers l'extérieur du virage pendant un dérapage (degrés)")]
        public float driftCameraAngle = 12f;

        [Header("Tremblement")]
        public float shakeStrength = 0.6f;
        public float shakeRecovery = 1.5f;

        [Header("Camera Lock")]
        public bool cameraLocked = false;
        public float defaultPitch = 12f;

        [Header("Debug")]
        [SerializeField] private float currentSpeedKmh;
        [SerializeField] private float currentYaw;
        [SerializeField] private float currentPitch;

        private Camera cam;
        private BusController bus;

        private float yaw;
        private float pitch = 12f;
        private float driftOffset;
        private float shake;

        private float lastManualOrbitTime;
        private Vector3 positionVelocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            if (target == null)
            {
                BusController found = FindAnyObjectByType<BusController>();
                if (found != null)
                    target = found.transform;
            }

            if (target == null)
            {
                Debug.LogError("BusCameraFollow : aucun bus à suivre (Target vide et aucun BusController).", this);
                enabled = false;
                return;
            }

            if (targetRigidbody == null)
                targetRigidbody = target.GetComponentInParent<Rigidbody>();

            bus = target.GetComponentInParent<BusController>();

            // Commence directement derrière le bus
            yaw = target.eulerAngles.y;
            lastManualOrbitTime = -999f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SnapCamera();
        }

        // Fait trembler la caméra (0 = rien, 1 = très fort). Les secousses s'additionnent.
        public void Shake(float amount)
        {
            shake = Mathf.Clamp01(shake + amount);
        }

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
            if (target == null || Time.timeScale <= 0f)
                return;

            if (cameraLocked)
            {
                yaw = target.eulerAngles.y;
                pitch = defaultPitch;
            }
            else
            {
                HandleAutoRecenter();
            }

            float targetDriftOffset = bus != null && bus.IsDrifting ? -bus.DriftDirection * driftCameraAngle : 0f;
            driftOffset = Mathf.Lerp(driftOffset, targetDriftOffset, 3f * Time.deltaTime);

            UpdateCameraPosition();
            ApplyShake();
        }

        private void HandleMouseOrbit()
        {
            if (cameraLocked || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            // Stick droit de la manette : même chose que la souris
            if (Gamepad.current != null)
                mouseDelta += Gamepad.current.rightStick.ReadValue() * 12f;

            if (mouseDelta.sqrMagnitude <= 0.01f)
                return;

            yaw += mouseDelta.x * mouseSensitivity;

            float verticalInput = mouseDelta.y * mouseSensitivity;
            pitch += invertY ? verticalInput : -verticalInput;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            lastManualOrbitTime = Time.time;
        }

        private void HandleAutoRecenter()
        {
            if (!autoRecenter ||
                currentSpeedKmh < minSpeedForRecenter ||
                Time.time - lastManualOrbitTime < recenterDelay)
                return;

            yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, recenterSpeed * Time.deltaTime);
        }

        private void UpdateCameraPosition()
        {
            float speedRatio = Mathf.Clamp01(currentSpeedKmh / fovAtSpeedKmh);
            Vector3 lookAhead = target.forward * lookAheadDistance * speedRatio;

            Vector3 pivot = target.position + Vector3.up * height + lookAhead;
            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw + driftOffset, 0f);
            Vector3 desiredPosition = pivot - orbitRotation * Vector3.forward * distance;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );

            Vector3 lookTarget = target.position + Vector3.up * lookHeight + lookAhead;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }

        private void ApplyShake()
        {
            if (shake <= 0f)
                return;

            // Le tremblement diminue tout seul
            float power = shake * shake * shakeStrength;
            float time = Time.time * 25f;
            transform.position += transform.right * (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * power
                                + transform.up * (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * power;

            shake = Mathf.Max(0f, shake - shakeRecovery * Time.deltaTime);
        }

        private void UpdateSpeed()
        {
            if (targetRigidbody == null)
            {
                currentSpeedKmh = 0f;
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(targetRigidbody.linearVelocity, Vector3.up);
            currentSpeedKmh = horizontalVelocity.magnitude * 3.6f;
        }

        private void UpdateFOV()
        {
            float speedRatio = Mathf.Clamp01(currentSpeedKmh / fovAtSpeedKmh);
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);

            if (bus != null && bus.IsBoosting)
                targetFOV += boostFOV;

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
        }

        private void HandleCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
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

        private void SnapCamera()
        {
            Vector3 pivot = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            transform.position = pivot - rotation * Vector3.forward * distance;

            Vector3 lookTarget = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);

            cam.fieldOfView = baseFOV;
        }
    }
}
