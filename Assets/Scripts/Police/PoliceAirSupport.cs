using UnityEngine;

[DisallowMultipleComponent]
public sealed class PoliceAirSupport : MonoBehaviour
{
    private PoliceChaseManager police;
    private ProceduralCity city;
    private BusFlightController flight;
    private BoxCollider busCollider;
    private GameObject helicopterPrefab, bombPrefab, dumpsterPrefab, helicopter;
    private Transform rotor;
    private float nextSpawn, nextDrop, suspicion, patrolPhase;
    private int drops;
    private Vector3 patrolCenter, previousRelative;
    private bool hasPrevious, wasAttacking;
    private bool eligibleLastFrame;
    private float leaveAt;
    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private bool Airborne => flight != null && flight.IsAirborne;
    // Air support keeps its slower progression independently of ground-police tuning.
    private float Pressure => DeliveryManager.Instance != null
        ? Mathf.Clamp01((DeliveryManager.Instance.TotalDelivered / 6) / 8f) : 0f;
    private bool Eligible => DeliveryManager.Instance != null && DeliveryManager.Instance.RunsLoaded >= 2;
    private float VisitDuration => Mathf.Lerp(22f, 32f, Pressure);
    private float VisitCooldown => Mathf.Lerp(95f, 60f, Pressure);
    // Keep the introductory drops gentle, then tighten the cadence each new round.
    private float DropInterval => Mathf.Max(1f, 6f * Mathf.Pow(.75f,
        Mathf.Max(0, (DeliveryManager.Instance != null ? DeliveryManager.Instance.RunsLoaded : 2) - 2)));

    private void Start()
    {
        police = GetComponent<PoliceChaseManager>();
        city = FindAnyObjectByType<ProceduralCity>();
        flight = police.BusBody.GetComponent<BusFlightController>();
        busCollider = police.BusBody.GetComponent<BoxCollider>();
        helicopterPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Helicopter");
        bombPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Bomb");
        dumpsterPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Dumpster");
        if (helicopterPrefab == null || bombPrefab == null || dumpsterPrefab == null)
        { Debug.LogError("Police air support: missing imported models.", this); enabled = false; }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || police == null || police.BusBody == null) return;
        if (DeliveryManager.Instance != null && DeliveryManager.Instance.HasFailed)
        { ClearHelicopter(); return; }
        if (!Eligible)
        {
            if (helicopter != null) ClearHelicopter();
            eligibleLastFrame = false;
            return;
        }
        if (!eligibleLastFrame) { nextSpawn = Time.time + 18f; eligibleLastFrame = true; }
        if (helicopter != null && Time.time >= leaveAt)
        { ClearHelicopter(); nextSpawn = Time.time + VisitCooldown; return; }
        if (helicopter == null)
        {
            if (Time.time >= nextSpawn && city != null && city.IsAreaReady(police.BusPosition)) Spawn();
            return;
        }
        float dt = Time.deltaTime;
        if (rotor != null) rotor.Rotate(Vector3.up, 1800f * dt, Space.Self);
        bool visible = !Airborne && CanSeeBus();
        if (visible && police.CanAirSpotSpeeding)
        {
            suspicion += dt;
            if (suspicion >= .75f) police.ReportAirSight();
        }
        else suspicion = 0f;
        if (visible && police.ChaseStarted) police.ReportAirSight();
        bool attacking = visible && police.ChaseStarted;
        if (attacking && !wasAttacking) nextDrop = Time.time + 5f;
        if (!attacking && wasAttacking) patrolCenter = helicopter.transform.position;
        wasAttacking = attacking;

        Vector3 heading = police.BusVelocity.sqrMagnitude > 4f ? police.BusVelocity.normalized :
            Vector3.ProjectOnPlane(police.BusBody.transform.forward, Vector3.up).normalized;
        Vector3 target;
        if (attacking)
        {
            target = police.BusPosition + heading * 55f;
            target.y = city.Origin.y + 22f;
        }
        else
        {
            // Orbit the last ground location, never chase the bus's altitude or airborne position.
            patrolPhase += dt * .22f;
            target = patrolCenter + new Vector3(Mathf.Sin(patrolPhase), 0f, Mathf.Cos(patrolPhase)) * 32f;
            target.y = city.Origin.y + 22f;
        }
        Vector3 offset = target - helicopter.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (direction.sqrMagnitude > .1f)
            helicopter.transform.rotation = Quaternion.RotateTowards(helicopter.transform.rotation,
                Quaternion.LookRotation(attacking ? heading : direction), 90f * dt);
        // Match bus speed in formation; bounded catch-up when entering or turning.
        float speed = attacking ? police.BusVelocity.magnitude + Mathf.Min(12f, offset.magnitude * 2f) : 9f;
        helicopter.transform.position = Vector3.MoveTowards(helicopter.transform.position, target, speed * dt);
        if (!Airborne && !attacking && Vector3.Distance(helicopter.transform.position, police.BusPosition) > 320f)
        { ClearHelicopter(); nextSpawn = Time.time + VisitCooldown; return; }
        if (!attacking || Time.time < nextDrop || offset.magnitude > 15f) return;
        Vector3 drop = helicopter.transform.position - Vector3.up * 2f;
        if (!city.IsAreaReady(drop)) return;
        nextDrop = Time.time + DropInterval;
        drop += helicopter.transform.right * Random.Range(-.6f, .6f);
        bool dumpster = ++drops % (Pressure < .5f ? 4 : 3) == 0;
        var hazard = new GameObject(dumpster ? "Poubelle incendiaire police" : "Bombe police");
        hazard.transform.position = drop;
        hazard.AddComponent<PoliceAirProjectile>().Initialize(dumpster ? dumpsterPrefab : bombPrefab, dumpster, police.BusBody);
    }

    private bool CanSeeBus()
    {
        Vector3 eye = helicopter.transform.position + Vector3.up * 2f;
        Vector3 offset = police.BusBody.worldCenterOfMass - eye;
        if (offset.sqrMagnitude > 130f * 130f) return false;
        int count = Physics.RaycastNonAlloc(eye, offset.normalized, sightHits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        if (count == sightHits.Length) return false;
        for (int i = 0; i < count; i++)
            if (sightHits[i].rigidbody == null) return false;
        return true;
    }

    private void FixedUpdate()
    {
        if (helicopter == null || busCollider == null || Time.timeScale <= 0f) { hasPrevious = false; return; }
        Vector3 relative = busCollider.bounds.center - (helicopter.transform.position + Vector3.up * 2f);
        if (flight != null && flight.CurrentMode == BusFlightController.FlightMode.Flying)
        {
            // Swept relative bounds also catch fast passes between physics steps. No solid
            // collider: ramming must not trigger the flight controller's landing behaviour.
            Vector3 extents = busCollider.bounds.extents + new Vector3(3.5f, 2.5f, 3.5f);
            Bounds bounds = new Bounds(Vector3.zero, extents * 2f);
            Vector3 movement = relative - previousRelative;
            bool hit = bounds.Contains(relative) || hasPrevious &&
                (bounds.Contains(previousRelative) || movement.sqrMagnitude > .0001f &&
                 bounds.IntersectRay(new Ray(previousRelative, movement.normalized), out float distance) && distance <= movement.magnitude);
            if (hit)
            {
                var crash = police.BusBody.GetComponent<BusCarCrash>();
                if (crash != null && crash.explosionPrefab != null)
                    Destroy(Instantiate(crash.explosionPrefab, helicopter.transform.position, Quaternion.identity), 5f);
                CroustySoundDesignManager.Play(CroustySound.Explosion, helicopter.transform.position);
                ClearHelicopter();
                nextSpawn = Time.time + VisitCooldown;
                return;
            }
        }
        previousRelative = relative;
        hasPrevious = flight != null && flight.CurrentMode == BusFlightController.FlightMode.Flying;
    }

    private void Spawn()
    {
        Vector3 heading = Vector3.ProjectOnPlane(police.BusBody.transform.forward, Vector3.up).normalized;
        helicopter = new GameObject("Helicoptere de police - patrouille");
        PoliceModelVisual.Create(helicopterPrefab, helicopter.transform, new Vector3(14f, 0f, 0f), 90f);
        foreach (Transform part in helicopter.GetComponentsInChildren<Transform>())
            if (part.name == "Rotor_Main") { rotor = part; break; }
        patrolCenter = police.BusPosition + heading * 75f;
        patrolCenter.y = city.Origin.y + 22f;
        helicopter.transform.SetPositionAndRotation(patrolCenter, Quaternion.LookRotation(heading));
        patrolPhase = 0f; drops = 0; suspicion = 0f; hasPrevious = false; wasAttacking = false;
        leaveAt = Time.time + VisitDuration;
    }

    private void ClearHelicopter()
    {
        if (helicopter != null) Destroy(helicopter);
        helicopter = null; rotor = null; hasPrevious = false; wasAttacking = false;
    }
    private void OnDisable() => ClearHelicopter();
}
