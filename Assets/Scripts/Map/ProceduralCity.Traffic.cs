using System.Collections.Generic;
using UnityEngine;

// Ambient traffic: cars built from the FBX vehicles drive the same street graph
// as the GPS, always in the right-hand lane, and are recycled around the bus.
public sealed partial class ProceduralCity
{
    public const float LaneOffset = 4f;
    // Distance from a junction centre where turning curves begin (just past the crosswalk).
    public const float JunctionEntry = 9f;
    [Header("Trafic")]
    [Range(0, 80)] public int trafficCars = 48;
    [Tooltip("Longueur des voitures ; les modeles de Resources/Crousty/Vehicles regardent vers +Z.")]
    public float vehicleLength = 4.4f;
    // Bodywork material of NormalCar2, repainted with the city's pastel palette.
    private const string PaintMaterial = "LightBlue";
    private static readonly Color[] Paints =
    {
        new Color(.55f, .70f, .86f), new Color(.93f, .55f, .52f), new Color(.98f, .84f, .48f),
        new Color(.58f, .80f, .62f), new Color(.80f, .65f, .88f), new Color(.95f, .95f, .92f)
    };
    private GameObject[] vehicles = new GameObject[0];
    private readonly Dictionary<Material, Material[]> paints = new Dictionary<Material, Material[]>();
    private readonly List<CityTrafficCar> traffic = new List<CityTrafficCar>();
    private Transform trafficRoot;
    private float nextTrafficSpawn;
    public IReadOnlyList<CityTrafficCar> Traffic => traffic;
    // Police cars in pursuit leave the lanes; civilian cars still keep their distance from them.
    public readonly List<Transform> Pursuers = new List<Transform>();
    public int VehicleModelCount => vehicles.Length;
    public bool IsAreaReady(Vector3 position) => chunks.TryGetValue(ChunkAt(position), out var chunk) && chunk.complete;

    public void AddPolicePatrol(CityTrafficCar car) { if (!traffic.Contains(car)) traffic.Add(car); }
    public void RemovePolicePatrol(CityTrafficCar car) => traffic.Remove(car);

    public static Vector3 Flat(Vector2Int direction) => new Vector3(direction.x, 0, direction.y);
    public Vector3 JunctionPosition(Vector2Int junction) => Origin + new Vector3(junction.x * BlockSize, 0, junction.y * BlockSize);
    // Centre of the right-hand lane of a car heading along `heading`, `along` metres past the junction.
    public Vector3 LanePoint(Vector2Int junction, Vector2Int heading, float along) =>
        JunctionPosition(junction) + Flat(heading) * along + new Vector3(heading.y, 0, -heading.x) * LaneOffset + Vector3.up * .02f;

    public Vector2Int ChooseExit(Vector2Int from, Vector2Int junction)
    {
        Vector2Int heading = junction - from;
        var exits = new List<Vector2Int>(4);
        foreach (var direction in RoadDirections)
        {
            if (direction == -heading || !IsRoadEdgeOpen(junction, junction + direction)) continue;
            exits.Add(direction);
            if (direction == heading) exits.Add(direction); // going straight is twice as likely
        }
        return junction + (exits.Count == 0 ? -heading : exits[Random.Range(0, exits.Count)]);
    }

    // Patrol the player's neighbourhood without aiming directly at the bus or ignoring road rules.
    public Vector2Int ChoosePolicePatrolExit(Vector2Int from, Vector2Int junction)
    {
        if (player == null) return ChooseExit(from, junction);
        Vector2Int heading = junction - from;
        Vector2Int best = from;
        float bestScore = float.PositiveInfinity;
        foreach (var direction in RoadDirections)
        {
            if (direction == -heading || !IsRoadEdgeOpen(junction, junction + direction)) continue;
            Vector3 offset = JunctionPosition(junction + direction) - player.position;
            offset.y = 0f;
            float score = Mathf.Abs(offset.magnitude - 55f) + Random.Range(0f, 24f) - (direction == heading ? 8f : 0f);
            if (score < bestScore) { bestScore = score; best = junction + direction; }
        }
        return best;
    }

    private void UpdateTraffic(bool initial)
    {
        traffic.RemoveAll(car => car == null); // exploded by the bus
        int civilians = 0;
        foreach (var car in traffic) if (!car.IsPolice) civilians++;
        if (vehicles.Length == 0) return;
        if (trafficRoot == null)
        {
            trafficRoot = new GameObject("Trafic").transform;
            trafficRoot.SetParent(WorldRoot, false);
        }
        float far = (viewDistance + .5f) * ChunkSize;
        foreach (var car in traffic)
        {
            if (car.IsPolice) continue; // the police manager keeps its own patrols around the bus
            Vector3 offset = car.transform.position - player.position; offset.y = 0;
            if (offset.sqrMagnitude > far * far) PlaceVehicle(car, false);
        }
        for (int budget = initial ? trafficCars : 1; budget > 0 && civilians < trafficCars; budget--)
        {
            if (!initial && Time.time < nextTrafficSpawn) break;
            var car = CreateVehicle();
            if (!PlaceVehicle(car, initial)) { Destroy(car.gameObject); break; }
            traffic.Add(car); civilians++;
            nextTrafficSpawn = Time.time + .4f;
        }
    }

    private void LoadVehicles()
    {
        vehicles = Resources.LoadAll<GameObject>("Crousty/Vehicles");
        System.Array.Sort(vehicles, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (vehicles.Length == 0) Debug.LogWarning("Ville procedurale : aucun vehicule dans Resources/Crousty/Vehicles.", this);
    }

    private CityTrafficCar CreateVehicle()
    {
        GameObject model = vehicles[Random.Range(0, vehicles.Length)];
        var car = new GameObject("Voiture " + model.name) { tag = "voitures" };
        car.transform.SetParent(trafficRoot, false);
        var bodywork = Instantiate(model, car.transform, false).transform;
        bodywork.name = "Carrosserie";
        // Fit the model to the requested length, wheels on the road, centred on the car.
        Bounds bounds = default;
        bool found = false;
        foreach (var filter in bodywork.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bodywork.InverseTransformPoint(filter.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!found) { bounds = new Bounds(corner, Vector3.zero); found = true; }
                else bounds.Encapsulate(corner);
            }
        }
        float scale = vehicleLength / Mathf.Max(.01f, bounds.size.z);
        bodywork.localScale = Vector3.one * scale;
        bodywork.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        Repaint(bodywork);
        var box = car.AddComponent<BoxCollider>();
        box.size = bounds.size * scale;
        box.center = new Vector3(0, bounds.size.y * scale * .5f, 0);
        var driver = car.AddComponent<CityTrafficCar>();
        driver.Init(this, vehicleLength * .5f);
        driver.cruiseSpeed = Random.Range(9f, 13f);
        return driver;
    }

    private void Repaint(Transform bodywork)
    {
        Color paint = Paints[Random.Range(0, Paints.Length)];
        foreach (var renderer in bodywork.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].name != PaintMaterial) continue;
                if (!paints.TryGetValue(materials[i], out var variants))
                {
                    variants = new Material[Paints.Length];
                    for (int c = 0; c < Paints.Length; c++)
                        variants[c] = new Material(materials[i]) { name = PaintMaterial + " " + c, color = Paints[c] };
                    paints.Add(materials[i], variants);
                }
                materials[i] = variants[System.Array.IndexOf(Paints, paint)];
            }
            renderer.sharedMaterials = materials;
        }
    }

    private void DisposeTraffic()
    {
        foreach (var variants in paints.Values)
            foreach (var material in variants) if (material != null) Destroy(material);
        paints.Clear();
    }

    // Spawns on a loaded street, away from the bus and out of its sight.
    private bool PlaceVehicle(CityTrafficCar car, bool initial)
    {
        if (!TryFindStreetSpawn(car, initial ? 30f : 70f, Mathf.Min(200f, viewDistance * ChunkSize), !initial,
                initial ? 60 : 24, out var from, out var to, out var at)) return false;
        car.Place(from, to, at);
        return true;
    }

    // A free right-hand lane position on a loaded street, between the given distances
    // from the bus and, if asked, out of the camera's sight.
    public bool TryFindStreetSpawn(CityTrafficCar car, float minDistance, float maxDistance, bool hidden, int attempts,
        out Vector2Int from, out Vector2Int to, out Vector3 at)
    {
        from = to = default; at = default;
        Vector3 local = (player.position - Origin) / BlockSize;
        var center = new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.z));
        Camera view = Camera.main;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            from = center + new Vector2Int(Random.Range(-3, 4), Random.Range(-3, 4));
            var heading = RoadDirections[Random.Range(0, RoadDirections.Length)];
            if (!IsRoadEdgeOpen(from, from + heading)) continue;
            at = LanePoint(from, heading, Random.Range(JunctionEntry + 4f, BlockSize - StopLineDistance - 6f));
            Vector3 offset = at - player.position; offset.y = 0;
            float distance = offset.magnitude;
            if (distance < minDistance || distance > maxDistance) continue;
            if (!chunks.TryGetValue(ChunkAt(at), out var chunk) || !chunk.complete) continue;
            if (hidden && view != null && distance < RenderSettings.fogEndDistance + 20f)
            {
                Vector3 viewport = view.WorldToViewportPoint(at);
                if (viewport.z > 0 && viewport.x > -.1f && viewport.x < 1.1f && viewport.y > -.1f && viewport.y < 1.1f) continue;
            }
            bool crowded = false;
            foreach (var other in traffic)
                if (other != null && other != car && (other.transform.position - at).sqrMagnitude < 12f * 12f) crowded = true;
            foreach (var pursuer in Pursuers)
                if (pursuer != null && (car == null || pursuer != car.transform) &&
                    (pursuer.position - at).sqrMagnitude < 12f * 12f) crowded = true;
            if (crowded) continue;
            to = from + heading;
            return true;
        }
        return false;
    }

    // Right-hand lane closest to a position, driving roughly along `forward` (used when a police car resumes its patrol).
    public bool NearestLane(Vector3 position, Vector3 forward, out Vector2Int from, out Vector2Int to, out Vector3 at)
    {
        Vector3 local = (position - Origin) / BlockSize;
        from = to = default; at = default;
        float best = float.PositiveInfinity;
        for (int axis = 0; axis < 2; axis++)
        {
            float line = axis == 0 ? Mathf.Round(local.z) : Mathf.Round(local.x);
            float along = axis == 0 ? local.x : local.z;
            float sign = (axis == 0 ? forward.x : forward.z) >= 0f ? 1f : -1f;
            var heading = axis == 0 ? new Vector2Int((int)sign, 0) : new Vector2Int(0, (int)sign);
            int start = sign > 0 ? Mathf.FloorToInt(along) : Mathf.CeilToInt(along);
            var a = axis == 0 ? new Vector2Int(start, (int)line) : new Vector2Int((int)line, start);
            if (!IsRoadEdgeOpen(a, a + heading)) continue;
            float metres = Mathf.Clamp(Mathf.Abs(along - start) * BlockSize, JunctionEntry + 2f, BlockSize - StopLineDistance - 4f);
            Vector3 candidate = LanePoint(a, heading, metres);
            float d = (candidate - position).sqrMagnitude;
            if (d >= best) continue;
            best = d; from = a; to = a + heading; at = candidate;
        }
        return !float.IsPositiveInfinity(best);
    }

    // Distance the car may still travel before its front bumper reaches the stop
    // line of the junction ahead; infinity once it is cleared to cross.
    public float ClearanceAhead(CityTrafficCar car, Vector3 position)
    {
        if (car.InJunction || car.released) return float.PositiveInfinity;
        Vector2Int approach = -car.Heading;
        float gap = Vector3.Dot(JunctionPosition(car.To) - position, Flat(car.Heading)) - StopLineDistance - car.HalfLength - .3f;
        // Already over the line (spawned close, or a light changed under the bumper).
        if (gap < -.5f) { car.released = true; return float.PositiveInfinity; }
        RoadRule rule = RuleAt(car.To, approach);
        bool wait = false;
        if (rule == RoadRule.TrafficLight)
        {
            LightAspect aspect = LightAt(car.To, approach, Time.time);
            wait = aspect == LightAspect.Red ||
                aspect == LightAspect.Amber && gap > car.Speed * car.Speed / (2f * car.braking);
        }
        else if (rule == RoadRule.Stop)
        {
            if (car.Speed > .3f || gap > 1f) wait = true;
            else
            {
                if (car.stoppedAt < 0f) car.stoppedAt = Time.time;
                wait = Time.time - car.stoppedAt < 1f;
            }
        }
        if (!wait && (rule == RoadRule.Stop || rule == RoadRule.Yield)) wait = PriorityTrafficNear(car);
        if (!wait && car.NextTurn == CityTrafficCar.Turn.Left) wait = OncomingTraffic(car);
        if (!wait) wait = JunctionBusy(car) || ExitBlocked(car);
        if (wait) return Mathf.Max(0f, gap);
        if (gap < .3f) car.released = true;
        return float.PositiveInfinity;
    }

    // Do not enter while a car whose path crosses ours is still in the junction.
    private bool JunctionBusy(CityTrafficCar car)
    {
        foreach (var other in traffic)
        {
            if (other == null || other == car || other.To != car.To || other.Heading == car.Heading) continue;
            if (!other.InJunction && !other.released) continue;
            if (Crosses(car, other)) return true;
        }
        return false;
    }

    private static bool Crosses(CityTrafficCar a, CityTrafficCar b)
    {
        if (a.NextTurn == CityTrafficCar.Turn.Back || b.NextTurn == CityTrafficCar.Turn.Back) return true;
        // Opposite cars only meet when exactly one of them turns left.
        if (a.Heading == -b.Heading) return (a.NextTurn == CityTrafficCar.Turn.Left) != (b.NextTurn == CityTrafficCar.Turn.Left);
        // Perpendicular cars only avoid each other when both turn right.
        return a.NextTurn != CityTrafficCar.Turn.Right || b.NextTurn != CityTrafficCar.Turn.Right;
    }

    // Do not block the box: wait until the chosen exit has room for the car.
    private bool ExitBlocked(CityTrafficCar car)
    {
        Vector3 node = JunctionPosition(car.To);
        Vector3 exit = Flat(car.Next - car.To);
        foreach (var other in traffic)
        {
            if (other == null || other == car || other.From != car.To || other.To != car.Next || other.Speed > 2f) continue;
            if (Vector3.Dot(other.transform.position - node, exit) < JunctionEntry + other.HalfLength + car.HalfLength * 2f + 3f) return true;
        }
        return false;
    }

    // A minor approach gives way to cars in the junction and to priority traffic.
    private bool PriorityTrafficNear(CityTrafficCar car)
    {
        Vector3 node = JunctionPosition(car.To);
        foreach (var other in traffic)
        {
            if (other == null || other == car || other.To != car.To || other.Heading == car.Heading) continue;
            if (other.InJunction) return true;
            float distance = Vector3.Dot(node - other.transform.position, Flat(other.Heading));
            if (distance < 35f && other.Speed > .5f && RuleAt(car.To, -other.Heading) == RoadRule.None) return true;
        }
        return false;
    }

    // A left turn crosses the opposite lane: wait for oncoming cars that go on.
    private bool OncomingTraffic(CityTrafficCar car)
    {
        Vector3 node = JunctionPosition(car.To);
        foreach (var other in traffic)
        {
            if (other == null || other == car || other.To != car.To || other.Heading != -car.Heading ||
                other.NextTurn == CityTrafficCar.Turn.Left) continue;
            if (other.InJunction) return true;
            float distance = Vector3.Dot(node - other.transform.position, Flat(other.Heading));
            if (distance < 40f && other.Speed > 1f) return true;
        }
        return false;
    }
}
