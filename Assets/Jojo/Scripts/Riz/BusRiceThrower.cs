using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Compétence RIZ (touche E) : le bus jette une poignée de riz derrière lui.
// Le riz forme une flaque (RiceZone) sur la route pendant quelques secondes.
// Le script RiceSlowdown (sur le PoliceChaseManager) ralentit la police qui roule dessus.
//
// A placer à la main sur le bus (le GameObject qui a le BusController et le Rigidbody).
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusRiceThrower : MonoBehaviour
{
    [Header("Touche et charges")]
    public Key activationKey = Key.E;
    [Tooltip("Nombre d'utilisations au début de la partie")]
    [Range(0, 3)] public int startingCharges = 3;

    [Header("Flaque de riz")]
    [Tooltip("Rayon de la flaque de riz (m)")]
    public float slickRadius = 6f;
    [Tooltip("Durée pendant laquelle le riz reste sur la route (s)")]
    public float slickDuration = 10f;
    [Tooltip("Distance derrière le bus où le riz retombe (m)")]
    public float throwDistance = 6f;

    [Header("Grains")]
    [Min(4)] public int grainsPerThrow = 60;
    [Tooltip("Hauteur de la courbe des grains (m)")]
    public float grainArcHeight = 2f;
    public Vector3 grainSize = new Vector3(0.12f, 0.12f, 0.28f);
    public Color riceColor = new Color(0.98f, 0.97f, 0.9f);

    [Header("Référence (cherchée automatiquement si vide)")]
    [Tooltip("Affiche les charges restantes")]
    public CroustyBonusHUD bonusHud;

    // Charges restantes (lu par le HUD)
    public int UsesRemaining { get; private set; }

    // Un grain en train de voler vers la route
    private class FlyingGrain
    {
        public Transform transform;
        public Vector3 start, end;
        public float age, flightTime;
    }
    private readonly List<FlyingGrain> flyingGrains = new List<FlyingGrain>();

    private Rigidbody rb;
    private BusController drive;
    private BusFlightController flight;
    private BoxCollider body;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        drive = GetComponent<BusController>();
        flight = GetComponent<BusFlightController>();
        body = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (bonusHud == null)
            bonusHud = FindAnyObjectByType<CroustyBonusHUD>();

        UsesRemaining = startingCharges;
        RefreshHud();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame)
            TryActivate();

        MoveFlyingGrains();
    }

    // Lance le riz. Renvoie faux si ce n'est pas possible (plus de charges, bus en vol...).
    public bool TryActivate()
    {
        bool busCanThrow = drive != null && drive.enabled && !rb.isKinematic;
        bool busIsFlying = flight != null && flight.IsAirborne;
        if (!isActiveAndEnabled || Time.timeScale <= 0f || UsesRemaining <= 0 || !busCanThrow || busIsFlying)
            return false;

        UsesRemaining--;
        RefreshHud();
        CroustySoundDesignManager.Play(CroustySound.Paper, transform.position);

        // Direction "derrière le bus", à plat
        Vector3 back = -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float halfLength = body != null ? body.size.z * Mathf.Abs(transform.lossyScale.z) * 0.5f : 3f;
        Vector3 center = body != null ? body.bounds.center : rb.position;
        Vector3 rear = center + back * halfLength;

        // 1. On pose la flaque sur la route
        GameObject slick = new GameObject("Flaque de riz");
        slick.transform.position = GroundPoint(rear + back * throwDistance);
        RiceZone zone = slick.AddComponent<RiceZone>();
        zone.radius = slickRadius;
        zone.duration = slickDuration;

        // 2. Les grains partent de l'arrière du bus et retombent dans la flaque
        for (int i = 0; i < grainsPerThrow; i++)
        {
            Color color = i % 3 == 0 ? riceColor * 0.85f : riceColor;
            GameObject grain = JojoVisuals.CreateShape(PrimitiveType.Cube, slick.transform, color);
            grain.transform.localScale = grainSize;
            grain.transform.rotation = Random.rotation;

            Vector2 spot = Random.insideUnitCircle * slickRadius;
            FlyingGrain flying = new FlyingGrain();
            flying.transform = grain.transform;
            flying.start = rear + Vector3.up * 1.5f + Random.insideUnitSphere * 0.4f;
            flying.end = GroundPoint(slick.transform.position + new Vector3(spot.x, 0f, spot.y)) + Vector3.up * 0.06f;
            flying.flightTime = Random.Range(0.35f, 0.7f);
            grain.transform.position = flying.start;
            flyingGrains.Add(flying);
        }

        return true;
    }

    private void RefreshHud()
    {
        if (bonusHud != null)
            bonusHud.SetRiceCharges(UsesRemaining);
    }

    // Fait voler les grains en cloche jusqu'à la route
    private void MoveFlyingGrains()
    {
        // On parcourt la liste à l'envers pour pouvoir retirer des éléments
        for (int i = flyingGrains.Count - 1; i >= 0; i--)
        {
            FlyingGrain grain = flyingGrains[i];

            // La flaque a déjà disparu (et le grain avec)
            if (grain.transform == null)
            {
                flyingGrains.RemoveAt(i);
                continue;
            }

            grain.age += Time.deltaTime;
            float t = Mathf.Min(grain.age / grain.flightTime, 1f); // 0 = départ, 1 = arrivée

            Vector3 position = Vector3.Lerp(grain.start, grain.end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * grainArcHeight;
            grain.transform.position = position;

            if (t >= 1f)
                flyingGrains.RemoveAt(i); // posé sur la route
            else
                grain.transform.Rotate(720f * Time.deltaTime, 360f * Time.deltaTime, 0f);
        }
    }

    // Trouve le sol sous une position (en ignorant le bus et les objets qui bougent)
    private Vector3 GroundPoint(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float highest = float.NegativeInfinity;

        foreach (RaycastHit hit in hits)
        {
            bool isBus = hit.collider.transform.IsChildOf(transform);
            bool isMoving = hit.rigidbody != null;
            bool isFlat = hit.normal.y > 0.65f;
            bool notTooHigh = hit.point.y <= position.y + 2f;

            if (!isBus && !isMoving && isFlat && notTooHigh)
                highest = Mathf.Max(highest, hit.point.y);
        }

        if (!float.IsNegativeInfinity(highest))
            position.y = highest;
        return position;
    }
}
