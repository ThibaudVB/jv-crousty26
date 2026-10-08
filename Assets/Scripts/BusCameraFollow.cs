using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class BusCameraFollow : MonoBehaviour
{
    [Header("Target")]
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

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("GTABusCamera : aucun Target assigné.");
            enabled = false;
            return;
        }

        targetRb = targetRigidbody;

        // Commence directement derrière le bus
        yaw = target.eulerAngles.y;

        lastManualOrbitTime = -999f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SnapCamera();
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

    UpdateCameraPosition();
}
    private void HandleMouseOrbit()
{
    if (cameraLocked)
        return;

    if (Mouse.current == null)
        return;

    if (Cursor.lockState != CursorLockMode.Locked)
        return;

    Vector2 mouseDelta = Mouse.current.delta.ReadValue();

    if (mouseDelta.sqrMagnitude > 0.01f)
    {
        yaw += mouseDelta.x * mouseSensitivity;

        float verticalInput = mouseDelta.y * mouseSensitivity;

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

    private void UpdateCameraPosition()
    {
        float speedRatio =
            Mathf.Clamp01(
                currentSpeedKmh / fovAtSpeedKmh
            );

        Vector3 lookAhead =
            target.forward *
            lookAheadDistance *
            speedRatio;

        Vector3 pivot =
            target.position +
            Vector3.up * height +
            lookAhead;

        Quaternion orbitRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        Vector3 desiredPosition =
            pivot -
            orbitRotation *
            Vector3.forward *
            distance;

        transform.position =
            Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );

        Vector3 lookTarget =
            target.position +
            Vector3.up * lookHeight +
            lookAhead;

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
    }

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

    private void UpdateFOV()
    {
        float speedRatio =
            Mathf.Clamp01(
                currentSpeedKmh / fovAtSpeedKmh
            );

        float targetFOV =
            Mathf.Lerp(
                baseFOV,
                maxFOV,
                speedRatio
            );

        cam.fieldOfView =
            Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                fovSmoothSpeed * Time.deltaTime
            );
    }

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
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

    private void SnapCamera()
    {
        Vector3 pivot =
            target.position +
            Vector3.up * height;

        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        transform.position =
            pivot -
            rotation *
            Vector3.forward *
            distance;

        Vector3 lookTarget =
            target.position +
            Vector3.up * lookHeight;

        transform.rotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );

        cam.fieldOfView = baseFOV;
    }
}