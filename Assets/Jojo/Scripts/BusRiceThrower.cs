using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Compétence RIZ : le bus jette une poignée de riz derrière lui.
// Le riz reste sur la route un moment : la police qui roule dessus glisse et ralentit.
// Si la police colle le bus au moment du jet, elle se le prend en pleine face et ralentit tout de suite.
// Ajouté automatiquement sur le bus par le DeliveryManager.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusRiceThrower : MonoBehaviour
{
    public Key activationKey = Key.E;
    [Range(0, 3)] public int startingCharges = 3;
    public int UsesRemaining { get; private set; }

    [Header("Riz sur la route")]
    [Tooltip("Rayon de la flaque de riz (m)")]
    public float slickRadius = 6f;
    [Tooltip("Durée pendant laquelle le riz reste sur la route")]
    public float slickDuration = 10f;
    [Tooltip("Distance derrière le bus où le riz retombe")]
    public float throwDistance = 6f;
    [Min(4)] public int grainsPerThrow = 60;

    [Header("Effet sur la police")]
    [Tooltip("Secondes de glissade")]
    public float slowDuration = 3.5f;
    [Tooltip("Vitesse de la police sur le riz (0.3 = 30 %)")]
    [Range(0.05f, 1f)] public float slowFactor = 0.3f;
    [Tooltip("La police à moins de cette distance derrière le bus est touchée directement")]
    public float directHitRange = 25f;

    public Color riceColor = new Color(0.98f, 0.97f, 0.9f);

    private class Grain { public Transform t; public Vector3 start, end; public float age, flight; public Slick slick; }
    private class Slick { public Vector3 center; public float remaining; }

    private readonly List<Grain> grains = new List<Grain>();
    private readonly List<Slick> slicks = new List<Slick>();
    private Transform grainRoot;
    private int nextGrain;

    private Rigidbody rb;
    private BusController drive;
    private BusFlightController flight;
    private BoxCollider body;
    private PoliceChaseManager police;
    private CroustyBonusHUD bonusHud;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        drive = GetComponent<BusController>();
        flight = GetComponent<BusFlightController>();
        body = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        UsesRemaining = startingCharges;
        police = FindAnyObjectByType<PoliceChaseManager>();
        bonusHud = FindAnyObjectByType<CroustyBonusHUD>();
        if (bonusHud != null) bonusHud.SetRiceCharges(UsesRemaining);

        grainRoot = new GameObject("Riz").transform;
        for (int i = 0; i < grainsPerThrow * 2; i++)
        {
            GameObject grain = JojoVisuals.CreateShape(PrimitiveType.Cube, grainRoot,
                i % 3 == 0 ? riceColor * 0.85f : riceColor);
            grain.SetActive(false);
            grains.Add(new Grain { t = grain.transform });
        }
    }

    private void OnDestroy()
    {
        if (grainRoot != null) Destroy(grainRoot.gameObject);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Keyboard.current != null && Keyboard.current[activationKey].wasPressedThisFrame)
            TryActivate();

        UpdateGrains();
    }

    public bool TryActivate()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || UsesRemaining <= 0 ||
            drive == null || !drive.enabled || (flight != null && flight.IsAirborne) || rb.isKinematic)
            return false;

        UsesRemaining--;
        if (bonusHud != null) bonusHud.SetRiceCharges(UsesRemaining);
        CroustySoundDesignManager.Play(CroustySound.Paper, transform.position);

        Vector3 back = -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float halfLength = body != null ? body.size.z * Mathf.Abs(transform.lossyScale.z) * 0.5f : 3f;
        Vector3 rear = (body != null ? body.bounds.center : rb.position) + back * halfLength;

        var slick = new Slick { remaining = slickDuration };
        slick.center = GroundPoint(rear + back * throwDistance);
        slicks.Add(slick);

        // Les grains partent de l'arrière du bus et retombent en tas sur la route
        for (int i = 0; i < grainsPerThrow; i++)
        {
            Grain grain = grains[nextGrain];
            nextGrain = (nextGrain + 1) % grains.Count;
            Vector2 spot = Random.insideUnitCircle * slickRadius;
            grain.start = rear + Vector3.up * 1.5f + Random.insideUnitSphere * 0.4f;
            grain.end = GroundPoint(slick.center + new Vector3(spot.x, 0f, spot.y)) + Vector3.up * 0.06f;
            grain.age = 0f;
            grain.flight = Random.Range(0.35f, 0.7f);
            grain.slick = slick;
            grain.t.position = grain.start;
            grain.t.rotation = Random.rotation;
            grain.t.localScale = new Vector3(0.12f, 0.12f, 0.28f);
            grain.t.gameObject.SetActive(true);
        }

        // Police juste derrière : riz en pleine face
        PoliceCarAI car = police != null ? police.ActivePolice : null;
        if (car != null && car.DistanceToTarget <= directHitRange &&
            Vector3.Dot(car.transform.position - transform.position, back) > 0f)
            car.Slow(slowDuration, slowFactor);

        return true;
    }

    private void FixedUpdate()
    {
        PoliceCarAI car = police != null ? police.ActivePolice : null;
        for (int i = slicks.Count - 1; i >= 0; i--)
        {
            Slick slick = slicks[i];
            slick.remaining -= Time.fixedDeltaTime;
            if (slick.remaining <= 0f)
            {
                slicks.RemoveAt(i);
                continue;
            }

            // La police roule sur le riz
            if (car != null && Vector3.ProjectOnPlane(car.transform.position - slick.center, Vector3.up).magnitude <= slickRadius)
                car.Slow(slowDuration, slowFactor);
        }
    }

    private void UpdateGrains()
    {
        foreach (Grain grain in grains)
        {
            if (!grain.t.gameObject.activeSelf) continue;
            grain.age += Time.deltaTime;

            if (grain.age < grain.flight)
            {
                // Vol en cloche
                float t = grain.age / grain.flight;
                Vector3 position = Vector3.Lerp(grain.start, grain.end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 2f;
                grain.t.position = position;
                grain.t.Rotate(720f * Time.deltaTime, 360f * Time.deltaTime, 0f);
                continue;
            }

            grain.t.position = grain.end;
            // Le riz disparaît en rétrécissant avec sa flaque
            float remaining = grain.slick.remaining;
            if (remaining <= 0f)
                grain.t.gameObject.SetActive(false);
            else if (remaining < 1f)
                grain.t.localScale = new Vector3(0.12f, 0.12f, 0.28f) * remaining;
        }
    }

    // Point du sol sous une position (ignore le bus)
    private Vector3 GroundPoint(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float highest = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
            if (!hit.collider.transform.IsChildOf(transform) && hit.rigidbody == null &&
                hit.normal.y > 0.65f && hit.point.y <= position.y + 2f)
                highest = Mathf.Max(highest, hit.point.y);
        if (!float.IsNegativeInfinity(highest)) position.y = highest;
        return position;
    }
}
