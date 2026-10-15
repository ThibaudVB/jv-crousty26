using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Police of the procedural city. Patrol cars drive the streets with the traffic; one that
// sees the bus speeding chases it and calls backup. Breaking line of sight long enough
// (buildings, jumps, the PET cloud that blinds and hides) shakes them off; staying slow
// next to an officer fills the arrest meter.
public class PoliceChaseManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody busRigidbody;
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private PoliceCatchZone catchZone;
    [SerializeField] private BusFlightController flightController;

    [Header("Patrouilles")]
    [SerializeField, Range(0, 10)] private int patrolCount = 3;
    [SerializeField, Min(0f)] private float startupGraceSeconds = 3f;
    [SerializeField, Min(50f)] private float patrolRadius = 115f;

    [Header("Detection")]
    [SerializeField, Min(10f)] private float speedLimitKmh = 50f;
    [SerializeField, Min(5f)] private float visionRange = 75f;
    [SerializeField, Range(10f, 180f)] private float visionHalfAngle = 95f;
    [Tooltip("En dessous de cette distance, la patrouille remarque le bus dans toutes les directions.")]
    [SerializeField, Min(0f)] private float closeAwareness = 22f;
    [SerializeField, Min(0f)] private float spottingSeconds = .35f;

    [Header("Poursuite")]
    [SerializeField, Min(10f)] private float chaseSpeedKmh = 138f;
    [SerializeField, Min(1f)] private float chaseAcceleration = 10f;
    [SerializeField, Range(1, 5)] private int maxChasers = 5;
    [SerializeField, Min(0f)] private float backupRadius = 260f;
    [SerializeField, Min(10f)] private float pursuitVisionRange = 90f;
    [Tooltip("Apres la perte de vue, la radio donne encore la position du bus pendant ce delai.")]
    [SerializeField, Min(0f)] private float radioTrackingSeconds = 2.5f;
    [SerializeField, Min(1f)] private float loseSightSeconds = 8f;
    [SerializeField, Min(30f)] private float escapeDistance = 170f;
    [SerializeField, Min(0f)] private float escapeCooldownSeconds = 8f;

    [Header("Escalade")]
    [SerializeField, Range(1, 5)] private int maximumHeat = 5;
    [SerializeField, Min(5f)] private float escalationSeconds = 30f;
    [SerializeField, Range(3, 12)] private int maximumReinforcements = 12;
    [Header("Progression par livraisons reussies")]
    [SerializeField, Min(1)] private int deliveriesPerDifficultyLevel = 1;
    [SerializeField, Range(1, 8)] private int maximumDifficultyLevel = 4;
    [SerializeField, Range(0, 10)] private int latePatrolCount = 10;
    [SerializeField, Min(0f)] private float lateChaseSpeedBonusKmh = 42f;
    [Header("Barrages solides")]
    [SerializeField, Range(1, 8)] private int maximumRoadblocks = 6;
    [SerializeField, Min(5f)] private float roadblockLifetime = 90f;
    private readonly List<PoliceRoadblock> roadblocks = new List<PoliceRoadblock>();
    private readonly Collider[] roadblockOverlaps = new Collider[64];
    private float nextRoadblock;
    private int lastRoadblockOrders;
    private Material barricadeWhite, barricadeRed;
    private GameObject barrierPrefab;
    private int CompletedOrders => DeliveryManager.Instance != null ? DeliveryManager.Instance.TotalDelivered : 0;
    private int RoadblockLimit => Mathf.Min(maximumRoadblocks, CompletedOrders);
    private float RoadblockInterval => Mathf.Lerp(24f, 5f, Mathf.Clamp01((CompletedOrders - 1) / 3f));
    public int ActiveRoadblockCount => roadblocks.Count;
    // Session progress survives pickup/round changes and escapes, but resets with the game.
    public int DifficultyLevel => DeliveryManager.Instance == null ? 0 :
        Mathf.Clamp(DeliveryManager.Instance.TotalDelivered / Mathf.Max(1, deliveriesPerDifficultyLevel),
            0, Mathf.Max(1, maximumDifficultyLevel));
    private float Difficulty => DifficultyLevel / (float)Mathf.Max(1, maximumDifficultyLevel);
    public float ProgressionPressure => Difficulty;
    private int CurrentPatrolCount => patrolCount == 0 ? 0 :
        Mathf.RoundToInt(Mathf.Lerp(patrolCount, Mathf.Max(patrolCount, latePatrolCount), Difficulty));
    private float EscalationInterval => escalationSeconds * Mathf.Lerp(1f, .3f, Difficulty);
    private float SpottingDuration => spottingSeconds * Mathf.Lerp(1f, .3f, Difficulty);
    private float RadioTrackingDuration => radioTrackingSeconds + Difficulty * 3.5f;
    private int StartingHeat => Mathf.Min(maximumHeat, 1 + Mathf.FloorToInt(Difficulty * 4f));
    private float ArrestDuration => bustSeconds * Mathf.Lerp(1f, .4f, Difficulty);
    private float BaseChaseSpeed => (chaseSpeedKmh + lateChaseSpeedBonusKmh * Difficulty) / 3.6f;
    private int heat;
    private float nextEscalation, nextBackup;
    public int HeatLevel => heat;
    private int PursuitLimit => Mathf.Min(maximumReinforcements,
        Mathf.Min(maxChasers, 2 + Mathf.FloorToInt(Difficulty * 6f)) + Mathf.Max(0, heat - 1) * 2,
        Mathf.RoundToInt(Mathf.Lerp(4f, maximumReinforcements, Difficulty)));
    private float SearchDuration => loseSightSeconds + Difficulty * 9f + Mathf.Max(0, heat - 1) * 1.5f;

    [Header("Arrestation")]
    [SerializeField, Min(1f)] private float bustDistance = 9f;
    [SerializeField, Min(0f)] private float bustSpeedKmh = 22f;
    [SerializeField, Min(.2f)] private float bustSeconds = 2.5f;

    private readonly List<PoliceCarAI> cars = new List<PoliceCarAI>();
    private readonly List<(PoliceCarAI car, float at)> backup = new List<(PoliceCarAI, float)>();
    private readonly RaycastHit[] sightHits = new RaycastHit[16];
    private ProceduralCity city;
    private BusPetController pet;
    private Transform root;
    private Material beaconDark, beaconRed, beaconBlue;
    private bool chasing;
    private bool spawningEnabled = true;
    private float suspicion, lastSeen, calmUntil, nextSpawn, nextSight, arrest;
    private Vector3 lastKnown;
    private Vector3 lastKnownVelocity;
    private float nextRearRam;
    private Image arrestFill;
    private GameObject arrestBar;

    public IReadOnlyList<PoliceCarAI> Cars => cars;
    public bool ChaseStarted => chasing;
    public void SetSpawningEnabled(bool enabled) => spawningEnabled = enabled;
    public PoliceCarAI CreateTutorialPoliceCar(Vector3 position, Quaternion rotation)
    {
        if (city == null || city.WorldRoot == null || policePrefab == null)
        {
            Debug.LogError("Police : impossible de créer la voiture du tutoriel sans ville chargée.", this);
            return null;
        }

        PoliceCarAI car = CreateCar();
        cars.Add(car);
        car.MarkAsTutorialImpactCar();
        car.StartChase();
        car.enabled = false;
        car.transform.SetPositionAndRotation(position, rotation);
        Rigidbody body = car.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.position = position;
        body.rotation = rotation;
        return car;
    }

    public void RemoveTutorialPoliceCar(PoliceCarAI car)
    {
        if (car == null) return;
        cars.Remove(car);
        Destroy(car.gameObject);
    }

    public bool BusInSight { get; private set; }
    public float SecondsOutOfSight => chasing ? Time.time - lastSeen : 0f;
    public float ArrestProgress => arrest;
    public float ChaseSpeed => Mathf.Min(BaseChaseSpeed + Mathf.Lerp(20f, 24f, Difficulty) / 3.6f,
        Mathf.Max(BaseChaseSpeed + heat * 1.2f,
            chasing && BusInSight ? BusVelocity.magnitude + 5f + Difficulty * 6f + heat * .5f : 0f));
    public float ChaseAcceleration => chaseAcceleration + Difficulty * 10f + heat * .75f;
    public float RamClosingSpeed => 3.5f + Difficulty * 3f + Mathf.Max(0, heat - 1) * .5f;
    public float RearRamCooldown => Mathf.Lerp(3.5f, 1.5f, Difficulty);
    public bool CanAttemptRearRam(PoliceCarAI candidate)
    {
        if (Time.time < nextRearRam || BusHidden) return false;
        foreach (var car in cars)
            if (car != null && car != candidate && car.IsRammingBus) return false;
        return true;
    }
    public void RearRamContact() => nextRearRam = Time.time + RearRamCooldown;
    public Rigidbody BusBody => busRigidbody;
    public Vector3 BusPosition => busRigidbody.position;
    public Vector3 BusVelocity { get { var v = busRigidbody.linearVelocity; v.y = 0; return v; } }
    public float BusHalfLength { get; private set; } = 3f;
    // Where pursuers head when they cannot see the bus: radio fix first, then the last sighting.
    public Vector3 TrackedPosition => lastKnown + lastKnownVelocity * Mathf.Clamp(Time.time - lastSeen, 0f, RadioTrackingDuration);

    // Nearest pursuer close enough for the camera to frame it.
    public PoliceCarAI ActivePolice
    {
        get
        {
            PoliceCarAI best = null; float distance = 45f;
            foreach (var car in cars)
            {
                if (car == null || !car.InPursuit) continue;
                float d = Vector3.Distance(car.transform.position, BusPosition);
                if (d < distance) { distance = d; best = car; }
            }
            return best;
        }
    }
    public float PoliceDistance { get { var p = ActivePolice; return p != null ? Vector3.Distance(p.transform.position, BusPosition) : 45f; } }
    public float BlindRemaining { get { float b = 0; foreach (var car in cars) if (car != null) b = Mathf.Max(b, car.BlindRemaining); return b; } }
    public bool IsBlinded => BlindRemaining > 0f;
    private bool BusHidden => flightController != null && flightController.IsAirborne || pet != null && pet.IsBoosting;

    private void Awake()
    {
        city = FindAnyObjectByType<ProceduralCity>();
        if (busRigidbody == null || policePrefab == null || city == null)
        {
            Debug.LogError("Police : bus, prefab de voiture et ville procedurale requis.", this);
            enabled = false;
            return;
        }
        if (catchZone == null) catchZone = busRigidbody.GetComponentInChildren<PoliceCatchZone>();
        if (flightController == null) flightController = busRigidbody.GetComponent<BusFlightController>();
        pet = busRigidbody.GetComponent<BusPetController>();
        var busBox = busRigidbody.GetComponent<BoxCollider>();
        if (busBox != null) BusHalfLength = busBox.size.z * Mathf.Abs(busBox.transform.lossyScale.z) * .5f;
        var signal = Resources.Load<Material>("Crousty/ProceduralSignal");
        beaconDark = Glow(signal, new Color(.12f, .12f, .16f), 0f);
        beaconRed = Glow(signal, new Color(1f, .1f, .08f), 4f);
        beaconBlue = Glow(signal, new Color(.15f, .35f, 1f), 4f);
        barricadeWhite = Glow(signal, new Color(.85f, .88f, .9f), 0f);
        barricadeRed = Glow(signal, new Color(.9f, .08f, .06f), .3f);
        calmUntil = Time.time + startupGraceSeconds;
        BuildHud();
        barrierPrefab = Resources.Load<GameObject>("Crousty/PoliceAir/Barrier");
        if (GetComponent<PoliceAirSupport>() == null) gameObject.AddComponent<PoliceAirSupport>();
    }

    private static Material Glow(Material template, Color color, float intensity)
    {
        var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetColor("_EmissionColor", color * intensity);
        return material;
    }

    private void FixedUpdate()
    {
        if (city == null || city.WorldRoot == null || Time.timeScale <= 0f || catchZone != null && catchZone.IsCaught) return;
        float dt = Time.fixedDeltaTime;
        cars.RemoveAll(car => car == null || car.IsDestroyed);
        // A delivery completed during an existing chase raises pressure immediately.
        // Progress alone never starts a chase against a legally driven bus.
        if (chasing) heat = Mathf.Max(heat, StartingHeat);
        if (spawningEnabled)
        {
            MaintainRoadblocks();
            MaintainPatrols();
        }

        bool look = Time.time >= nextSight;
        if (look) nextSight = Time.time + .1f;
        float busKmh = BusVelocity.magnitude * 3.6f;
        bool hidden = BusHidden;

        if (!chasing)
        {
            bool spotted = false;
            if (Time.time >= calmUntil && !hidden)
                foreach (var car in cars)
                    if ((car.State == PoliceCarAI.Mode.Patrol || car.IsRoadblock) && !car.IsBlinded &&
                        busKmh > speedLimitKmh &&
                        CanSee(car, visionRange, visionHalfAngle))
                    { spotted = true; if ((suspicion += dt) >= SpottingDuration) BeginChase(car); break; }
            // Driving legally never carries partial suspicion into a later encounter.
            if (busKmh <= speedLimitKmh || hidden) suspicion = 0f;
            else if (!spotted) suspicion = Mathf.Max(0f, suspicion - dt);
            SetArrest(Mathf.MoveTowards(arrest, 0f, dt));
            return;
        }

        // Backup arrives a few seconds after the call.
        if (Time.time >= nextEscalation && BusInSight)
        {
            RaiseHeat();
            nextEscalation = Time.time + EscalationInterval;
        }
        RequestBackup();
        for (int i = backup.Count - 1; i >= 0; i--)
        {
            if (backup[i].car == null || backup[i].car.IsDestroyed) { backup.RemoveAt(i); continue; }
            if (Time.time < backup[i].at) continue;
            backup[i].car.StartChase();
            backup.RemoveAt(i);
        }

        BusInSight = !hidden && Time.time < airSeenUntil;
        float nearest = float.PositiveInfinity;
        int pinning = 0;
        foreach (var car in cars)
        {
            if (!car.InPursuit && !car.IsRoadblock) continue;
            if (hidden) car.SeesBus = false;
            if (look) car.SeesBus = !hidden && !car.IsBlinded && CanSee(car, pursuitVisionRange, 180f);
            if (car.SeesBus) BusInSight = true;
            Vector3 offset = car.transform.position - BusPosition; offset.y = 0;
            nearest = Mathf.Min(nearest, offset.magnitude);
            if (car.SeesBus && !hidden && !car.IsBlinded && offset.magnitude < bustDistance) pinning++;
        }
        if (BusInSight) { lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity; }

        float unseen = Time.time - lastSeen;
        if (unseen > SearchDuration || unseen > 4f + heat && nearest > escapeDistance + heat * 35f) { Escape(); return; }

        // Arrest: slow (or stuck) next to an officer. Two officers are quicker.
        if (pinning > 0 && busKmh < bustSpeedKmh)
            SetArrest(arrest + dt / ArrestDuration * (1f + .5f * Mathf.Min(2, pinning - 1)));
        else
            SetArrest(Mathf.Max(0f, arrest - dt * .5f));
    }

    private void Update() => UpdateHud();

    private bool CanSee(PoliceCarAI car, float range, float halfAngle)
    {
        Vector3 eye = car.transform.position + Vector3.up * 1.4f;
        Vector3 bus = BusPosition + Vector3.up * 1.4f;
        Vector3 offset = bus - eye;
        Vector3 flat = new Vector3(offset.x, 0, offset.z);
        float distance = flat.magnitude;
        if (distance > range) return false;
        if (distance > closeAwareness && Vector3.Angle(car.transform.forward, flat) > halfAngle) return false;
        // Only static scenery (buildings, walls, ramps) hides the bus.
        int count = Physics.RaycastNonAlloc(eye, offset.normalized, sightHits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (sightHits[i].rigidbody == null && !sightHits[i].collider.transform.IsChildOf(car.transform)) return false;
        return true;
    }

    private float airSeenUntil;
    public bool CanAirSpotSpeeding => !BusHidden && Time.time >= calmUntil && BusVelocity.magnitude * 3.6f > speedLimitKmh;

    public void ReportAirSight()
    {
        if (BusHidden || catchZone != null && catchZone.IsCaught) return;
        if (!chasing)
        {
            if (!CanAirSpotSpeeding) return;
            BeginChase(null);
        }
        airSeenUntil = Time.time + .15f;
        lastSeen = Time.time;
        lastKnown = BusPosition;
        lastKnownVelocity = BusVelocity;
    }

    private void BeginChase(PoliceCarAI first)
    {
        chasing = true; suspicion = 0f;
        heat = Mathf.Max(StartingHeat, heat);
        nextEscalation = Time.time + EscalationInterval;
        nextBackup = 0f;
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
        if (first != null) { first.StartChase(); first.SeesBus = true; }
        backup.Clear();
        var others = new List<PoliceCarAI>();
        foreach (var car in cars)
            if (car != first && car.State == PoliceCarAI.Mode.Patrol &&
                Vector3.Distance(car.transform.position, BusPosition) < backupRadius) others.Add(car);
        others.Sort((a, b) => Vector3.Distance(a.transform.position, BusPosition).CompareTo(Vector3.Distance(b.transform.position, BusPosition)));
        for (int i = 0; i < others.Count && i < PursuitLimit - 1; i++)
            backup.Add((others[i], Time.time + Mathf.Lerp(2f, .5f, Difficulty) + Mathf.Lerp(1.5f, .2f, Difficulty) * i));
    }

    public void CarDestroyed(PoliceCarAI car)
    {
        cars.Remove(car);
        backup.RemoveAll(entry => entry.car == car);
        if (catchZone != null && catchZone.IsCaught) return;
        if (!chasing)
        {
            chasing = true;
            heat = StartingHeat;
        }
        RaiseHeat();
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
        nextEscalation = Time.time + EscalationInterval;
        nextBackup = nextSpawn = 0f;
    }

    private void RaiseHeat()
    {
        if (heat >= maximumHeat) return;
        heat++;
    }

    private void RequestBackup()
    {
        if (!BusInSight && SecondsOutOfSight > RadioTrackingDuration) return;
        if (Time.time < nextBackup) return;
        nextBackup = Time.time + 1f;
        int assigned = backup.Count;
        foreach (var car in cars) if (car.InPursuit) assigned++;
        foreach (var car in cars)
        {
            if (assigned >= PursuitLimit) break;
            if (car.IsDestroyed || car.IsRoadblock || car.InPursuit || backup.Exists(entry => entry.car == car)) continue;
            if (Vector3.Distance(car.transform.position, BusPosition) > backupRadius) continue;
            backup.Add((car, Time.time + Mathf.Max(.5f, 2.5f - heat * .35f - Difficulty * .75f)));
            assigned++;
        }
    }

    private void Escape()
    {
        chasing = false;
        heat = 0;
        BusInSight = false;
        backup.Clear();
        foreach (var car in cars) { car.SeesBus = false; car.ReturnToPatrol(); }
        calmUntil = Time.time + escapeCooldownSeconds * Mathf.Lerp(1f, .25f, Difficulty);
        SetArrest(0f);
    }

    // The PET cloud blinds officers behind or right next to the bus; the bus stays hidden while boosting.
    public bool TryBlindWithPet(Rigidbody source, float duration, float range)
    {
        if (!isActiveAndEnabled || source != busRigidbody || Time.timeScale <= 0f) return false;
        bool any = false;
        Vector3 forward = source.rotation * Vector3.forward;
        foreach (var car in cars)
        {
            if (car == null) continue;
            Vector3 offset = car.transform.position - source.position; offset.y = 0;
            if (offset.magnitude > range || Vector3.Dot(offset, forward) > 0f && offset.magnitude > 12f) continue;
            car.Blind(Mathf.Clamp(duration, 0f, 6f));
            car.SeesBus = false;
            any = true;
        }
        return any;
    }

    // Keeps a few patrols around the bus, spawned and moved out of its sight.
    private void MaintainPatrols()
    {
        if (Time.time < nextSpawn) return;
        nextSpawn = Time.time + .5f;
        EnsureNearbyPatrol();
        bool reinforcing = chasing && (BusInSight || SecondsOutOfSight < RadioTrackingDuration);
        int mobileCars = 0;
        foreach (var car in cars) if (!car.IsRoadblock) mobileCars++;
        if (mobileCars < (reinforcing ? Mathf.Max(CurrentPatrolCount, PursuitLimit + 2) : CurrentPatrolCount))
        {
            if (city.TryFindStreetSpawn(null, 55f, patrolRadius, true, 40, out var from, out var to, out var at))
            { var car = CreateCar(); car.StartPatrol(from, to, at); cars.Add(car); }
            return;
        }
        foreach (var car in cars)
        {
            if (car.IsRoadblock) continue;
            Vector3 offset = car.transform.position - BusPosition; offset.y = 0;
            if (VisibleToPlayer(car.transform.position)) continue;
            if (car.InPursuit && !reinforcing) continue;
            if (!chasing && mobileCars > CurrentPatrolCount && offset.magnitude > 90f)
            {
                cars.Remove(car);
                Destroy(car.gameObject);
                return;
            }
            if (offset.magnitude > patrolRadius + 15f || !city.IsAreaReady(car.transform.position)) { Recycle(car); return; }
        }
    }

    // Visibility restrictions must never leave the loaded city without an operational patrol.
    private void EnsureNearbyPatrol()
    {
        if (CurrentPatrolCount <= 0) return;
        PoliceCarAI recoverable = null;
        foreach (var car in cars)
        {
            if (car.IsRoadblock) continue;
            Vector3 offset = car.transform.position - BusPosition;
            offset.y = 0f;
            if (car.isActiveAndEnabled && city.IsAreaReady(car.transform.position) &&
                offset.sqrMagnitude <= Mathf.Pow(Mathf.Max(160f, patrolRadius) + 15f, 2f)) return;
            if (recoverable == null) recoverable = car;
        }
        var traffic = recoverable != null ? recoverable.GetComponent<CityTrafficCar>() : null;
        if (!city.TryFindStreetSpawn(traffic, 55f, Mathf.Max(160f, patrolRadius), false, 120,
            out var from, out var to, out var at)) return;
        if (recoverable == null)
        {
            recoverable = CreateCar();
            cars.Add(recoverable);
        }
        recoverable.gameObject.SetActive(true);
        recoverable.enabled = true;
        backup.RemoveAll(entry => entry.car == recoverable);
        recoverable.StartPatrol(from, to, at);
    }

    public void Recycle(PoliceCarAI car)
    {
        if (car.IsRoadblock) return;
        if (VisibleToPlayer(car.transform.position)) return;
        if (city.TryFindStreetSpawn(car.GetComponent<CityTrafficCar>(), 55f, patrolRadius, true, 40, out var from, out var to, out var at))
        {
            backup.RemoveAll(entry => entry.car == car);
            car.StartPatrol(from, to, at);
        }
    }

    private bool VisibleToPlayer(Vector3 position)
    {
        if (Vector3.Distance(position, BusPosition) < 45f) return true;
        var camera = Camera.main;
        if (camera == null) return false;
        var point = camera.WorldToViewportPoint(position + Vector3.up);
        return point.z > 0f && point.z < RenderSettings.fogEndDistance + 20f &&
            point.x > -.1f && point.x < 1.1f && point.y > -.1f && point.y < 1.1f;
    }

    private PoliceCarAI CreateCar()
    {
        if (root == null) { root = new GameObject("Police").transform; root.SetParent(city.WorldRoot, false); }
        var car = new GameObject("Voiture de police");
        car.SetActive(false);
        car.transform.SetParent(root, false);
        var model = Instantiate(policePrefab, car.transform, false);
        model.name = "Carrosserie";
        // The prefab is used as a visual only: its own physics and scripts are removed.
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
        foreach (var rigidbody in model.GetComponentsInChildren<Rigidbody>(true)) { rigidbody.isKinematic = true; rigidbody.detectCollisions = false; Destroy(rigidbody); }
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
        Bounds bounds = new Bounds(); bool found = false;
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = car.transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!found) { bounds = new Bounds(corner, Vector3.zero); found = true; } else bounds.Encapsulate(corner);
            }
        }
        if (!found) bounds = new Bounds(new Vector3(0, .8f, 0), new Vector3(2f, 1.6f, 4.5f));
        float modelScale = city.vehicleLength / Mathf.Max(.01f, bounds.size.z);
        model.transform.localScale *= modelScale;
        model.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * modelScale;
        Vector3 size = bounds.size * modelScale;
        var box = car.AddComponent<BoxCollider>();
        box.size = size; box.center = new Vector3(0, size.y * .5f, 0);
        car.AddComponent<Rigidbody>();
        car.AddComponent<CityTrafficCar>();
        // Light bar: two beacons that flash during a pursuit.
        var beacons = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = i == 0 ? "Gyrophare rouge" : "Gyrophare bleu";
            Destroy(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(car.transform, false);
            lamp.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * size.x * .18f, size.y + .12f, -size.z * .05f);
            lamp.transform.localScale = new Vector3(size.x * .3f, .22f, .35f);
            beacons[i] = lamp.GetComponent<Renderer>();
            beacons[i].sharedMaterial = beaconDark;
        }
        var ai = car.AddComponent<PoliceCarAI>();
        ai.Init(this, city, size.z * .5f, beacons, beaconDark, beaconRed, beaconBlue);
        car.SetActive(true);
        return ai;
    }

    private void SetArrest(float value)
    {
        arrest = Mathf.Clamp01(value);
        if (catchZone != null) catchZone.SetProgress(arrest);
    }

    private void BuildHud()
    {
        var canvasObject = new GameObject("Police - HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        arrestBar = new GameObject("Arrestation", typeof(RectTransform), typeof(Image));
        arrestBar.transform.SetParent(canvasObject.transform, false);
        var back = arrestBar.GetComponent<Image>(); back.color = new Color(0, 0, 0, .55f); back.raycastTarget = false;
        var barRect = back.rectTransform;
        barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(.5f, 1f);
        barRect.anchoredPosition = new Vector2(0, -182); barRect.sizeDelta = new Vector2(420, 18);
        var fill = new GameObject("Jauge", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(arrestBar.transform, false);
        arrestFill = fill.GetComponent<Image>(); arrestFill.color = new Color(1f, .2f, .15f); arrestFill.raycastTarget = false;
        var fillRect = arrestFill.rectTransform;
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(0, 1); fillRect.pivot = new Vector2(0, .5f);
        fillRect.offsetMin = new Vector2(2, 2); fillRect.offsetMax = new Vector2(2, -2);
        arrestBar.SetActive(false);
    }

    private void UpdateHud()
    {
        bool showBar = arrest > .001f;
        arrestBar.SetActive(showBar);
        if (showBar) arrestFill.rectTransform.offsetMax = new Vector2(2 + 416 * arrest, -2);
    }

    private void OnDisable()
    {
        if (catchZone != null && !catchZone.IsCaught) catchZone.SetProgress(0f);
    }

    private void OnDestroy()
    {
        foreach (var block in roadblocks) if (block != null) Destroy(block.gameObject);
        foreach (var material in new[] { beaconDark, beaconRed, beaconBlue, barricadeWhite, barricadeRed })
            if (material != null) Destroy(material);
    }

    private void MaintainRoadblocks()
    {
        if (CompletedOrders > lastRoadblockOrders) nextRoadblock = Time.time;
        lastRoadblockOrders = CompletedOrders;
        for (int i = roadblocks.Count - 1; i >= 0; i--)
        {
            var block = roadblocks[i];
            if (block == null) { roadblocks.RemoveAt(i); continue; }
            float distance = Vector3.Distance(block.transform.position, BusPosition);
            bool retire = distance > 320f || !city.IsAreaReady(block.transform.position) ||
                Time.time - block.CreatedAt > roadblockLifetime;
            // Never remove an obstacle while the player is touching or looking at it.
            if (!retire || distance < 75f || VisibleToPlayer(block.transform.position)) continue;
            foreach (var officer in block.GetComponentsInChildren<PoliceCarAI>()) cars.Remove(officer);
            Destroy(block.gameObject);
            roadblocks.RemoveAt(i);
        }
        if (RoadblockLimit == 0 || roadblocks.Count >= RoadblockLimit || Time.time < nextRoadblock) return;
        // Retry soon when streaming or occupied roads prevent a safe placement.
        nextRoadblock = Time.time + 1f;
        if (!TryRoadblockPosition(out var position, out var heading)) return;
        CreateRoadblock(position, heading);
        nextRoadblock = Time.time + RoadblockInterval;
    }

    private bool TryRoadblockPosition(out Vector3 position, out Vector3 heading)
    {
        position = heading = default;
        Vector3 forward = busRigidbody.rotation * Vector3.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int attempt = 0; attempt < 48; attempt++)
        {
            // Prefer the current direction, then streets beyond the next turns.
            Vector3 probe = BusPosition + forward * Random.Range(110f, 240f) +
                right * (attempt < 16 ? 0f : Random.Range(-2, 3) * ProceduralCity.BlockSize);
            if (!city.NearestLane(probe, forward, out var from, out var to, out _)) continue;
            Vector3 along = ProceduralCity.Flat(to - from);
            Vector3 center = city.JunctionPosition(from) + along * (ProceduralCity.BlockSize * .5f) + Vector3.up * .02f;
            Vector3 offset = center - BusPosition; offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 110f || distance > 260f || Vector3.Dot(offset, forward) < 65f) continue;
            // Hidden placements first. Distant visible placements still leave at least
            // four seconds to react at 130 km/h, with a HUD warning.
            if (VisibleToPlayer(center) && (attempt < 32 || distance < 170f)) continue;
            if (!RoadblockFootprintClear(center, along)) continue;
            bool nearby = false;
            foreach (var block in roadblocks)
                if (block != null && (block.transform.position - center).sqrMagnitude < 48f * 48f) { nearby = true; break; }
            if (nearby) continue;
            position = center; heading = along;
            return true;
        }
        return false;
    }

    private bool RoadblockFootprintClear(Vector3 center, Vector3 heading)
    {
        Quaternion rotation = Quaternion.LookRotation(heading);
        Vector3 side = rotation * Vector3.right;
        // The asphalt is 16 m wide. Cover both lanes and the shoulders; leave
        // intersections free so there is always another street to take.
        foreach (float x in new[] { -9f, 0f, 9f })
            foreach (float z in new[] { -2f, 14f })
                if (!city.IsAreaReady(center + side * x + heading * z)) return false;
        int count = Physics.OverlapBoxNonAlloc(center + heading * 6f + Vector3.up * 1.4f,
            new Vector3(9.5f, 1.1f, 9f), roadblockOverlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == roadblockOverlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var collider = roadblockOverlaps[i];
            // Low road surfaces and kerbs are allowed; vehicles and scenery are not.
            if (collider.bounds.max.y <= center.y + .25f) continue;
            return false;
        }
        return true;
    }

    private PoliceRoadblock CreateRoadblock(Vector3 position, Vector3 heading)
    {
        var group = new GameObject("Barrage de police - " + CompletedOrders + " livraisons");
        group.transform.SetParent(city.WorldRoot, false);
        group.transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
        var block = group.AddComponent<PoliceRoadblock>();
        roadblocks.Add(block);
        int rows = CompletedOrders >= 4 ? 3 : CompletedOrders >= 2 ? 2 : 1;
        for (int row = 0; row < rows; row++)
        {
            for (int segment = 0; segment < 6; segment++)
            {
                float x = -7.5f + segment * 3f;
                var barrier = new GameObject("Barriere solide - TrafficBarrier_2");
                barrier.transform.SetParent(group.transform, false);
                barrier.transform.localPosition = new Vector3(x, 0f, row * 4f);
                var solid = barrier.AddComponent<BoxCollider>();
                solid.center = new Vector3(0f, .7f, 0f);
                solid.size = new Vector3(3f, 1.4f, 1.2f);
                if (barrierPrefab != null)
                    PoliceModelVisual.Create(barrierPrefab, barrier.transform, solid.size);
                else
                    Debug.LogError("Modele TrafficBarrier_2 introuvable.", this);
            }
        }
        for (int i = 0; i < 2; i++)
        {
            var officer = CreateCar();
            officer.transform.SetParent(group.transform, false);
            Vector3 at = group.transform.TransformPoint(new Vector3(i == 0 ? -4.5f : 4.5f, 0f, rows * 4f));
            officer.ParkAtRoadblock(at, group.transform.rotation * Quaternion.Euler(0f, 90f, 0f));
            cars.Add(officer);
        }
        return block;
    }
}
