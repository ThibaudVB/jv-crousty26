using UnityEngine;

// This component opts only the SceneBase van into terrain following.
[DefaultExecutionOrder(500)]
[RequireComponent(typeof(BusController), typeof(BusWheelRig))]
public sealed class SceneBaseGrounding : MonoBehaviour
{
    [Min(0.01f)] public float suspensionTravel = 0.3f;
    [Min(0f)] public float suspensionDamping = 5500f;
    [Min(0f)] public float slopeResponse = 8f;
    [Min(1f)] public float gravityMultiplier = 1.35f;
    [Min(0f)] public float tiltSpring = 40f;
    [Min(0f)] public float tiltDamping = 13f;
    [Range(0f, 75f)] public float maximumSlope = 55f;
    [SerializeField, HideInInspector] private int tuningRevision;

    private Rigidbody body;
    private BusController controller;
    private BusWheelRig rig;
    private RigidbodyConstraints originalConstraints;
    private Vector3 originalCenterOfMass;
    private float originalStepHeight;
    private bool originalTerrainDrive;
    private float[] originalTravel;
    private JointSpring[] originalSprings;
    private Vector3 filteredNormal = Vector3.up;
    private bool configured;

    private void Awake()
    {
        ApplyTerrainTuningUpdate();
        body = GetComponent<Rigidbody>();
        controller = GetComponent<BusController>();
        rig = GetComponent<BusWheelRig>();
    }
    public bool ApplyTerrainTuningUpdate()
    {
        if (tuningRevision >= 1) return false;
        suspensionTravel = 0.3f;
        suspensionDamping = 5500f;
        slopeResponse = 8f;
        gravityMultiplier = 1.35f;
        tiltSpring = 40f;
        tiltDamping = 13f;
        maximumSlope = 55f;
        tuningRevision = 1;
        return true;
    }
    private void Start() => Configure();
    private void OnEnable()
    {
        if (rig != null && rig.IsReady) Configure();
    }
    private void Configure()
    {
        if (configured || !rig.IsReady) return;
        originalConstraints = body.constraints;
        originalCenterOfMass = body.centerOfMass;
        originalStepHeight = controller.maxStepHeight;
        originalTerrainDrive = rig.followTerrain;
        originalTravel = new float[rig.wheels.Length];
        originalSprings = new JointSpring[rig.wheels.Length];
        for (int i = 0; i < rig.wheels.Length; i++)
        {
            var wheel = rig.wheels[i].collider;
            originalTravel[i] = wheel.suspensionDistance;
            originalSprings[i] = wheel.suspensionSpring;
            wheel.suspensionDistance = suspensionTravel;
            var spring = wheel.suspensionSpring;
            spring.damper = suspensionDamping;
            wheel.suspensionSpring = spring;
        }
        var center = body.centerOfMass;
        center.y = Mathf.Min(center.y, 0.35f);
        body.centerOfMass = center;
        // Let the wheel contacts determine height. The old curb helper teleports the chassis upward.
        controller.maxStepHeight = 0f;
        rig.followTerrain = true;
        body.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
        filteredNormal = Vector3.up;
        configured = true;
    }
    private void FixedUpdate()
    {
        if (!configured || body.isKinematic || !controller.isActiveAndEnabled || !rig.isActiveAndEnabled) return;
        Vector3 normal = Vector3.zero;
        int contacts = 0;
        float minimumUp = Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
        foreach (var wheel in rig.wheels)
            if (wheel.collider.GetGroundHit(out var hit) && hit.normal.y >= minimumUp)
            {
                normal += hit.normal;
                contacts++;
            }
        if (contacts == 0)
        {
            if (body.useGravity)
                body.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            return;
        }
        if (contacts < 2) return;
        filteredNormal = Vector3.Slerp(filteredNormal, normal.normalized,
            1f - Mathf.Exp(-slopeResponse * Time.fixedDeltaTime)).normalized;
        Vector3 error = Vector3.Cross(body.rotation * Vector3.up, filteredNormal);
        Vector3 tiltVelocity = Vector3.ProjectOnPlane(body.angularVelocity, Vector3.up);
        Vector3 acceleration = Vector3.ProjectOnPlane(error * tiltSpring, Vector3.up) - tiltVelocity * tiltDamping;
        // A damped torque assists the suspension; neither pose nor vertical velocity is overwritten.
        body.AddTorque(Vector3.ClampMagnitude(acceleration, 20f), ForceMode.Acceleration);
    }
    private void OnDisable()
    {
        if (!configured) return;
        if (controller != null) controller.maxStepHeight = originalStepHeight;
        if (body != null)
        {
            body.constraints = originalConstraints;
            body.centerOfMass = originalCenterOfMass;
        }
        if (rig != null)
        {
            rig.followTerrain = originalTerrainDrive;
            for (int i = 0; i < rig.wheels.Length; i++)
            {
                var wheel = rig.wheels[i].collider;
                if (wheel == null) continue;
                wheel.suspensionDistance = originalTravel[i];
                wheel.suspensionSpring = originalSprings[i];
            }
        }
        configured = false;
    }
}
