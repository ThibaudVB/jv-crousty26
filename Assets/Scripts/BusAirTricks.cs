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

    // Nombre de figures réussies (un tour complet = une figure)
    public int TricksLanded { get; private set; }
    // Appelé à l'atterrissage avec le nombre de tours faits pendant le saut
    public event System.Action<int> TrickLanded;

    private BusWheelRig wheelRig;
    private BusFlightController flight;
    private float airTime;
    private float pitch, yaw;              // angles de la figure en cours (en degrés, non limités à 360)
    private float pitchDirection, yawDirection; // sens de la dernière rotation, pour finir la figure dans le même sens

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
        if (flight != null && flight.IsAirborne)
        {
            ResetTrick();
            return;
        }

        if (wheelRig.IsGrounded)
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

    private void UpdateAirControls(float dt)
    {
        ReadInputs(out float flipInput, out float spinInput);

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

    // Continue dans le même sens jusqu'au prochain tour complet
    private float FinishTurn(float angle, float direction, float dt)
    {
        if (direction == 0f) return angle;
        float target = direction > 0f ? Mathf.Ceil(angle / 360f - 0.001f) * 360f : Mathf.Floor(angle / 360f + 0.001f) * 360f;
        return Mathf.MoveTowards(angle, target, finishSpeed * dt);
    }

    private void Land()
    {
        int turns = Mathf.RoundToInt(Mathf.Abs(NearestTurn(pitch)) / 360f) + Mathf.RoundToInt(Mathf.Abs(NearestTurn(yaw)) / 360f);
        if (turns > 0)
        {
            TricksLanded += turns;
            TrickLanded?.Invoke(turns);
        }
        pitchDirection = yawDirection = 0f;
    }

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
