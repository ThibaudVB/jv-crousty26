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
    [SerializeField, Range(0, 10)] private int patrolCount = 7;
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
    [SerializeField, Min(10f)] private float chaseSpeedKmh = 88f;
    [SerializeField, Min(1f)] private float chaseAcceleration = 9f;
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
    [SerializeField, Range(3, 12)] private int maximumReinforcements = 10;
    private int heat;
    private float nextEscalation, nextBackup;
    public int HeatLevel => heat;
    private int PursuitLimit => Mathf.Min(maximumReinforcements, maxChasers + Mathf.Max(0, heat - 1) * 2);
    private float SearchDuration => loseSightSeconds + Mathf.Max(0, heat - 1) * 1.5f;

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
    private float suspicion, lastSeen, calmUntil, nextSpawn, nextSight, arrest, messageUntil;
    private Vector3 lastKnown;
    private Vector3 lastKnownVelocity;
    private string message = "";
    private Text statusText;
    private Image arrestFill;
    private GameObject arrestBar;

    public IReadOnlyList<PoliceCarAI> Cars => cars;
    public bool ChaseStarted => chasing;
    public bool BusInSight { get; private set; }
    public float SecondsOutOfSight => chasing ? Time.time - lastSeen : 0f;
    public float ArrestProgress => arrest;
    public float ChaseSpeed => Mathf.Max(chaseSpeedKmh / 3.6f + heat * 2f,
        chasing && BusInSight ? BusVelocity.magnitude * 1.08f + 3f : 0f);
    public float ChaseAcceleration => chaseAcceleration + heat * 1.5f;
    public Rigidbody BusBody => busRigidbody;
    public Vector3 BusPosition => busRigidbody.position;
    public Vector3 BusVelocity { get { var v = busRigidbody.linearVelocity; v.y = 0; return v; } }
    public float BusHalfLength { get; private set; } = 3f;
    // Where pursuers head when they cannot see the bus: radio fix first, then the last sighting.
    public Vector3 TrackedPosition => lastKnown + lastKnownVelocity * Mathf.Clamp(Time.time - lastSeen, 0f, radioTrackingSeconds);

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
        calmUntil = Time.time + startupGraceSeconds;
        BuildHud();
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
        MaintainPatrols();

        bool look = Time.time >= nextSight;
        if (look) nextSight = Time.time + .1f;
        float busKmh = BusVelocity.magnitude * 3.6f;
        bool hidden = BusHidden;

        if (!chasing)
        {
            bool spotted = false;
            if (Time.time >= calmUntil && !hidden)
                foreach (var car in cars)
                    if (car.State == PoliceCarAI.Mode.Patrol && !car.IsBlinded &&
                        (busKmh > speedLimitKmh || Vector3.Distance(car.transform.position, BusPosition) < closeAwareness) &&
                        CanSee(car, visionRange, visionHalfAngle))
                    { spotted = true; if ((suspicion += dt) >= spottingSeconds) BeginChase(car); break; }
            if (!spotted) suspicion = Mathf.Max(0f, suspicion - dt);
            SetArrest(Mathf.MoveTowards(arrest, 0f, dt));
            return;
        }

        // Backup arrives a few seconds after the call.
        if (Time.time >= nextEscalation && BusInSight)
        {
            RaiseHeat();
            nextEscalation = Time.time + escalationSeconds;
        }
        RequestBackup();
        for (int i = backup.Count - 1; i >= 0; i--)
        {
            if (backup[i].car == null || backup[i].car.IsDestroyed) { backup.RemoveAt(i); continue; }
            if (Time.time < backup[i].at) continue;
            backup[i].car.StartChase();
            backup.RemoveAt(i);
        }

        BusInSight = false;
        float nearest = float.PositiveInfinity;
        int pinning = 0;
        foreach (var car in cars)
        {
            if (!car.InPursuit) continue;
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
            SetArrest(arrest + dt / bustSeconds * (1f + .5f * (pinning - 1)));
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

    private void BeginChase(PoliceCarAI first)
    {
        chasing = true; suspicion = 0f;
        heat = Mathf.Max(1, heat);
        nextEscalation = Time.time + escalationSeconds;
        nextBackup = 0f;
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
        first.StartChase();
        first.SeesBus = true;
        Flash("POLICE ! Le bus a ete repere");
        backup.Clear();
        var others = new List<PoliceCarAI>();
        foreach (var car in cars)
            if (car != first && car.State == PoliceCarAI.Mode.Patrol &&
                Vector3.Distance(car.transform.position, BusPosition) < backupRadius) others.Add(car);
        others.Sort((a, b) => Vector3.Distance(a.transform.position, BusPosition).CompareTo(Vector3.Distance(b.transform.position, BusPosition)));
        for (int i = 0; i < others.Count && i < maxChasers - 1; i++) backup.Add((others[i], Time.time + 2f + 1.5f * i));
    }

    // Ramming a patrol car, even slowly, starts a chase immediately.
    public void Provoked(PoliceCarAI car)
    {
        if (car == null || car.IsDestroyed) return;
        if (catchZone != null && catchZone.IsCaught) return;
        if (!chasing) BeginChase(car);
        else car.StartChase();
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
    }

    public void CarDestroyed(PoliceCarAI car)
    {
        cars.Remove(car);
        backup.RemoveAll(entry => entry.car == car);
        if (catchZone != null && catchZone.IsCaught) return;
        if (!chasing)
        {
            chasing = true;
            heat = 1;
        }
        RaiseHeat();
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
        nextEscalation = Time.time + escalationSeconds;
        nextBackup = nextSpawn = 0f;
        Flash("VOITURE DE POLICE DETRUITE ! ALERTE " + heat + " - RENFORTS !");
    }

    public void CivilianCarDestroyed(Rigidbody source)
    {
        if (!isActiveAndEnabled || source != busRigidbody || catchZone != null && catchZone.IsCaught) return;
        if (!chasing)
        {
            PoliceCarAI closest = null;
            float nearest = float.PositiveInfinity;
            foreach (var car in cars)
            {
                if (car == null || car.IsDestroyed) continue;
                float distance = (car.transform.position - BusPosition).sqrMagnitude;
                if (distance < nearest) { nearest = distance; closest = car; }
            }
            if (closest != null) BeginChase(closest);
            else { chasing = true; heat = 1; suspicion = 0f; }
        }
        else RaiseHeat();
        lastSeen = Time.time; lastKnown = BusPosition; lastKnownVelocity = BusVelocity;
        nextEscalation = Time.time + escalationSeconds;
        nextBackup = nextSpawn = 0f;
        RequestBackup();
        Flash("VOITURE DETRUITE ! LA POLICE EST A TES TROUSSES !");
    }

    private void RaiseHeat()
    {
        if (heat >= maximumHeat) return;
        heat++;
        Flash("ALERTE " + heat + " - La police appelle des renforts !");
    }

    private void RequestBackup()
    {
        if (!BusInSight && SecondsOutOfSight > radioTrackingSeconds) return;
        if (Time.time < nextBackup) return;
        nextBackup = Time.time + 1f;
        int assigned = backup.Count;
        foreach (var car in cars) if (car.InPursuit) assigned++;
        foreach (var car in cars)
        {
            if (assigned >= PursuitLimit) break;
            if (car.IsDestroyed || car.InPursuit || backup.Exists(entry => entry.car == car)) continue;
            if (Vector3.Distance(car.transform.position, BusPosition) > backupRadius) continue;
            backup.Add((car, Time.time + Mathf.Max(.5f, 2.5f - heat * .35f)));
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
        calmUntil = Time.time + escapeCooldownSeconds;
        SetArrest(0f);
        Flash("Police semee !");
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
        if (any) Flash("Nuage PET : la police est aveuglee !");
        return any;
    }

    // Keeps a few patrols around the bus, spawned and moved out of its sight.
    private void MaintainPatrols()
    {
        if (Time.time < nextSpawn) return;
        nextSpawn = Time.time + .5f;
        bool reinforcing = chasing && (BusInSight || SecondsOutOfSight < radioTrackingSeconds);
        if (cars.Count < (reinforcing ? Mathf.Max(patrolCount, PursuitLimit + 2) : patrolCount))
        {
            if (city.TryFindStreetSpawn(null, 55f, patrolRadius, true, 40, out var from, out var to, out var at))
            { var car = CreateCar(); car.StartPatrol(from, to, at); cars.Add(car); }
            return;
        }
        foreach (var car in cars)
        {
            Vector3 offset = car.transform.position - BusPosition; offset.y = 0;
            if (VisibleToPlayer(car.transform.position)) continue;
            if (car.InPursuit && !reinforcing) continue;
            if (!chasing && cars.Count > patrolCount && offset.magnitude > 90f)
            {
                cars.Remove(car);
                Destroy(car.gameObject);
                return;
            }
            if (offset.magnitude > patrolRadius + 15f || !city.IsAreaReady(car.transform.position)) { Recycle(car); return; }
        }
    }

    public void Recycle(PoliceCarAI car)
    {
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

    private void Flash(string text) { message = text; messageUntil = Time.time + 2.5f; }

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
        var textObject = new GameObject("Statut police", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(canvasObject.transform, false);
        statusText = textObject.GetComponent<Text>();
        statusText.font = (CroustyTypography.LegacyFont != null ? CroustyTypography.LegacyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        statusText.fontSize = 34; statusText.fontStyle = FontStyle.Bold;
        statusText.alignment = TextAnchor.MiddleCenter; statusText.raycastTarget = false;
        textObject.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
        var rect = statusText.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -130); rect.sizeDelta = new Vector2(900, 50);
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
        if (statusText == null) return;
        string text = ""; Color color = Color.white;
        if (Time.time < messageUntil) { text = message; color = chasing ? new Color(1f, .3f, .25f) : new Color(.55f, 1f, .45f); }
        else if (chasing && BusInSight) { text = "ALERTE " + heat + "/" + maximumHeat + " - POURSUITE POLICIERE"; color = new Color(1f, .3f, .25f); }
        else if (chasing) { text = "ALERTE " + heat + " - Hors de vue... " + Mathf.CeilToInt(Mathf.Max(0f, SearchDuration - SecondsOutOfSight)) + " s"; color = new Color(1f, .8f, .3f); }
        statusText.text = text; statusText.color = color;
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
        foreach (var material in new[] { beaconDark, beaconRed, beaconBlue }) if (material != null) Destroy(material);
    }
}
