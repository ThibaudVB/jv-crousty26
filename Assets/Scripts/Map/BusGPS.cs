using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BusGPS : MonoBehaviour
{
    public DeliveryManager deliveries;
    [Tooltip("Racine des routes et obstacles. A renseigner apres generation procedurale.")]
    public Transform navigationRoot;
    [Range(0, 3)] public int startingCharges = 3;
    public int Charges { get; private set; }
    public DeliveryPoint Target { get; private set; }
    public readonly List<Vector3> Route = new List<Vector3>();
    public string Status { get; private set; } = "[G] GPS";
    private NavMeshData data;
    private NavMeshDataInstance instance;
    private int agentType = -1;
    private NavMeshQueryFilter filter;
    private NavMeshPath path;
    private float nextRepath;

    private void Awake()
    {
        path = new NavMeshPath();
        Charges = startingCharges;
        if (deliveries == null) deliveries = GetComponent<DeliveryManager>();
    }
    private void OnEnable() { if (deliveries != null) deliveries.DeliveryCompleted += OnDelivered; }
    private void OnDisable()
    {
        if (deliveries != null) deliveries.DeliveryCompleted -= OnDelivered;
        Target = null;
        Route.Clear();
    }
    // Appel par un futur pickup : un bonus recharge jusqu'a trois livraisons.
    public void GrantBonus() { Charges = 3; Status = "[G] GPS"; }

    // Appeler apres modification/remplacement de la carte procedurale.
    public void RebuildNavigation(Transform root)
    {
        ReleaseNavigation();
        navigationRoot = root;
        Target = null;
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
        if (navigationRoot == null) { Status = "GPS : carte indisponible"; return false; }
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(navigationRoot, ~0, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(s => s.component != null &&
            (s.component.GetComponentInParent<Rigidbody>() != null ||
             (s.component is Collider c && c.isTrigger)));
        foreach (var source in sources)
            if (source.sourceObject is Mesh mesh && !mesh.isReadable)
            {
                Status = "GPS : meshes non lisibles";
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
        if (data == null) { ReleaseNavigation(); Status = "GPS : navigation indisponible"; return false; }
        instance = NavMesh.AddNavMeshData(data);
        filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
        return true;
    }
    private bool Calculate(DeliveryPoint point)
    {
        if (deliveries == null || deliveries.player == null || point == null) return false;
        // Echantillonnage court : ne jamais accrocher un toit ou une rue de l'autre cote d'un mur.
        return NavMesh.SamplePosition(deliveries.player.position, out var start, 6f, filter) &&
            NavMesh.SamplePosition(point.transform.position, out var end, 4f, filter) &&
            NavMesh.CalculatePath(start.position, end.position, filter, path) &&
            path.status == NavMeshPathStatus.PathComplete;
    }
    public bool SelectNext()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || Charges <= 0 || deliveries == null) return false;
        if (Target != null && !Target.IsWaiting) return false; // Colis en vol.
        if (!EnsureNavigation()) return false;
        var candidates = new List<DeliveryPoint>();
        foreach (var point in deliveries.deliveryPoints)
            if (point != null && point.IsWaiting) candidates.Add(point);
        if (deliveries.player == null) return false;
        candidates.Sort((a,b) => (a.transform.position-deliveries.player.position).sqrMagnitude
            .CompareTo((b.transform.position-deliveries.player.position).sqrMagnitude));
        int index = candidates.IndexOf(Target);
        for (int i=1; i<=candidates.Count; i++)
        {
            var candidate = candidates[(index+i)%candidates.Count];
            if (!Calculate(candidate)) continue;
            Target = candidate;
            SetRoute();
            return true;
        }
        Target = null;
        Route.Clear();
        Status = candidates.Count == 0 ? "GPS : prends des commandes" : "GPS : aucune route accessible";
        return false;
    }
    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) SelectNext();
        if (Target == null || !Target.IsWaiting || Time.time < nextRepath) return;
        nextRepath = Time.time + 0.75f;
        if (Calculate(Target)) SetRoute();
        else { Route.Clear(); Status = "GPS : rejoins une route"; }
    }
    private void SetRoute()
    {
        Route.Clear();
        Route.AddRange(path.corners);
        float distance = 0;
        for (int i=1; i<Route.Count; i++) distance += Vector3.Distance(Route[i-1], Route[i]);
        Status = $"GPS : {distance:0} m  [G] changer";
    }
    private void OnDelivered(DeliveryPoint point)
    {
        if (point != Target) return;
        Charges = Mathf.Max(0, Charges-1);
        Target = null;
        Route.Clear();
        Status = Charges > 0 ? "[G] GPS : prochaine livraison" : "GPS epuise";
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
