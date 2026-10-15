using UnityEngine;

// Figures en l'air (après un tremplin, une rampe...) :
//   Z / Haut   -> galipette avant
//   S / Bas    -> galipette arrière
//   Q / Gauche -> 360 vers la gauche
//   D / Droite -> 360 vers la droite
// Manette : stick gauche.
//
// Les figures sont seulement visuelles : on fait tourner la carrosserie (et les roues),
// le bus physique reste droit. Donc pas de risque d'atterrir sur le toit, et la caméra ne fait pas de loopings.
// Quand on lâche, la figure se termine toute seule jusqu'au tour complet le plus proche.
// À ajouter à la main sur le bus (Add Component > BusAirTricks), à côté de BusWheelRig.
// Les vitesses se règlent dans l'Inspector.
[RequireComponent(typeof(BusWheelRig))]
public class BusAirTricks : MonoBehaviour
{
    [Header("Vitesse des figures (degrés / seconde)")]
    [Tooltip("Galipettes avant / arrière. 360 = un tour par seconde")]
    [Min(0f)] public float flipSpeed = 240f;
    [Tooltip("360 gauche / droite. 360 = un tour par seconde")]
    [Min(0f)] public float spinSpeed = 270f;

    [Header("Fin de figure")]
    [Tooltip("Vitesse à laquelle la carrosserie finit son tour quand on lâche (degrés / seconde)")]
    [Min(0f)] public float finishSpeed = 220f;
    [Tooltip("Vitesse du redressement à l'atterrissage (degrés / seconde)")]
    public float landingSnapSpeed = 900f;

    [Header("Détection du saut")]
    [Tooltip("Temps en l'air (s) avant de pouvoir faire une figure : évite les petites bosses")]
    public float minAirTime = 0.15f;
    [Tooltip("Hauteur minimale au-dessus du sol (m) pour faire une figure. En dessous, les touches ne font plus " +
             "tourner le bus : la figure en cours se termine toute seule.")]
    [Min(0f)] public float minHeightAboveGround = 4f;
    [Tooltip("Couches considérées comme le sol pour mesurer la hauteur")]
    public LayerMask groundLayers = ~0;

    [Header("Récompense des figures")]
    [Tooltip("Écart maximal (degrés) avec un tour complet pour que l'atterrissage compte comme réussi")]
    [Range(0f, 180f)] public float landingTolerance = 35f;
    [Tooltip("Durée du boost de points après une figure réussie (s)")]
    [Min(0f)] public float boostDuration = 10f;
    [Tooltip("Bonus par tour : 0.5 = x1.5 pour 1 tour, x2 pour 2 tours, x2.5 pour 3 tours...")]
    [Min(0f)] public float boostPerTurn = 0.5f;
    [Tooltip("Multiplicateur de points maximum")]
    [Min(1f)] public float maxBoostMultiplier = 4f;
    [Tooltip("Après une figure réussie, le bus ne prend pas de dégâts pendant ce temps (s)")]
    [Min(0f)] public float safeLandingSeconds = 0.75f;

    // Nombre de figures réussies (un tour complet = une figure)
    public int TricksLanded { get; private set; }
    // Appelé quand on atterrit une figure réussie, avec le nombre de tours faits pendant le saut
    public event System.Action<int> TrickLanded;

    private BusWheelRig wheelRig;
    private BusFlightController flight;
    private float airTime;
    private float pitch, yaw;              // angles de la figure en cours (en degrés, non limités à 360)
    private float pitchDirection, yawDirection; // sens de la dernière rotation, pour finir la figure dans le même sens
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private float safeUntil;

    [Header("Debug")]
    [Tooltip("Hauteur actuelle au-dessus du sol pendant un saut (m), pour aider à régler la hauteur minimale")]
    [SerializeField] private float currentHeight;
    public float HeightAboveGround => currentHeight;

    private void Awake()
    {
        wheelRig = GetComponent<BusWheelRig>();
        flight = GetComponent<BusFlightController>();
    }

    private void Update()
    {
        if (wheelRig == null || !wheelRig.IsReady || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;

        // Pendant le pouvoir de vol, les touches servent à piloter : pas de figure
        if (flight != null && flight.CurrentMode == BusFlightController.FlightMode.Flying)
        {
            ResetTrick();
            return;
        }

        if ((flight == null || !flight.IsFalling) && wheelRig.IsGrounded)
        {
            if (airTime > 0f) Land();
            airTime = 0f;
            // Redressement rapide si on a atterri au milieu d'une figure
            pitch = Mathf.MoveTowards(pitch, NearestTurn(pitch), landingSnapSpeed * dt);
            yaw = Mathf.MoveTowards(yaw, NearestTurn(yaw), landingSnapSpeed * dt);
        }
        else
        {
            airTime += dt;
            if (airTime >= minAirTime) UpdateAirControls(dt);
        }

        wheelRig.trickRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
    }

    private void LateUpdate()
    {
        // Falling keeps the driving rig disabled to preserve inertia. Update only
        // its existing visual animation, without enabling tire physics or steering.
        if (flight == null || !flight.IsFalling || wheelRig == null ||
            wheelRig.enabled || !wheelRig.IsReady || Time.timeScale <= 0f) return;
        wheelRig.UpdateArcadeVisuals(Time.deltaTime);
        wheelRig.UpdateMeshes();
    }

    private void UpdateAirControls(float dt)
    {
        ReadInputs(out float flipInput, out float spinInput);

        // Trop près du sol : on ne peut plus lancer ou continuer une figure, elle se termine seule
        currentHeight = MeasureHeight();
        if (currentHeight < minHeightAboveGround)
            flipInput = spinInput = 0f;

        if (Mathf.Abs(flipInput) > 0.2f)
        {
            pitchDirection = Mathf.Sign(flipInput);
            pitch += flipInput * flipSpeed * dt;
        }
        else pitch = FinishTurn(pitch, pitchDirection, dt);

        if (Mathf.Abs(spinInput) > 0.2f)
        {
            yawDirection = Mathf.Sign(spinInput);
            yaw += spinInput * spinSpeed * dt;
        }
        else yaw = FinishTurn(yaw, yawDirection, dt);
    }

    // Rayon vers le bas depuis le bus, en ignorant ses propres colliders et les triggers
    private float MeasureHeight()
    {
        const float maxDistance = 1000f;
        int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, groundHits, maxDistance,
            groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].collider.transform.IsChildOf(transform)) continue;
            best = Mathf.Min(best, groundHits[i].distance);
        }
        return best;
    }

    // Continue dans le même sens jusqu'au prochain tour complet
    private float FinishTurn(float angle, float direction, float dt)
    {
        if (direction == 0f) return angle;
        float target = direction > 0f ? Mathf.Ceil(angle / 360f - 0.001f) * 360f : Mathf.Floor(angle / 360f + 0.001f) * 360f;
        return Mathf.MoveTowards(angle, target, finishSpeed * dt);
    }

    private void Land()
    {
        int turns = CountTurns();
        if (turns > 0 && IsStraight())
        {
            // Figure réussie : boost de points, et pas de dégâts pendant un court instant
            TricksLanded += turns;
            safeUntil = Time.time + safeLandingSeconds;
            float multiplier = Mathf.Min(maxBoostMultiplier, 1f + boostPerTurn * turns);
            if (DeliveryManager.Instance != null)
                DeliveryManager.Instance.StartScoreBoost(multiplier, boostDuration);
            TrickLanded?.Invoke(turns);
        }
        // Les tours complets ne comptent plus pour le prochain saut (même rotation à l'écran)
        pitch -= NearestTurn(pitch);
        yaw -= NearestTurn(yaw);
        pitchDirection = yawDirection = 0f;
    }

    private int CountTurns() =>
        Mathf.RoundToInt(Mathf.Abs(NearestTurn(pitch)) / 360f) + Mathf.RoundToInt(Mathf.Abs(NearestTurn(yaw)) / 360f);

    // Le bus est « à peu près droit » : chaque rotation est proche d'un tour complet
    private bool IsStraight() =>
        Mathf.Abs(pitch - NearestTurn(pitch)) <= landingTolerance && Mathf.Abs(yaw - NearestTurn(yaw)) <= landingTolerance;

    // Utilisé par BusDamage : vrai juste après une figure réussie, ou au moment même de l'atterrissage
    // (BusDamage peut voir l'atterrissage avant ce script)
    public bool ProtectsLanding =>
        isActiveAndEnabled && (Time.time < safeUntil ||
            airTime > 0f && wheelRig != null && wheelRig.IsGrounded && CountTurns() > 0 && IsStraight());

    private static float NearestTurn(float angle) => Mathf.Round(angle / 360f) * 360f;

    private void ReadInputs(out float flip, out float spin)
    {
        flip = spin = 0f;
        var pad = CroustyInput.Pad;
        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            flip = stick.y;
            spin = stick.x;
        }

        var keyboard = CroustyInput.Keyboard;
        if (keyboard == null) return;
        // Mêmes touches que la conduite (BusController)
        if (keyboard.zKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) flip = 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) flip = -1f;
        if (keyboard.qKey.isPressed || keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) spin = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) spin = 1f;
    }

    // Remet la carrosserie droite d'un coup (reset du bus, début du vol...)
    public void ResetTrick()
    {
        pitch = yaw = pitchDirection = yawDirection = airTime = 0f;
        if (wheelRig != null) wheelRig.trickRotation = Quaternion.identity;
    }

    private void OnDisable() => ResetTrick();
}
