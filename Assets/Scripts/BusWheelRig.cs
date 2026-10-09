using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusWheelRig : MonoBehaviour
{
    [Serializable]
    public class Wheel
    {
        public string label;
        public WheelCollider collider;
        public Transform mesh;
        public bool steering;
        public bool driven;
        [HideInInspector] public Quaternion rotationOffset = Quaternion.identity;
    }

    [Header("Quatre roues independantes")]
    public Wheel[] wheels = new Wheel[0];
    [Header("Suspension")]
    public float vehicleMass = 1200f;
    public float suspensionTravel = 0.3f;
    public float springStrength = 40000f;
    public float springDamper = 5000f;
    public float chassisClearance = 0.7f;
    [Header("Direction et drift continu")]
    public float lowSpeedSteerAngle = 48f;
    public float highSpeedSteerAngle = 38f;
    public float frontGrip = 1.45f;
    public float rearStraightGrip = 1.2f;
    public float rearDriftGrip = 1.1f;
    public float handbrakeRearGrip = 0.25f;
    public float driftResponse = 3f;

    [Header("Stabilisation du drift arcade")]
    public float driftAngle = 8f;
    public float trajectoryResponse = 14f;
    public float maxYawRate = 2.8f;

    [Header("Reprise et direction arcade")]
    [Range(0f, 1f)] public float accelerationAssist = 0.8f;
    [Min(0f)] public float steeringAssistResponse = 14f;

    [Header("Virages serres et sortie de drift")]
    [Min(1f)] public float tightTurnMultiplier = 1.9f;
    [Range(0.1f, 1f)] public float highSpeedTurnAuthority = 0.65f;
    [Min(0f)] public float turnTrajectoryResponse = 20f;
    [Min(0f)] public float driftRecoveryResponse = 16f;
    [Min(0f)] public float rearGripRecovery = 8f;
    [Min(1f)] public float driftExitBoost = 1.12f;

    [Header("Progressivite des commandes (secondes)")]
    [Min(0.01f)] public float driftBuildUpSeconds = 1.25f;
    [Min(0.01f)] public float throttleResponseSeconds = 0.8f;
    [Min(0.01f)] public float steeringReturnSeconds = 0.8f;

    [Header("Courbe du drift et adherence arcade")]
    [Min(0.1f)] public float steeringCurveStrength = 4.5f;
    [Min(0f)] public float steeringCatchUpResponse = 14f;
    [Range(0f, 45f)] public float maxArcadeSlipAngle = 12f;

    [Header("Mode arcade parcours")]
    public bool fullArcade = true;
    [Min(0f)] public float arcadeTurnSpeed = 130f;
    [Min(0f)] public float arcadeAcceleration = 26f;
    [Min(0f)] public float arcadeCoastDeceleration = 3f;
    [Min(0f)] public float arcadeBrakeDeceleration = 32f;
    [Range(0f, 30f)] public float visualDriftAngle = 18f;
    [Range(0f, 15f)] public float visualRollAngle = 7f;
    [Range(0f, 10f)] public float visualPitchAngle = 4f;
    public float arcadeBoostSeconds = 0.65f;
    [Min(0.01f)] public float visualTransitionSeconds = 0.18f;

    [SerializeField, HideInInspector] private int arcadeTuningRevision;

    // Migration unique des anciennes scenes deja ouvertes; les reglages restent editables ensuite.
    public bool ApplyArcadeTuningUpdate(BusController controller)
    {
        if (arcadeTuningRevision >= 2) return false;
        controller.maxSpeedKmh = 90f;
        fullArcade = true;
        arcadeAcceleration = 26f;
        driftBuildUpSeconds = 1.25f;
        steeringCurveStrength = 4.5f;
        steeringCatchUpResponse = 14f;
        steeringReturnSeconds = 0.8f;
        steeringAssistResponse = 14f;
        visualTransitionSeconds = 0.18f;
        arcadeTuningRevision = 2;
        return true;
    }

    private Transform visualPivot;
    private Vector3 visualAngles;
    private Vector3 visualAngularVelocity;
    private float visualAcceleration;
    private float previousArcadeSpeed;
    private float previousRawSteering;
    private float arcadeBoostRemaining;
    private float steeringCharge;
    private float steeringMemoryDirection;
    private float progressiveSteering;
    private float progressiveThrottle;
    private float throttleTarget;
    private float throttleTransitionRate = 1f;
    public float SteeringAmount => progressiveSteering;
    public float ThrottleAmount => progressiveThrottle;

    private Rigidbody body;
    private bool initialized;
    private float driftBlend;
    private float exitBoostRemaining;
    public bool IsReady => initialized && wheels.Length == 4;
    public bool IsGrounded
    {
        get
        {
            foreach (var wheel in wheels)
                if (wheel.collider != null && wheel.collider.isGrounded) return true;
            return false;
        }
    }
    public float RestBottom { get; private set; }

    private void Awake() => Initialize();

    public void Initialize()
    {
        if (initialized) return;
        body = GetComponent<Rigidbody>();
        if (wheels == null || wheels.Length != 4)
        {
            string[] names = { "Roue Avant G", "Roue Avant D", "Roue arrière G", "Roue arrière D" };
            var meshes = new Transform[4];
            var children = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < names.Length; i++)
                foreach (var filter in children)
                    if (string.Equals(filter.name, names[i], StringComparison.OrdinalIgnoreCase))
                        meshes[i] = filter.transform;
            foreach (var mesh in meshes) if (mesh == null) return;
            wheels = new Wheel[4];
            var group = new GameObject("Roues").transform;
            group.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                var filter = meshes[i].GetComponent<MeshFilter>();
                Vector3 center = transform.InverseTransformPoint(meshes[i].TransformPoint(filter.sharedMesh.bounds.center));
                Bounds bounds = meshes[i].GetComponent<Renderer>().bounds;
                float radius = bounds.extents.y / Mathf.Abs(transform.lossyScale.y);
                var pivot = new GameObject(names[i]).transform;
                pivot.SetParent(group, false);
                pivot.localPosition = center;
                meshes[i].SetParent(pivot, true);
                meshes[i].name = "Mesh";
                var physics = new GameObject(names[i] + " - Suspension").transform;
                physics.SetParent(group, false);
                physics.localPosition = center + Vector3.up * (suspensionTravel * 0.25f);
                var collider = physics.gameObject.AddComponent<WheelCollider>();
                collider.radius = radius;
                wheels[i] = new Wheel { label = names[i], collider = collider, mesh = pivot,
                    steering = i < 2, driven = i >= 2 };
            }
        }
        foreach (var wheel in wheels)
            if (wheel == null || wheel.collider == null || wheel.mesh == null) return;
        RestBottom = float.PositiveInfinity;
        foreach (var wheel in wheels)
        {
            var collider = wheel.collider;
            collider.mass = 30f;
            collider.suspensionDistance = suspensionTravel;
            collider.suspensionSpring = new JointSpring { spring = springStrength, damper = springDamper, targetPosition = 0.5f };
            collider.forceAppPointDistance = 0.25f;
            collider.wheelDampingRate = 1f;
            collider.ConfigureVehicleSubsteps(10f, 5, 8);
            collider.forwardFriction = Friction(1.7f);
            collider.sidewaysFriction = Friction(wheel.steering ? frontGrip : rearStraightGrip);
            wheel.rotationOffset = Quaternion.Inverse(transform.rotation) * wheel.mesh.rotation;
            float bottom = transform.InverseTransformPoint(wheel.mesh.position).y - collider.radius;
            RestBottom = Mathf.Min(RestBottom, bottom);
        }
        body.mass = vehicleMass;
        body.centerOfMass = new Vector3(0f, 0.75f, -0.65f);
        body.constraints = (body.constraints & ~RigidbodyConstraints.FreezeRotationY) |
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.angularDamping = 2f;
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            float rise = Mathf.Max(0f, chassisClearance - (box.center.y - box.size.y * 0.5f));
            if (rise < box.size.y - 0.1f)
            {
                box.center += Vector3.up * (rise * 0.5f);
                box.size -= Vector3.up * rise;
            }
        }
        InitializeArcadeVisuals();
        initialized = true;
    }

    private WheelFrictionCurve Friction(float stiffness)
        => new WheelFrictionCurve { extremumSlip = 0.35f, extremumValue = 1f,
            asymptoteSlip = 0.8f, asymptoteValue = 0.65f, stiffness = stiffness };

    private void UpdateProgressiveControls(float throttle, float steering, float deltaTime)
    {
        throttle = Mathf.Clamp(throttle, -1f, 1f);
        steering = Mathf.Clamp(steering, -1f, 1f);
        float buildTime = Mathf.Max(0.01f, driftBuildUpSeconds);
        if (Mathf.Abs(steering) > 0.001f)
        {
            float direction = Mathf.Sign(steering);
            if (direction != steeringMemoryDirection)
            {
                steeringCharge = 0f;
                steeringMemoryDirection = direction;
            }
            steeringCharge = Mathf.MoveTowards(steeringCharge, 1f, deltaTime / buildTime);
            // Courbe exponentielle normalisee: vive au debut, douce pres du maximum.
            float strength = Mathf.Max(0.1f, steeringCurveStrength);
            float curve = (1f - Mathf.Exp(-strength * steeringCharge)) / (1f - Mathf.Exp(-strength));
            float target = steering * curve;
            progressiveSteering = Mathf.Lerp(progressiveSteering, target,
                1f - Mathf.Exp(-steeringCatchUpResponse * deltaTime));
            if (steeringCharge >= 0.99999f) progressiveSteering = steering;
        }
        else
        {
            // La memoire perd exactement le temps de touche relache: aucune nouvelle attente de 2,5 s.
            steeringCharge = Mathf.MoveTowards(steeringCharge, 0f, deltaTime / buildTime);
            // La rotation visible revient plus vite, sans continuer un virage maximal en roue libre.
            float releaseRate = Mathf.Log(100f) / Mathf.Max(0.01f, steeringReturnSeconds);
            progressiveSteering *= Mathf.Exp(-releaseRate * deltaTime);
            if (Mathf.Abs(progressiveSteering) <= 0.01f) progressiveSteering = 0f;
        }
        // Rampe de commande commune aux deux modes de conduite.
        if (!Mathf.Approximately(throttle, throttleTarget))
        {
            throttleTransitionRate = Mathf.Abs(throttle - progressiveThrottle);
            throttleTarget = throttle;
        }
        progressiveThrottle = Mathf.MoveTowards(progressiveThrottle, throttle,
            throttleTransitionRate * deltaTime / Mathf.Max(0.01f, throttleResponseSeconds));
    }

    public void Drive(float throttle, float steering, bool handbrake, BusController settings)
    {
        if (!IsReady) return;
        if (fullArcade) { DriveArcade(throttle, steering, handbrake, settings); return; }
        UpdateProgressiveControls(throttle, steering, Time.fixedDeltaTime);
        throttle = progressiveThrottle;
        steering = progressiveSteering;
        float speed = Vector3.Dot(body.linearVelocity, body.rotation * Vector3.forward);
        float speedKmh = Mathf.Abs(speed) * 3.6f;
        float angle = steering * Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle,
            Mathf.Clamp01(speedKmh / Mathf.Max(1f, settings.maxSpeedKmh)));
        float drift = Mathf.Abs(steering) * Mathf.Clamp01((speedKmh - 8f) / 30f);
        bool recoveringDrift = drift < driftBlend;
        if (recoveringDrift && Mathf.Abs(steering) < 0.25f) exitBoostRemaining = 0.3f;
        else exitBoostRemaining = Mathf.Max(0f, exitBoostRemaining - Time.fixedDeltaTime);
        driftBlend = Mathf.MoveTowards(driftBlend, drift,
            (recoveringDrift ? rearGripRecovery : driftResponse) * Time.fixedDeltaTime);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        float signedPlanarSpeed = planarVelocity.magnitude * (speed < 0f ? -1f : 1f);
        // La rampe de commande sert de cible de vitesse en mode roues.
        float targetSpeed = throttle * (throttle < 0f ? settings.maxReverseSpeedKmh : settings.maxSpeedKmh) / 3.6f;
        bool slowing = Mathf.Abs(targetSpeed) < planarVelocity.magnitude - 0.1f;
        bool braking = throttle > 0f && speed < -0.5f || throttle < 0f && speed > 0.5f || slowing;
        float acceleration = throttle < 0f ? settings.reverseAcceleration : settings.acceleration;
        float limit = (throttle < 0f ? settings.maxReverseSpeedKmh : settings.maxSpeedKmh) / 3.6f;
        foreach (var wheel in wheels)
        {
            var collider = wheel.collider;
            collider.steerAngle = wheel.steering ? angle : 0f;
            float rearGrip = handbrake ? handbrakeRearGrip : Mathf.Lerp(rearStraightGrip, rearDriftGrip, driftBlend);
            collider.sidewaysFriction = Friction(wheel.steering ? frontGrip : rearGrip);
            collider.motorTorque = wheel.driven && !braking && !handbrake && Mathf.Abs(speed) < limit
                ? throttle * vehicleMass * acceleration * collider.radius * 0.5f : 0f;
            float brake = braking ? settings.brakeDeceleration : throttle == 0f ? settings.naturalDeceleration : 0f;
            if (Mathf.Abs(speed) > limit + 1f) brake = Mathf.Max(brake, settings.brakeDeceleration);
            collider.brakeTorque = vehicleMass * brake * collider.radius * 0.25f;
            if (handbrake && wheel.driven)
                collider.brakeTorque = vehicleMass * settings.handbrakeDeceleration * collider.radius * 0.5f;
        }
        // Accompagne la cible de vitesse sans casser l'angle du drift ni la gravite.
        if (IsGrounded && !handbrake)
        {
            float responseAcceleration = Mathf.Max(settings.maxSpeedKmh, settings.maxReverseSpeedKmh) /
                (3.6f * Mathf.Max(0.01f, throttleResponseSeconds));
            float recoveryBoost = throttle > 0f && exitBoostRemaining > 0f ? driftExitBoost : 1f;
            float correction = (targetSpeed - signedPlanarSpeed) * accelerationAssist * recoveryBoost /
                Time.fixedDeltaTime;
            correction = Mathf.Clamp(correction, -responseAcceleration * 2f, responseAcceleration * 2f);
            Vector3 driveDirection = planarVelocity.magnitude > 0.5f
                ? planarVelocity.normalized * (speed < 0f ? -1f : 1f)
                : body.rotation * Vector3.forward;
            body.AddForce(driveDirection * correction, ForceMode.Acceleration);
        }
        // Les roues restent directrices; cette aide donne une reponse franche meme en glisse.
        Vector3 angular = body.angularVelocity;
        if (IsGrounded)
        {
            float speedFactor = Mathf.Lerp(1f, highSpeedTurnAuthority,
                Mathf.Clamp01(speedKmh / Mathf.Max(1f, settings.maxSpeedKmh)));
            // Borner le virage maximal avant la rampe evite de le saturer avant 2,5 secondes.
            float fullYaw = settings.maxSteeringSpeed * tightTurnMultiplier * speedFactor *
                Mathf.Clamp01(speedKmh / 8f) * (speed < 0f ? -1f : 1f) * Mathf.Deg2Rad;
            if (handbrake) fullYaw *= settings.handbrakeSteeringMultiplier;
            float desiredYaw = steering * Mathf.Clamp(fullYaw, -maxYawRate, maxYawRate);
            angular.y = Mathf.Lerp(angular.y, desiredYaw,
                1f - Mathf.Exp(-steeringAssistResponse * Time.fixedDeltaTime));
        }
        angular.y = Mathf.Clamp(angular.y, -maxYawRate, maxYawRate);
        body.angularVelocity = angular;
        // Les roues produisent le mouvement; cette aide conserve un angle de glisse controlable.
        Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        if (IsGrounded && horizontal.magnitude > 2f)
        {
            float slip = driftAngle * driftBlend * Mathf.Sign(steering);
            if (handbrake) slip *= 1.5f;
            Vector3 direction = body.rotation * Quaternion.Euler(0f, -slip, 0f) *
                Vector3.forward * (speed < 0f ? -1f : 1f);
            float response = recoveringDrift || Mathf.Abs(steering) < 0.15f
                ? driftRecoveryResponse
                : Mathf.Lerp(trajectoryResponse, turnTrajectoryResponse, Mathf.Abs(steering));
            if (handbrake) response = trajectoryResponse * 0.45f;
            Vector3 heading = Vector3.Slerp(horizontal.normalized, direction,
                1f - Mathf.Exp(-response * Time.fixedDeltaTime)).normalized;
            // Une petite derive visuelle suffit; la trajectoire doit suivre la rotation du bus.
            Vector3 forward = body.rotation * Vector3.forward * (speed < 0f ? -1f : 1f);
            float slipLimit = handbrake ? maxArcadeSlipAngle * 2f : maxArcadeSlipAngle;
            Vector3 limitedHeading = Vector3.RotateTowards(forward, heading,
                slipLimit * Mathf.Deg2Rad, 0f).normalized;
            // Ramene la glisse progressivement, sans recaler la vitesse d'un coup sur un rail.
            heading = Vector3.Slerp(heading, limitedHeading,
                1f - Mathf.Exp(-response * Time.fixedDeltaTime)).normalized;
            body.linearVelocity = heading * horizontal.magnitude + Vector3.up * body.linearVelocity.y;
        }
    }

    private void InitializeArcadeVisuals()
    {
        visualPivot = transform.Find("Carrosserie - Animation arcade");
        if (visualPivot != null) return;
        // Le pivot anime la carrosserie et les poses visuelles des roues; la physique reste sur le chassis.
        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<MeshRenderer>() == null ||
                child.GetComponentInChildren<Collider>() != null) continue;
            bool containsWheel = false;
            foreach (var wheel in wheels)
                if (wheel.mesh.IsChildOf(child)) containsWheel = true;
            if (containsWheel) continue;
            visualPivot = new GameObject("Carrosserie - Animation arcade").transform;
            visualPivot.SetParent(transform, false);
            visualPivot.localPosition = new Vector3(0f, 0.8f, -0.6f);
            child.SetParent(visualPivot, true);
            break;
        }
    }

    private void DriveArcade(float throttle, float steering, bool handbrake, BusController settings)
    {
        float dt = Time.fixedDeltaTime;
        float rawThrottle = Mathf.Clamp(throttle, -1f, 1f);
        UpdateProgressiveControls(throttle, steering, dt);
        Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Vector3 forward = body.rotation * Vector3.forward;
        float speed = horizontal.magnitude;
        float signedSpeed = speed * (Vector3.Dot(horizontal, forward) < -0.1f ? -1f : 1f);
        bool grounded = IsGrounded;
        if (Mathf.Abs(previousRawSteering) > 0.35f && Mathf.Abs(steering) < 0.05f &&
            speed > 5f && rawThrottle > 0f && grounded)
            arcadeBoostRemaining = Mathf.Max(0f, arcadeBoostSeconds);
        previousRawSteering = steering;
        arcadeBoostRemaining = Mathf.Max(0f, arcadeBoostRemaining - dt);
        bool boosting = arcadeBoostRemaining > 0f;
        float maxSpeed = settings.maxSpeedKmh / 3.6f;
        float targetSpeed = rawThrottle < 0f ? -settings.maxReverseSpeedKmh / 3.6f : maxSpeed;
        if (boosting && rawThrottle > 0f) targetSpeed *= driftExitBoost;
        if (grounded)
        {
            if (handbrake)
                signedSpeed = Mathf.MoveTowards(signedSpeed, 0f, arcadeBrakeDeceleration * 0.45f * dt);
            else if (Mathf.Abs(rawThrottle) < 0.01f)
                signedSpeed = Mathf.MoveTowards(signedSpeed, 0f, arcadeCoastDeceleration * dt);
            else
            {
                bool braking = signedSpeed * rawThrottle < -0.2f;
                float acceleration = braking ? arcadeBrakeDeceleration :
                    arcadeAcceleration * Mathf.Lerp(0.35f, 1f, Mathf.Abs(progressiveThrottle));
                if (boosting && !braking) acceleration *= 1.35f;
                signedSpeed = Mathf.MoveTowards(signedSpeed, targetSpeed, acceleration * dt);
            }
            float speedRatio = Mathf.Clamp01(Mathf.Abs(signedSpeed) / Mathf.Max(1f, maxSpeed));
            float authority = Mathf.Lerp(1f, 0.78f, speedRatio);
            float yaw = progressiveSteering * arcadeTurnSpeed * authority *
                Mathf.Clamp01(Mathf.Abs(signedSpeed) / 2f) * (signedSpeed < 0f ? -1f : 1f);
            if (handbrake) yaw *= settings.handbrakeSteeringMultiplier;
            yaw = Mathf.Clamp(yaw * Mathf.Deg2Rad, -maxYawRate, maxYawRate);
            var angular = body.angularVelocity;
            angular.y = Mathf.Lerp(angular.y, yaw, 1f - Mathf.Exp(-steeringAssistResponse * dt));
            body.angularVelocity = angular;
            // Le virage entraine la trajectoire. La glisse est bornee et se rattrape seule.
            float slip = driftAngle * progressiveSteering * speedRatio * (handbrake ? 1.5f : 1f);
            Vector3 desiredHeading = body.rotation * Quaternion.Euler(0f, -slip, 0f) *
                Vector3.forward * (signedSpeed < 0f ? -1f : 1f);
            Vector3 heading = horizontal.magnitude > 0.5f && signedSpeed * Vector3.Dot(horizontal, forward) > 0f
                ? Vector3.Slerp(horizontal.normalized, desiredHeading,
                    1f - Mathf.Exp(-turnTrajectoryResponse * dt)).normalized : desiredHeading;
            heading = Vector3.RotateTowards(forward * (signedSpeed < 0f ? -1f : 1f), heading,
                maxArcadeSlipAngle * Mathf.Deg2Rad, 0f).normalized;
            body.linearVelocity = heading * Mathf.Abs(signedSpeed) + Vector3.up * body.linearVelocity.y;
        }
        float driftTarget = Mathf.Abs(progressiveSteering) * Mathf.Clamp01(speed / 4f);
        driftBlend = Mathf.MoveTowards(driftBlend, driftTarget, rearGripRecovery * dt);
        foreach (var wheel in wheels)
        {
            wheel.collider.steerAngle = wheel.steering ? progressiveSteering *
                Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed))) : 0f;
            // Les WheelCollider portent le bus et font tourner les meshes, sans combattre l'aide arcade.
            wheel.collider.sidewaysFriction = Friction(0.15f);
            wheel.collider.forwardFriction = Friction(0.15f);
            wheel.collider.motorTorque = 0f;
            wheel.collider.brakeTorque = handbrake && wheel.driven ? 250f : 0f;
        }
        visualAcceleration = Mathf.Clamp((signedSpeed - previousArcadeSpeed) / Mathf.Max(dt, 0.001f) / Mathf.Max(0.01f, arcadeAcceleration), -1f, 1f);
        previousArcadeSpeed = signedSpeed;
    }

    public void UpdateArcadeVisuals(float deltaTime)
    {
        if (visualPivot == null || !fullArcade || deltaTime <= 0f) return;
        float speedRatio = Mathf.Clamp01(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude / 6f);
        float turn = progressiveSteering * speedRatio;
        Vector3 target = new Vector3(-visualAcceleration * visualPitchAngle,
            turn * visualDriftAngle, -turn * visualRollAngle);
        visualAngles = Vector3.SmoothDamp(visualAngles, target, ref visualAngularVelocity,
            Mathf.Max(0.01f, visualTransitionSeconds), Mathf.Infinity, deltaTime);
        visualPivot.localRotation = Quaternion.Euler(visualAngles);
    }

    public void UpdateMeshes()
    {
        if (!IsReady) return;
        foreach (var wheel in wheels)
        {
            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            if (fullArcade && visualPivot != null)
            {
                // Applique le meme mouvement visuel aux moyeux et a la carrosserie.
                // La suspension, le braquage et la rotation propre de chaque roue sont conserves.
                Vector3 hub = transform.InverseTransformPoint(position);
                Vector3 pivot = visualPivot.localPosition;
                position = transform.TransformPoint(pivot + visualPivot.localRotation * (hub - pivot));
                Quaternion visualRotation = transform.rotation * visualPivot.localRotation *
                    Quaternion.Inverse(transform.rotation);
                rotation = visualRotation * rotation;
            }
            wheel.mesh.SetPositionAndRotation(position, rotation * wheel.rotationOffset);
        }
    }
    private void LateUpdate()
    {
        UpdateArcadeVisuals(Time.deltaTime);
        UpdateMeshes();
    }
    public void ResetWheels()
    {
        visualAngles = visualAngularVelocity = Vector3.zero;
        visualAcceleration = previousArcadeSpeed = previousRawSteering = arcadeBoostRemaining = 0f;
        if (visualPivot != null) visualPivot.localRotation = Quaternion.identity;
        driftBlend = 0f;
        exitBoostRemaining = 0f;
        progressiveSteering = 0f;
        steeringCharge = 0f;
        steeringMemoryDirection = 0f;
        progressiveThrottle = 0f;
        throttleTarget = 0f;
        throttleTransitionRate = 1f;
        foreach (var wheel in wheels)
            if (wheel != null && wheel.collider != null) { wheel.collider.motorTorque = 0f; wheel.collider.brakeTorque = 0f; wheel.collider.steerAngle = 0f; }
    }
    private void OnDisable() => ResetWheels();
}
