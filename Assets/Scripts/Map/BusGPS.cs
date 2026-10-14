using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class BusGPS : MonoBehaviour
{
    public DeliveryManager deliveries;
    [Tooltip("Racine des routes et obstacles. A renseigner apres generation procedurale.")]
    public Transform navigationRoot;
    [Range(0, 1)] public int startingCharges = 0;
    public int Charges { get; private set; }
    public bool CanReceiveCharges => Charges < 1 &&
        (Inventory == null || Inventory.CanAddPower(PowerUpInventory.PowerUpType.GPS));
    public bool BonusEquipped { get; set; } = true;
    public bool ShowGarageMarkers { get; private set; } = true;
    public DeliveryPoint Target { get; private set; }
    public bool RoutingToGarage { get; private set; }
    public readonly List<Vector3> Route = new List<Vector3>();
    public string Status { get; private set; } = "[E] GPS";
    private NavMeshData data;
    private NavMeshDataInstance instance;
    private int agentType = -1;
    private NavMeshQueryFilter filter;
    private NavMeshPath path;
    private float nextRepath;
    private Vector3 garageDestination;
    private ProceduralCity proceduralCity;
    private readonly List<Vector3> proceduralRoute = new List<Vector3>();
    private PowerUpInventory inventory;
    private PowerUpInventory Inventory
    {
        get
        {
            if (inventory == null && deliveries != null && deliveries.player != null)
            {
                var bus = deliveries.player.GetComponentInParent<BusController>();
                if (bus != null) inventory = PowerUpInventory.EnsureFor(bus.gameObject);
            }
            return inventory;
        }
    }

    private void Awake()
    {
        path = new NavMeshPath();
        Charges = Mathf.Clamp(startingCharges, 0, 1);
        if (deliveries == null) deliveries = GetComponent<DeliveryManager>();
        if (Inventory != null && Charges > 0)
            Inventory.TryAddPower(PowerUpInventory.PowerUpType.GPS, Charges);
        proceduralCity = FindAnyObjectByType<ProceduralCity>();
    }
    private void OnEnable() { if (deliveries != null) deliveries.DeliveryCompleted += OnDelivered; }
    private void OnDisable()
    {
        if (deliveries != null) deliveries.DeliveryCompleted -= OnDelivered;
        Target = null;
        RoutingToGarage = false;
        Route.Clear();
    }
    public void GrantBonus() => AddCharges(1, 1);
    public bool AddCharges(int amount, int max)
    {
        int addedAmount = Mathf.Min(amount, Mathf.Min(max, 1) - Charges);
        if (addedAmount <= 0) return false;
        if (Inventory != null && !Inventory.TryAddPower(PowerUpInventory.PowerUpType.GPS, addedAmount))
            return false;
        Charges += addedAmount;
        Status = "[E] GPS";
        BonusEquipped = true;
        return true;
    }

    // Appeler apres modification/remplacement de la carte procedurale.
    public void RebuildNavigation(Transform root)
    {
        ReleaseNavigation();
        navigationRoot = root;
        Target = null;
        RoutingToGarage = false;
        Route.Clear();
    }
    private bool EnsureNavigation()
    {
        if (data != null) return true;
        if (navigationRoot == null)
        {
            // Adaptateur pour la carte actuelle ; aucun nom de route requis par le calcul.
            foreach (MeshCollider collider in FindObjectsByType<MeshCollider>())
                if (collider.name == "Ground_Asphalt") { navigationRoot = collider.transform.root; break; }
        }
        if (navigationRoot == null) { Status = "GPS: map unavailable"; return false; }
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(navigationRoot, ~0, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(s => s.component != null &&
            (s.component.GetComponentInParent<Rigidbody>() != null ||
             (s.component is Collider c && c.isTrigger)));
        foreach (var source in sources)
            if (source.sourceObject is Mesh mesh && !mesh.isReadable)
            {
                Status = "GPS: map data unavailable";
                return false;
            }
        var renderers = navigationRoot.GetComponentsInChildren<Renderer>();
        if (sources.Count == 0 || renderers.Length == 0) return false;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        bounds.Expand(10f);
        var settings = NavMesh.CreateSettings();
        agentType = settings.agentTypeID;
        settings.agentRadius = 2f;
        settings.agentHeight = 4f;
        settings.agentClimb = 0.3f;
        settings.agentSlope = 35f;
        settings.overrideVoxelSize = true;
        settings.voxelSize = 0.4f;
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data == null) { ReleaseNavigation(); Status = "GPS: navigation unavailable"; return false; }
        instance = NavMesh.AddNavMeshData(data);
        filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        return true;
    }
    private bool Calculate(DeliveryPoint point)
    {
        return point != null && Calculate(point.transform.position);
    }
    private bool Calculate(Vector3 destination)
    {
        if (deliveries == null || deliveries.player == null) return false;
        if (proceduralCity != null && proceduralCity.WorldRoot != null)
            return proceduralCity.TryBuildRoute(deliveries.player.position, destination, proceduralRoute);
        if (!EnsureNavigation()) return false;
        // Echantillonnage court : ne jamais accrocher un toit ou une rue de l'autre cote d'un mur.
        return NavMesh.SamplePosition(deliveries.player.position, out var start, 6f, filter) &&
            NavMesh.SamplePosition(destination, out var end, 4f, filter) &&
            NavMesh.CalculatePath(start.position, end.position, filter, path) &&
            path.status == NavMeshPathStatus.PathComplete;
    }
    public bool SelectNearestGarageRoute()
    {
        if (!isActiveAndEnabled || deliveries == null || deliveries.player == null ||
            Time.timeScale <= 0f)
            return false;
        if (proceduralCity == null)
            proceduralCity = FindAnyObjectByType<ProceduralCity>();
        if (proceduralCity == null || proceduralCity.WorldRoot == null)
        {
            Status = "GPS: garage route unavailable";
            return false;
        }

        var garages = new List<Vector3>();
        proceduralCity.GetNearbyGarages(deliveries.player.position, 5000f, garages);
        for (int i = 0; i < garages.Count; i++)
        {
            Vector3 garage = garages[i];
            if (!Calculate(garage))
                continue;

            Target = null;
            RoutingToGarage = true;
            garageDestination = garage;
            SetRoute();
            nextRepath = Time.time + 0.75f;
            return true;
        }

        Target = null;
        RoutingToGarage = false;
        Route.Clear();
        Status = "GPS: no reachable garage";
        return false;
    }
    public void DisableGarageGuidance()
    {
        ShowGarageMarkers = false;
        if (!RoutingToGarage) return;
        RoutingToGarage = false;
        Route.Clear();
        Status = Target != null ? "[E] GPS: next delivery" :
            Charges > 0 ? "[E] GPS: next delivery" : "GPS depleted";
    }
    public bool SelectNext()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || Charges <= 0 ||
            (Inventory != null && Inventory.PrimaryPower != PowerUpInventory.PowerUpType.GPS) ||
            deliveries == null) return false;
        if (Target != null && !Target.IsWaiting) return false; // Colis en vol.
        var candidates = new List<DeliveryPoint>();
        foreach (var point in deliveries.deliveryPoints)
            if (point != null && point.IsWaiting) candidates.Add(point);
        if (deliveries.player == null) return false;
        if (candidates.Count == 0)
        {
            Status = "GPS: pick up some orders";
            return false;
        }
        if ((proceduralCity == null || proceduralCity.WorldRoot == null) && !EnsureNavigation()) return false;
        candidates.Sort((a,b) => (a.transform.position-deliveries.player.position).sqrMagnitude
            .CompareTo((b.transform.position-deliveries.player.position).sqrMagnitude));
        int index = candidates.IndexOf(Target);
        for (int i=1; i<=candidates.Count; i++)
        {
            var candidate = candidates[(index+i)%candidates.Count];
            if (!Calculate(candidate)) continue;
            Target = candidate;
            RoutingToGarage = false;
            Charges--;
            Inventory?.Consume(PowerUpInventory.PowerUpType.GPS);
            SetRoute();
            return true;
        }
        Target = null;
        RoutingToGarage = false;
        Route.Clear();
        Status = candidates.Count == 0 ? "GPS: pick up some orders" : "GPS: no route available";
        return false;
    }
    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Time.time < nextRepath) return;
        if (RoutingToGarage)
        {
            nextRepath = Time.time + 0.75f;
            if (Calculate(garageDestination)) SetRoute();
            else { Route.Clear(); Status = "GPS: get back on the road"; }
            return;
        }
        if (Target == null || !Target.IsWaiting) return;
        nextRepath = Time.time + 0.75f;
        if (Calculate(Target)) SetRoute();
        else { Route.Clear(); Status = "GPS: get back on the road"; }
    }
    private void SetRoute()
    {
        Route.Clear();
        if (proceduralCity != null && proceduralCity.WorldRoot != null) Route.AddRange(proceduralRoute);
        else Route.AddRange(path.corners);
        float distance = 0;
        for (int i=1; i<Route.Count; i++) distance += Vector3.Distance(Route[i-1], Route[i]);
        Status = RoutingToGarage ? $"GPS: garage {distance:0} m" : $"GPS: {distance:0} m";
    }
    private void OnDelivered(DeliveryPoint point)
    {
        if (point != Target) return;
        Target = null;
        RoutingToGarage = false;
        Route.Clear();
        Status = Charges > 0 ? "[E] GPS: next delivery" : "GPS depleted";
    }
    private void ReleaseNavigation()
    {
        if (instance.valid) instance.Remove();
        if (data != null) Destroy(data);
        data = null;
        if (agentType != -1) NavMesh.RemoveSettings(agentType);
        agentType = -1;
    }
    private void OnDestroy() { ReleaseNavigation(); }
}
