using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Compétence RIZ (touche E) : le bus jette une poignée de riz derrière lui.
// Le riz reste sur la route un moment : la police qui roule dessus glisse et ralentit.
// Si la police est collée au bus au moment du jet, elle est ralentie tout de suite.
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

    [Header("Riz sur la route")]
    [Tooltip("Rayon de la flaque de riz (m)")]
    public float slickRadius = 6f;
    [Tooltip("Durée pendant laquelle le riz reste sur la route (s)")]
    public float slickDuration = 10f;
    [Tooltip("Distance derrière le bus où le riz retombe (m)")]
    public float throwDistance = 6f;
    [Tooltip("Hauteur de la courbe des grains (m)")]
    public float grainArcHeight = 2f;
    [Min(4)] public int grainsPerThrow = 60;
    public Vector3 grainSize = new Vector3(0.12f, 0.12f, 0.28f);
    public Color riceColor = new Color(0.98f, 0.97f, 0.9f);

    [Header("Effet sur la police")]
    [Tooltip("Durée de la glissade (s)")]
    public float slowDuration = 3.5f;
    [Tooltip("Vitesse de la police sur le riz (0.3 = 30 %)")]
    [Range(0.05f, 1f)] public float slowFactor = 0.3f;
    [Tooltip("La police à moins de cette distance derrière le bus est touchée directement (m)")]
    public float directHitRange = 25f;

    [Header("Références (cherchées automatiquement si vides)")]
    [Tooltip("Gère la poursuite de la police")]
    public PoliceChaseManager police;
    [Tooltip("Affiche les charges restantes")]
    public CroustyBonusHUD bonusHud;

    // Charges restantes (lu par le HUD)
    public int UsesRemaining { get; private set; }

    // Une flaque de riz posée sur la route
    private class Slick
    {
        public Vector3 center;
        public float timeLeft;
    }

    // Un grain de riz (un petit cube)
    private class Grain
    {
        public Transform transform;
        public Vector3 start, end;
        public float age, flightTime;
        public Slick slick; // la flaque où il est tombé
    }

    private readonly List<Slick> slicks = new List<Slick>();
    private readonly List<Grain> grains = new List<Grain>();
    private Transform grainRoot;
    private int nextGrain;

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
        if (police == null)
            police = FindAnyObjectByType<PoliceChaseManager>();
        if (bonusHud == null)
            bonusHud = FindAnyObjectByType<CroustyBonusHUD>();

        UsesRemaining = startingCharges;
        RefreshHud();

        // On crée les grains une seule fois, puis on les réutilise (2 jets à l'écran en même temps)
        grainRoot = new GameObject("Riz").transform;
        for (int i = 0; i < grainsPerThrow * 2; i++)
        {
            Color color = i % 3 == 0 ? riceColor * 0.85f : riceColor;
            GameObject grain = JojoVisuals.CreateShape(PrimitiveType.Cube, grainRoot, color);
            grain.SetActive(false);
            grains.Add(new Grain { transform = grain.transform });
        }
    }

    private void OnDestroy()
    {
        if (grainRoot != null)
            Destroy(grainRoot.gameObject);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame)
            TryActivate();

        UpdateGrains();
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

        // Nouvelle flaque sur la route
        Slick slick = new Slick();
        slick.center = GroundPoint(rear + back * throwDistance);
        slick.timeLeft = slickDuration;
        slicks.Add(slick);

        // Les grains partent de l'arrière du bus et retombent dans la flaque
        for (int i = 0; i < grainsPerThrow; i++)
        {
            Grain grain = grains[nextGrain];
            nextGrain = (nextGrain + 1) % grains.Count;

            Vector2 spot = Random.insideUnitCircle * slickRadius;
            grain.start = rear + Vector3.up * 1.5f + Random.insideUnitSphere * 0.4f;
            grain.end = GroundPoint(slick.center + new Vector3(spot.x, 0f, spot.y)) + Vector3.up * 0.06f;
            grain.age = 0f;
            grain.flightTime = Random.Range(0.35f, 0.7f);
            grain.slick = slick;

            grain.transform.position = grain.start;
            grain.transform.rotation = Random.rotation;
            grain.transform.localScale = grainSize;
            grain.transform.gameObject.SetActive(true);
        }

        // Police juste derrière : riz en pleine face
        PoliceCarAI car = police != null ? police.ActivePolice : null;
        if (car != null && car.DistanceToTarget <= directHitRange)
        {
            bool carIsBehind = Vector3.Dot(car.transform.position - transform.position, back) > 0f;
            if (carIsBehind)
                car.Slow(slowDuration, slowFactor);
        }

        return true;
    }

    private void RefreshHud()
    {
        if (bonusHud != null)
            bonusHud.SetRiceCharges(UsesRemaining);
    }

    // FixedUpdate : en même temps que la physique de la voiture de police
    private void FixedUpdate()
    {
        PoliceCarAI car = police != null ? police.ActivePolice : null;

        // On parcourt la liste à l'envers pour pouvoir retirer des éléments
        for (int i = slicks.Count - 1; i >= 0; i--)
        {
            Slick slick = slicks[i];
            slick.timeLeft -= Time.fixedDeltaTime;
            if (slick.timeLeft <= 0f)
            {
                slicks.RemoveAt(i);
                continue;
            }

            // La police roule sur le riz ?
            if (car != null)
            {
                Vector3 offset = car.transform.position - slick.center;
                offset.y = 0f;
                if (offset.magnitude <= slickRadius)
                    car.Slow(slowDuration, slowFactor);
            }
        }
    }

    private void UpdateGrains()
    {
        foreach (Grain grain in grains)
        {
            if (!grain.transform.gameObject.activeSelf)
                continue;

            grain.age += Time.deltaTime;

            // En vol : trajectoire en cloche
            if (grain.age < grain.flightTime)
            {
                float t = grain.age / grain.flightTime;
                Vector3 position = Vector3.Lerp(grain.start, grain.end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * grainArcHeight;
                grain.transform.position = position;
                grain.transform.Rotate(720f * Time.deltaTime, 360f * Time.deltaTime, 0f);
                continue;
            }

            // Au sol : le grain disparaît en rétrécissant avec sa flaque
            grain.transform.position = grain.end;
            float timeLeft = grain.slick.timeLeft;
            if (timeLeft <= 0f)
                grain.transform.gameObject.SetActive(false);
            else if (timeLeft < 1f)
                grain.transform.localScale = grainSize * timeLeft;
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
