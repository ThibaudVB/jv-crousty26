using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace V1
{
    // Conduite du bus : accélération, freinage, direction, adhérence,
    // dérapage au frein à main avec boost à la sortie (façon Mario Kart).
    //
    // Clavier : Z/W/↑ avancer, S/↓ freiner/reculer, Q/A/← et D/→ tourner,
    //           Espace frein à main / dérapage, R remettre le bus droit.
    // Manette : gâchette droite avancer, gâchette gauche freiner, stick gauche tourner,
    //           A (bouton du bas) dérapage, Select remettre le bus droit.
    //
    // Ajoute tout seul BusHorn (klaxon) et BusDriftEffects (traces, fumée, sons).
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

        [Header("Dérapage")]
        [Tooltip("Vitesse minimale pour commencer un dérapage")]
        public float driftMinSpeedKmh = 35f;
        [Tooltip("Adhérence latérale pendant le dérapage (plus petit = glisse plus)")]
        public float driftGrip = 4f;
        [Tooltip("Ralentissement pendant le dérapage si on n'accélère pas")]
        public float driftDeceleration = 4f;
        [Tooltip("Virage le plus large / le plus serré pendant un dérapage (multiplie la direction)")]
        public float driftWideTurn = 0.45f;
        public float driftTightTurn = 1.5f;

        [Header("Boost de sortie de dérapage")]
        [Tooltip("Durée de dérapage (s) pour atteindre les niveaux 1, 2 et 3")]
        public float[] boostChargeTimes = { 0.8f, 1.8f, 3f };
        [Tooltip("Vitesse ajoutée (km/h) pour les niveaux 1, 2 et 3")]
        public float[] boostSpeedKmh = { 15f, 25f, 40f };
        [Tooltip("Durée du boost (s) pour les niveaux 1, 2 et 3")]
        public float[] boostDurations = { 0.6f, 1f, 1.5f };
        public float boostAcceleration = 30f;

        [Header("Reset vehicule")]
        public Key resetKey = Key.R;
        public float resetHeight = 2f;

        [Header("Debug")]
        [SerializeField] private float currentSpeedKmh;
        [SerializeField] private float signedSpeedKmh;
        [SerializeField] private bool isReversing;
        [SerializeField] private bool isHandbraking;

        // Déclenché à la sortie d'un dérapage chargé, avec le niveau du boost (1 à 3)
        public event Action<int> Boosted;

        public float SpeedKmh => currentSpeedKmh;
        public float SignedSpeedKmh => signedSpeedKmh;
        public bool IsHandbraking => isHandbraking;
        public bool IsDrifting { get; private set; }
        // -1 = dérape vers la gauche, 1 = vers la droite
        public int DriftDirection { get; private set; }
        public float DriftTime { get; private set; }
        // Niveau de boost déjà chargé pendant le dérapage en cours (0 à 3)
        public int DriftLevel { get; private set; }
        public bool IsBoosting => boostTimer > 0f;
        public int BoostLevel { get; private set; }
        public float LastBoostTime { get; private set; } = -999f;
        public int LastBoostLevel { get; private set; }
        // Angle (degrés) entre la direction du bus et sa trajectoire
        public float SlipAngle { get; private set; }

        private Rigidbody rb;

        private float throttleInput;
        private float steeringInput;
        private float currentSteering;
        private bool handbrakeInput;

        private float boostTimer;
        private float boostBonus;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            if (GetComponent<BusHorn>() == null)
                gameObject.AddComponent<BusHorn>();
            if (GetComponent<BusDriftEffects>() == null)
                gameObject.AddComponent<BusDriftEffects>();
        }

        private void Update()
        {
            ReadInputs();

            currentSteering = Mathf.MoveTowards(
                currentSteering,
                steeringInput,
                steeringResponse * Time.deltaTime
            );

            if (ResetPressed())
                ResetVehicle();
        }

        private void FixedUpdate()
        {
            UpdateDebug();
            UpdateDrift();

            ApplyLongitudinalMovement();
            ApplySteering();
            ApplyLateralGrip();
        }

        // ---------- ENTREES ----------

        private void ReadInputs()
        {
            throttleInput = 0f;
            steeringInput = 0f;
            handbrakeInput = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                    throttleInput = 1f;

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                    throttleInput = -1f;

                if (keyboard.qKey.isPressed || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    steeringInput = -1f;

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    steeringInput = 1f;

                handbrakeInput = keyboard.spaceKey.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float gas = gamepad.rightTrigger.ReadValue();
                float brake = gamepad.leftTrigger.ReadValue();
                if (gas > 0.1f || brake > 0.1f)
                    throttleInput = gas >= brake ? gas : -brake;

                float stick = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > 0.15f)
                    steeringInput = stick;

                handbrakeInput |= gamepad.buttonSouth.isPressed;
            }
        }

        private bool ResetPressed()
        {
            if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
                return true;

            return Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        }

        // ---------- DERAPAGE ----------

        private void UpdateDrift()
        {
            if (!IsDrifting)
            {
                // On commence à déraper : frein à main + virage + assez de vitesse en marche avant
                if (handbrakeInput &&
                    Mathf.Abs(steeringInput) > 0.3f &&
                    signedSpeedKmh > driftMinSpeedKmh)
                {
                    IsDrifting = true;
                    DriftDirection = steeringInput > 0f ? 1 : -1;
                    DriftTime = 0f;
                    DriftLevel = 0;
                }
                return;
            }

            // Trop lent ou marche arrière : le dérapage s'arrête sans boost
            if (signedSpeedKmh < driftMinSpeedKmh * 0.5f)
            {
                StopDrift(false);
                return;
            }

            // Frein à main relâché : sortie de dérapage, boost si assez chargé
            if (!handbrakeInput)
            {
                StopDrift(true);
                return;
            }

            DriftTime += Time.fixedDeltaTime;

            DriftLevel = 0;
            for (int i = 0; i < boostChargeTimes.Length; i++)
                if (DriftTime >= boostChargeTimes[i])
                    DriftLevel = i + 1;
        }

        private void StopDrift(bool allowBoost)
        {
            int level = DriftLevel;

            IsDrifting = false;
            DriftTime = 0f;
            DriftLevel = 0;

            if (!allowBoost || level <= 0)
                return;

            int index = Mathf.Min(level, boostSpeedKmh.Length, boostDurations.Length) - 1;
            if (index < 0)
                return;

            BoostLevel = level;
            LastBoostLevel = level;
            boostBonus = boostSpeedKmh[index] / 3.6f;
            boostTimer = boostDurations[index];
            LastBoostTime = Time.time;
            Boosted?.Invoke(level);
        }

        // ---------- PHYSIQUE ----------

        private void ApplyLongitudinalMovement()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float forwardSpeed = localVelocity.z;

            float maxForwardSpeed = maxSpeedKmh / 3.6f;
            float maxReverseSpeed = maxReverseSpeedKmh / 3.6f;

            if (boostTimer > 0f)
            {
                boostTimer -= Time.fixedDeltaTime;

                // Le boost pousse au-delà de la vitesse max
                forwardSpeed = Mathf.MoveTowards(
                    forwardSpeed,
                    maxForwardSpeed + boostBonus,
                    boostAcceleration * Time.fixedDeltaTime
                );
            }
            else if (IsDrifting)
            {
                // En dérapage, le frein à main ne bloque pas les roues : on garde l'élan
                float target = throttleInput > 0f ? maxForwardSpeed : 0f;
                float rate = throttleInput > 0f ? acceleration * 0.5f : driftDeceleration;
                if (throttleInput < 0f)
                    rate = brakeDeceleration;

                forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * Time.fixedDeltaTime);
            }
            else if (handbrakeInput)
            {
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, handbrakeDeceleration * Time.fixedDeltaTime);
            }
            else if (throttleInput > 0f)
            {
                // Si on recule, avancer freine d'abord
                if (forwardSpeed < -0.5f)
                    forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * Time.fixedDeltaTime);
                else if (forwardSpeed > maxForwardSpeed)
                    // Après un boost : on revient doucement à la vitesse max
                    forwardSpeed = Mathf.MoveTowards(forwardSpeed, maxForwardSpeed, naturalDeceleration * Time.fixedDeltaTime);
                else
                    forwardSpeed = Mathf.MoveTowards(forwardSpeed, maxForwardSpeed,
                        acceleration * throttleInput * Time.fixedDeltaTime);
            }
            else if (throttleInput < 0f)
            {
                // Si on avance, reculer freine d'abord
                if (forwardSpeed > 0.5f)
                    forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f,
                        brakeDeceleration * -throttleInput * Time.fixedDeltaTime);
                else
                    forwardSpeed = Mathf.MoveTowards(forwardSpeed, -maxReverseSpeed,
                        reverseAcceleration * -throttleInput * Time.fixedDeltaTime);
            }
            else
            {
                // Roue libre
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, naturalDeceleration * Time.fixedDeltaTime);
            }

            localVelocity.z = forwardSpeed;

            Vector3 worldVelocity = transform.TransformDirection(localVelocity);

            // Laisse Unity gérer la chute / gravité
            worldVelocity.y = rb.linearVelocity.y;

            rb.linearVelocity = worldVelocity;
        }

        private void ApplySteering()
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

            // Impossible de tourner quasiment sur place
            if (speedKmh < 2f)
                return;

            float lowSpeedFactor = Mathf.Clamp01(speedKmh / 8f);

            float highSpeedFactor = Mathf.Lerp(
                1f,
                highSpeedSteeringMultiplier,
                Mathf.Clamp01(speedKmh / maxSpeedKmh)
            );

            float reverseDirection = forwardSpeed >= 0f ? 1f : -1f;

            float steering = currentSteering;
            if (IsDrifting)
            {
                // Le sens du virage est verrouillé : la direction règle seulement
                // si on serre (même sens) ou si on élargit (contre-braquage)
                float tightness = (currentSteering * DriftDirection + 1f) * 0.5f;
                steering = DriftDirection * Mathf.Lerp(driftWideTurn, driftTightTurn, tightness);
                highSpeedFactor = Mathf.Max(highSpeedFactor, 0.6f);
            }

            float rotationAmount =
                steering *
                maxSteeringSpeed *
                lowSpeedFactor *
                highSpeedFactor *
                reverseDirection *
                Time.fixedDeltaTime;

            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, rotationAmount, 0f));
        }

        private void ApplyLateralGrip()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

            float grip = IsDrifting ? driftGrip
                : handbrakeInput ? handbrakeGrip
                : lateralGrip;

            localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, grip * Time.fixedDeltaTime);

            Vector3 correctedVelocity = transform.TransformDirection(localVelocity);
            correctedVelocity.y = rb.linearVelocity.y;

            rb.linearVelocity = correctedVelocity;
        }

        private void ResetVehicle()
        {
            // Conserve uniquement la direction horizontale actuelle
            float currentYaw = transform.eulerAngles.y;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Remonte légèrement le bus et le remet à plat
            rb.position = transform.position + Vector3.up * resetHeight;
            rb.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            currentSteering = 0f;
            boostTimer = 0f;
            StopDrift(false);
        }

        private void UpdateDebug()
        {
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            signedSpeedKmh = forwardSpeed * 3.6f;
            currentSpeedKmh = Mathf.Abs(signedSpeedKmh);
            isReversing = signedSpeedKmh < -0.5f;
            isHandbraking = handbrakeInput;

            Vector3 flatVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            SlipAngle = flatVelocity.sqrMagnitude > 1f
                ? Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), flatVelocity)
                : 0f;

            if (boostTimer <= 0f)
                BoostLevel = 0;
        }
    }
}
