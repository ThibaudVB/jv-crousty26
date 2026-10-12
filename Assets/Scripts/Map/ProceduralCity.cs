using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

// Roads share a global grid; only the contents of each block depend on its seed.
[DefaultExecutionOrder(-1200)]
public sealed partial class ProceduralCity : MonoBehaviour
{
    public enum District { Residential, Downtown, Shops, Gardens, Industrial }
    public const float BlockSize = 64f;
    public const float ChunkSize = BlockSize * 2f;
    public GameObject cityModel;
    public Transform player;
    public DeliveryManager deliveries;
    public bool randomSeed = true;
    public int seed = 26011;
    [Range(1, 4)] public int viewDistance = 2;
    [Range(1, 4)] public int chunksPerFrame = 1;
    [Tooltip("Budget souple par frame ; une operation Unity reste indivisible.")]
    [Range(.5f, 6f)] public float generationBudgetMs = 2f;
    public bool streamingFog = true;
    public Color streamingFogColor = new Color(.69f, .77f, .83f);
    public bool dayNightCycle = true;
    public float groundHeight = -1.33f;
    public int Seed { get; private set; }
    public Transform WorldRoot { get; private set; }
    public int LoadedChunkCount => chunks.Count;
    public int BuildingVariantCount => buildings.Count;
    public int LandmarkVariantCount => landmarks.Count;
    public Vector3 Origin { get; private set; }

    private sealed class Template
    {
        public string name;
        public Mesh mesh;
        public Material[] materials;
        public Matrix4x4 matrix;
        public Bounds bounds;
        public Template sign;
    }
    private sealed class Chunk
    {
        public GameObject root;
        public bool complete;
        public Mesh roadMarkings;
        public readonly List<Mesh> signMeshes = new List<Mesh>();
        public readonly List<SignalDisplay> signals = new List<SignalDisplay>();
        public readonly List<DeliveryPoint> addresses = new List<DeliveryPoint>();
        public readonly List<CroustyShop> shops = new List<CroustyShop>();
        public readonly List<CarPointRoute> routes = new List<CarPointRoute>();
    }
    private sealed class Landmark
    {
        public string name;
        public readonly List<Template> parts = new List<Template>();
        public Bounds bounds;
    }
    private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private readonly List<Template> buildings = new List<Template>();
    private readonly List<Template> trees = new List<Template>();
    private readonly List<Landmark> landmarks = new List<Landmark>();
    private readonly List<Vector2Int> removal = new List<Vector2Int>();
    private CarFollowPoints[] cars;
    private Template restaurant;
    private Material asphalt, pavement, grass, markings, wood;
    private BusGPS gps;
    private BusMinimap minimap;
    private static readonly ProfilerMarker StreamingMarker = new ProfilerMarker("Crousty.City.Streaming");
    private readonly Queue<Chunk> retiredChunks = new Queue<Chunk>();
    private readonly List<MeshFilter> retirementMeshes = new List<MeshFilter>();
    private Chunk retiringChunk;
    private int retirementIndex;
    private IEnumerator activeBuild;
    private Vector2Int activeCoordinate;
    private readonly Queue<ImportWork> importWork = new Queue<ImportWork>();
    private bool deferImports;
    private bool previousFog, fogConfigured;
    private FogMode previousFogMode;
    private Color previousFogColor;
    private float previousFogStart, previousFogEnd, nextMaintenance;
    private struct ImportWork
    {
        public Template part;
        public Transform root;
        public Vector3 offset;
        public bool solid;
    }


    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<BusController>()?.transform;
        if (deliveries == null) deliveries = FindAnyObjectByType<DeliveryManager>();
        if (player == null || deliveries == null || cityModel == null)
        {
            Debug.LogError("Ville procedurale : modele, bus et livraisons requis.", this);
            enabled = false;
            return;
        }
        ReadTemplates();
        if (buildings.Count == 0 || restaurant == null)
        {
            Debug.LogError("Ville procedurale : batiments ou restaurant CROUSTY manquants.", this);
            enabled = false;
            return;
        }
        Seed = randomSeed ? Guid.NewGuid().GetHashCode() : seed;
        Origin = new Vector3(player.position.x, groundHeight, player.position.z);
        WorldRoot = new GameObject("Ville procedurale - seed " + Seed).transform;
        WorldRoot.SetParent(transform, false);
        WorldRoot.position = Origin;
        if (dayNightCycle)
        {
            var cycle = GetComponent<CroustyDayNightCycle>();
            if (cycle == null) cycle = gameObject.AddComponent<CroustyDayNightCycle>();
            cycle.player = player;
            foreach (var part in catalog.Values) part.materials = cycle.PrepareLuminousMaterials(part.materials);
        }
        asphalt = Material(new Color(0.19f, 0.22f, 0.27f));
        pavement = Material(new Color(0.67f, 0.64f, 0.61f));
        grass = Material(new Color(0.39f, 0.66f, 0.45f));
        markings = Material(new Color(0.96f, 0.96f, 0.9f));
        wood = Material(new Color(0.53f, 0.35f, 0.23f));
        PrepareRoadMaterials();
        cars = FindObjectsByType<CarFollowPoints>();
        foreach (var setup in FindObjectsByType<SceneBaseTrafficSetup>()) setup.enabled = false;
        foreach (var route in FindObjectsByType<CarPointRoute>()) route.gameObject.SetActive(false);
        foreach (var intersection in FindObjectsByType<IntersectionChooser>()) intersection.gameObject.SetActive(false);
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (root.name.StartsWith("CroustyCity_") || root.name == "City") root.SetActive(false);
        foreach (var point in deliveries.deliveryPoints)
            if (point != null) point.gameObject.SetActive(false);
        deliveries.deliveryPoints.Clear();
        if (deliveries.shop != null) deliveries.shop.gameObject.SetActive(false);
        deliveries.shop = null;
        gps = deliveries.GetComponent<BusGPS>();
        minimap = deliveries.GetComponent<BusMinimap>();
        if (gps != null) gps.navigationRoot = WorldRoot;
        if (minimap != null) minimap.mapRoot = WorldRoot;
        // A complete neighbourhood is present before physics and deliveries start.
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++) LoadChunk(new Vector2Int(x, z));
        UpdateShop();
        UpdateTraffic(true);
        ConfigureStreamingFog();

        Debug.Log("Ville procedurale : seed " + Seed + ", " + buildings.Count +
            " batiments et " + landmarks.Count + " lieux remarquables.", this);
    }

    private void Update()
    {
        if (WorldRoot == null || player == null || Time.timeScale <= 0f) return;
        Vector2Int center = ChunkAt(player.position);
        EnsureNearestShop();
        // Protect physics, including teleports, without building nine full districts.
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            EnsureGround(center + new Vector2Int(x, z));
        if (activeBuild != null && DistanceToChunk(activeCoordinate) > (viewDistance + 2) * ChunkSize &&
            !IsPinned(activeCoordinate, chunks[activeCoordinate]))
        {
            (activeBuild as IDisposable)?.Dispose();
            activeBuild = null;
            importWork.Clear();
            UnloadChunk(activeCoordinate);
        }
        double deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(.5f, generationBudgetMs) / 1000.0;
        int completed = 0;
        using (StreamingMarker.Auto())
        {
            // Reserve a little of the shared budget for deferred Unity destruction.
            for (int i = 0; i < 8 && Time.realtimeSinceStartupAsDouble < deadline; i++)
                if (!RetireNextMesh()) break;
            do
            {
                if (activeBuild == null)
                {
                    if (!TryNextChunk(center, out activeCoordinate)) break;
                    activeBuild = BuildChunkSteps(activeCoordinate);
                }
                if (!activeBuild.MoveNext())
                {
                    (activeBuild as IDisposable)?.Dispose();
                    activeBuild = null;
                    completed++;
                }
            } while (Time.realtimeSinceStartupAsDouble < deadline && completed < Mathf.Max(1, chunksPerFrame));
        }
        UpdateStreamingFog(center);
        RefreshSignals();
        if (Time.unscaledTime < nextMaintenance) return;
        nextMaintenance = Time.unscaledTime + .2f;
        UpdateShop();
        UpdateTraffic(false);
        removal.Clear();
        foreach (var pair in chunks)
        {
            if (pair.Key == activeCoordinate && activeBuild != null) continue;
            if (DistanceToChunk(pair.Key) <= (viewDistance + 1) * ChunkSize || IsPinned(pair.Key, pair.Value)) continue;
            removal.Add(pair.Key);
        }
        // Retire one district per maintenance pass to spread destruction costs too.
        if (removal.Count > 0) UnloadChunk(removal[0]);
    }

    private bool IsPinned(Vector2Int coordinate, Chunk chunk)
    {
        if (coordinate == nearestShopChunk) return true;
        foreach (var address in chunk.addresses)
            if (address != null && address.HasOpenOrder) return true;
        return false;
    }

    private float DistanceToChunk(Vector2Int coordinate)
    {
        Vector3 p = player.position - Origin;
        float x = Mathf.Max(coordinate.x * ChunkSize - p.x, 0, p.x - (coordinate.x + 1) * ChunkSize);
        float z = Mathf.Max(coordinate.y * ChunkSize - p.z, 0, p.z - (coordinate.y + 1) * ChunkSize);
        return Mathf.Sqrt(x * x + z * z);
    }

    private bool TryNextChunk(Vector2Int center, out Vector2Int next)
    {
        next = center;
        float best = float.PositiveInfinity;
        int radius = viewDistance + 1;
        for (int x = -radius; x <= radius; x++) for (int z = -radius; z <= radius; z++)
        {
            var candidate = center + new Vector2Int(x, z);
            if (chunks.TryGetValue(candidate, out var existing) && existing.complete) continue;
            float distance = DistanceToChunk(candidate);
            if (distance > viewDistance * ChunkSize || distance >= best) continue;
            best = distance;
            next = candidate;
        }
        if (float.IsPositiveInfinity(best) &&
            (!chunks.TryGetValue(nearestShopChunk, out var shopChunk) || !shopChunk.complete))
        { next = nearestShopChunk; return true; }
        return !float.IsPositiveInfinity(best);
    }

    private void ConfigureStreamingFog()
    {
        if (!streamingFog) return;
        previousFog = RenderSettings.fog;
        previousFogMode = RenderSettings.fogMode;
        previousFogColor = RenderSettings.fogColor;
        previousFogStart = RenderSettings.fogStartDistance;
        previousFogEnd = RenderSettings.fogEndDistance;
        fogConfigured = true;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = streamingFogColor;
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 110f;
    }

    private void UpdateStreamingFog(Vector2Int center)
    {
        if (!fogConfigured) return;
        float frontier = viewDistance * ChunkSize;
        int radius = viewDistance + 1;
        for (int x = -radius; x <= radius; x++) for (int z = -radius; z <= radius; z++)
        {
            var coordinate = center + new Vector2Int(x, z);
            if (!chunks.TryGetValue(coordinate, out var chunk) || !chunk.complete)
                frontier = Mathf.Min(frontier, DistanceToChunk(coordinate));
        }
        float target = Mathf.Clamp(frontier - 24f, 40f, viewDistance * ChunkSize - 32f);
        // Close immediately for safety, reveal the skyline gradually.
        float end = Mathf.Min(target, RenderSettings.fogEndDistance + Time.unscaledDeltaTime * 45f);
        RenderSettings.fogEndDistance = end;
        RenderSettings.fogStartDistance = end * .45f;
    }

    public Vector2Int ChunkAt(Vector3 position) => new Vector2Int(
        Mathf.FloorToInt((position.x - Origin.x) / ChunkSize),
        Mathf.FloorToInt((position.z - Origin.z) / ChunkSize));

    public int CoordinateSeed(int x, int z, int salt = 0)
    {
        unchecked
        {
            uint value = (uint)Seed ^ (uint)x * 0x9E3779B9u ^ (uint)z * 0x85EBCA6Bu ^ (uint)salt;
            value ^= value >> 16; value *= 0x7FEB352Du;
            value ^= value >> 15; value *= 0x846CA68Bu;
            return (int)(value ^ (value >> 16));
        }
    }

    public District DistrictAt(int blockX, int blockZ) => (District)(
        (uint)CoordinateSeed(Mathf.FloorToInt(blockX / 4f), Mathf.FloorToInt(blockZ / 4f), 13) % 5);

    private Chunk EnsureGround(Vector2Int coordinate)
    {
        if (chunks.TryGetValue(coordinate, out var existing)) return existing;
        var chunk = new Chunk { root = new GameObject("Quartier " + coordinate.x + "," + coordinate.y) };
        chunk.root.transform.SetParent(WorldRoot, false);
        chunk.root.transform.localPosition = new Vector3(coordinate.x * ChunkSize, 0, coordinate.y * ChunkSize);
        chunks.Add(coordinate, chunk);
        Box("Asphalte", chunk.root.transform, new Vector3(64, -.15f, 64),
            new Vector3(128, .3f, 128), asphalt, RadarMapElement.Kind.Road);
        return chunk;
    }

    // Initial neighbourhood only: gameplay starts with its collisions available.
    private void LoadChunk(Vector2Int coordinate)
    {
        if (chunks.TryGetValue(coordinate, out var existing) && existing.complete) return;
        var steps = BuildChunkSteps(coordinate);
        while (steps.MoveNext()) { }
        (steps as IDisposable)?.Dispose();
    }

    private IEnumerator BuildChunkSteps(Vector2Int coordinate)
    {
        Chunk chunk = EnsureGround(coordinate);
        yield return null;
        int blocks = FeatureChunk(coordinate) != null ? 1 : 4;
        for (int i = 0; i < blocks; i++)
        {
            deferImports = true;
            try
            {
                if (blocks == 1) BuildFeatureChunk(chunk, coordinate);
                else BuildBlock(chunk, coordinate.x * 2 + i / 2, coordinate.y * 2 + i % 2,
                    new Vector3(i / 2 * BlockSize + 32, 0, i % 2 * BlockSize + 32));
            }
            finally { deferImports = false; }
            yield return null;
            while (importWork.Count > 0)
            {
                var work = importWork.Dequeue();
                AddImported(work.part, work.root, work.offset, work.solid);
                yield return null;
            }
        }
        var roads = BuildRoadRuleSteps(chunk, coordinate);
        while (roads.MoveNext()) yield return null;
        chunk.complete = true;
        foreach (var address in chunk.addresses) deliveries.RegisterPoint(address);
        minimap?.AddMapGeometry(chunk.root.transform);
    }

    private void UnloadChunk(Vector2Int coordinate)
    {
        Chunk chunk = chunks[coordinate];
        foreach (var point in chunk.addresses) deliveries.UnregisterPoint(point);
        minimap?.RemoveMapGeometry(chunk.root.transform);
        chunk.root.SetActive(false);
        retiredChunks.Enqueue(chunk);
        chunks.Remove(coordinate);

    }

    private bool RetireNextMesh()
    {
        if (retiringChunk == null)
        {
            if (retiredChunks.Count == 0) return false;
            retiringChunk = retiredChunks.Dequeue();
            retirementMeshes.Clear();
            retiringChunk.root.GetComponentsInChildren(true, retirementMeshes);
            retirementIndex = 0;
        }
        if (retirementIndex < retirementMeshes.Count)
        {
            var filter = retirementMeshes[retirementIndex++];
            if (filter != null) Destroy(filter.gameObject);
        }
        else
        {
            DisposeChunkMeshes(retiringChunk);
            Destroy(retiringChunk.root);
            retiringChunk = null;
        }
        return true;
    }

    private static void DisposeChunkMeshes(Chunk chunk)
    {
        if (chunk.roadMarkings != null) Destroy(chunk.roadMarkings);
        foreach (var mesh in chunk.signMeshes) if (mesh != null) Destroy(mesh);
    }

    private void UpdateShop()
    {
        CroustyShop closest = null;
        float best = float.PositiveInfinity;
        foreach (var chunk in chunks.Values)
            foreach (var shop in chunk.shops)
            {
                if (!chunk.complete) continue;
                float distance = (shop.transform.position - player.position).sqrMagnitude;
                if (distance < best) { closest = shop; best = distance; }
            }
        deliveries.shop = closest;
    }

    private void UpdateTraffic(bool initial)
    {
        foreach (var car in cars)
        {
            if (car == null) continue;
            if (!initial && car.route != null && (car.transform.position - player.position).sqrMagnitude < ChunkSize * ChunkSize * 4)
                continue;
            CarPointRoute best = null;
            float distance = float.PositiveInfinity;
            int corner = 0;
            foreach (var chunk in chunks.Values)
                foreach (var route in chunk.routes)
                    for (int i = 0; i < 4; i++)
                    {
                        if (!chunk.complete) continue;
                        float d = (route.transform.GetChild(i).position - player.position).sqrMagnitude;
                        // Spread the cars among nearby circuits instead of stacking at one corner.
                        if (d < 20 * 20 || d >= distance) continue;
                        bool occupied = false;
                        foreach (var other in cars) if (other != car && other != null && other.route == route) occupied = true;
                        if (occupied) continue;
                        best = route; distance = d; corner = i;
                    }
            if (best == null) continue;
            car.transform.position = best.transform.GetChild(corner).position;
            int next = (corner + 1) % 4;
            car.transform.rotation = Quaternion.LookRotation(best.transform.GetChild(next).position - car.transform.position);
            car.proceduralCity = this;
            car.FollowRoute(best, next);
        }
    }

    // Search the street graph: full-size stunt plazas and parks close their internal
    // streets, while the surrounding roads remain connected.
    public bool IsRoadEdgeOpen(Vector2Int a, Vector2Int b)
    {
        if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) != 1) return false;
        Vector2 midpoint = ((Vector2)a + (Vector2)b) * .5f;
        var chunk = new Vector2Int(Mathf.FloorToInt(midpoint.x / 2), Mathf.FloorToInt(midpoint.y / 2));
        if (FeatureChunk(chunk) == null) return true;
        return a.x == b.x ? FloorMod(a.x, 2) == 0 : FloorMod(a.y, 2) == 0;
    }

    public bool TryBuildRoute(Vector3 start, Vector3 end, List<Vector3> route)
    {
        route.Clear();
        if (WorldRoot == null) return false;
        Vector3 a = RoadProjection(start, out var first), b = RoadProjection(end, out var last);
        var queue = new SortedSet<(int score, int tie, int serial, Vector2Int point)>(
            Comparer<(int score, int tie, int serial, Vector2Int point)>.Create((left, right) =>
            {
                int order = left.score.CompareTo(right.score);
                if (order == 0) order = left.tie.CompareTo(right.tie);
                return order == 0 ? left.serial.CompareTo(right.serial) : order;
            }));
        var cost = new Dictionary<Vector2Int, int> { [first] = 0 };
        var previous = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();
        int serial = 0;
        queue.Add((Manhattan(first, last), 0, serial++, first));
        var directions = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        while (queue.Count > 0 && closed.Count < 16384)
        {
            var next = queue.Min; queue.Remove(next);
            Vector2Int current = next.point;
            if (!closed.Add(current)) continue;
            if (current == last)
            {
                var reverse = new List<Vector2Int> { last };
                while (current != first) { current = previous[current]; reverse.Add(current); }
                route.Add(a);
                for (int i = reverse.Count - 1; i >= 0; i--) AddCorner(route, Origin + new Vector3(reverse[i].x * BlockSize, .1f, reverse[i].y * BlockSize));
                AddCorner(route, b);
                return true;
            }
            foreach (var direction in directions)
            {
                Vector2Int neighbor = current + direction;
                if (!IsRoadEdgeOpen(current, neighbor) || closed.Contains(neighbor)) continue;
                int value = cost[current] + 1;
                if (cost.TryGetValue(neighbor, out int oldCost) && oldCost <= value) continue;
                cost[neighbor] = value; previous[neighbor] = current;
                queue.Add((value + Manhattan(neighbor, last), -value, serial++, neighbor));
            }
        }
        return false;
    }
    private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private Vector3 RoadProjection(Vector3 world, out Vector2Int junction)
    {
        Vector3 p = (world - Origin) / BlockSize;
        var center = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        float best = float.PositiveInfinity;
        Vector3 result = Vector3.zero; junction = center;
        for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
            for (int axis = 0; axis < 2; axis++)
            {
                var a = center + new Vector2Int(x, z);
                var b = a + (axis == 0 ? Vector2Int.right : Vector2Int.up);
                if (!IsRoadEdgeOpen(a, b)) continue;
                Vector2 point = axis == 0 ? new Vector2(Mathf.Clamp(p.x, a.x, b.x), a.y) :
                    new Vector2(a.x, Mathf.Clamp(p.z, a.y, b.y));
                float d = (point - new Vector2(p.x, p.z)).sqrMagnitude;
                if (d >= best) continue;
                best = d; result = new Vector3(point.x * BlockSize, .1f, point.y * BlockSize);
                junction = (point - (Vector2)a).sqrMagnitude < (point - (Vector2)b).sqrMagnitude ? a : b;
            }
        return Origin + result;
    }
    private static void AddCorner(List<Vector3> route, Vector3 point)
    {
        if ((route[route.Count - 1] - point).sqrMagnitude > .01f) route.Add(point);
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
        Material material, RadarMapElement.Kind? kind, bool solid = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) { box.GetComponent<Collider>().enabled = false; Destroy(box.GetComponent<Collider>()); }
        if (kind.HasValue) box.AddComponent<RadarMapElement>().kind = kind.Value;
        return box;
    }
    private static Material Material(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        material.enableInstancing = true;
        return material;
    }
    private static int FloorMod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
    private void OnDestroy()
    {
        (activeBuild as IDisposable)?.Dispose();
        importWork.Clear();
        if (fogConfigured)
        {
            RenderSettings.fog = previousFog;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFogColor;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
        }
        if (WorldRoot != null) Destroy(WorldRoot.gameObject);
        foreach (var chunk in chunks.Values) DisposeChunkMeshes(chunk);
        foreach (var chunk in retiredChunks) DisposeChunkMeshes(chunk);
        if (retiringChunk != null) DisposeChunkMeshes(retiringChunk);
        foreach (var material in new[] { asphalt, pavement, grass, markings, wood })
            if (material != null) Destroy(material);
        DisposeRoadMaterials();
    }
}
