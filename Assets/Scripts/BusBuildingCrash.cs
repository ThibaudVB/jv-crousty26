using System.Collections.Generic;
using UnityEngine;

// Même principe que BusCarCrash, mais pour les deux grandes tours (Tour_Aerienne_Nord / Sud).
// On ne peut les exploser qu'en volant (pouvoir de vol actif).
// Ajouté tout seul sur le bus par BusCarCrash, pas besoin de le mettre dans la scène.
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(100)]
public class BusBuildingCrash : MonoBehaviour
{
    [Header("Détection")]
    [Tooltip("Début du nom des bâtiments destructibles (la ville procédurale préfixe ses objets par \"Visuel \")")]
    public string[] buildingNamePrefixes = { "Visuel Tour_Aerienne" };
    [Tooltip("Ce qui tombe avec les tours (la passerelle entre les deux)")]
    public string[] attachedNamePrefixes = { "Visuel Passerelle_Aerienne" };

    [Header("Chute des routes posées sur les tours")]
    [Tooltip("Ce qui tombe par terre quand la tour qui le porte explose")]
    public string[] fallingNamePrefixes = { "Visuel Route_Aerienne", "Visuel Rampe_Aerienne" };
    [Tooltip("Distance (m) autour de la tour pour considérer qu'une route est posée dessus")]
    [Min(0f)] public float supportMargin = 4f;
    [Tooltip("Masse des morceaux qui tombent (kg)")]
    [Min(1f)] public float fallingMass = 8000f;
    [Tooltip("Petite rotation au hasard pendant la chute (pour que ça ne tombe pas tout droit)")]
    [Min(0f)] public float fallTumble = 0.4f;
    [Tooltip("Les morceaux tombés disparaissent après ce temps (s). 0 = ils restent par terre")]
    [Min(0f)] public float debrisLifetime = 30f;

    [Header("Vitesse minimale pour exploser un bâtiment")]
    public float explosionSpeedKmh = 40f;

    [Header("Explosion")]
    [Tooltip("Si vide : reprend celle de BusCarCrash")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 5f;
    [Tooltip("Nombre d'explosions réparties sur la hauteur du bâtiment")]
    [Min(1)] public int explosionCount = 6;

    [Header("Debug")]
    [SerializeField] private float lastSpeedKmh;

    public int BuildingsExploded { get; private set; }

    private Rigidbody rb;
    private BusFlightController flight;
    private Vector3 velocityBeforeImpact;
    private Vector3 angularVelocityBeforeImpact;
    private BoxCollider busCollider;
    private readonly RaycastHit[] impactHits = new RaycastHit[32];
    private readonly HashSet<GameObject> destroyedBuildings = new HashSet<GameObject>();

    private bool IsFlying => flight != null && flight.CurrentMode == BusFlightController.FlightMode.Flying;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        busCollider = GetComponent<BoxCollider>();
        flight = GetComponent<BusFlightController>();
        if (explosionPrefab == null)
        {
            var carCrash = GetComponent<BusCarCrash>();
            if (carCrash != null) explosionPrefab = carCrash.explosionPrefab;
        }
    }

    private void FixedUpdate()
    {
        velocityBeforeImpact = rb.linearVelocity;
        angularVelocityBeforeImpact = rb.angularVelocity;
        // En vol on compte la vitesse complète (on peut foncer en piqué)
        lastSpeedKmh = velocityBeforeImpact.magnitude * 3.6f;
        if (!IsFlying || lastSpeedKmh < explosionSpeedKmh || busCollider == null) return;

        // Comme pour les voitures : on enlève le bâtiment juste avant le choc, le bus garde son élan.
        Vector3 scale = busCollider.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfSize = Vector3.Scale(busCollider.size * .5f, scale);
        int count = Physics.BoxCastNonAlloc(busCollider.transform.TransformPoint(busCollider.center),
            halfSize, velocityBeforeImpact.normalized, impactHits, busCollider.transform.rotation,
            velocityBeforeImpact.magnitude * Time.fixedDeltaTime + .05f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            TryDestroyBuilding(impactHits[i].collider);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Rattrapage si le balayage a raté le contact
        if (!TryDestroyBuilding(collision.collider)) return;
        rb.linearVelocity = velocityBeforeImpact;
        rb.angularVelocity = angularVelocityBeforeImpact;
    }

    private bool TryDestroyBuilding(Collider hit)
    {
        if (!IsFlying || lastSpeedKmh < explosionSpeedKmh) return false;
        GameObject building = FindBuilding(hit.transform, buildingNamePrefixes);
        if (building == null || !destroyedBuildings.Add(building)) return false;
        ExplodeBuilding(building);
        return true;
    }

    // Utilisé par BusDamage : pas de dégâts quand on traverse une tour qui explose
    public bool WillDestroy(Collider other)
    {
        if (!isActiveAndEnabled) return false;
        GameObject building = FindBuilding(other.transform, buildingNamePrefixes);
        if (building == null) return false;
        return destroyedBuildings.Contains(building) || IsFlying && lastSpeedKmh >= explosionSpeedKmh;
    }

    // Remonte la hiérarchie : le nom peut être sur le collider ou sur un parent
    private static GameObject FindBuilding(Transform t, string[] prefixes)
    {
        for (; t != null; t = t.parent)
            foreach (string prefix in prefixes)
                if (t.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return t.gameObject;
        return null;
    }

    private void ExplodeBuilding(GameObject building)
    {
        BuildingsExploded++;
        Bounds bounds = GetBounds(building);
        CroustySoundDesignManager.Play(CroustySound.Explosion, bounds.center);
        SpawnExplosions(bounds);

        // Les autres morceaux de la même tour (ex : "Tour_Aerienne_Nord" et "Tour_Aerienne_Nord_Mesh"),
        // et la passerelle accrochée aux tours, tombent avec elle
        string towerName = building.name.EndsWith("_Mesh") ? building.name.Substring(0, building.name.Length - 5) : building.name;
        Bounds support = bounds;
        support.Expand(supportMargin * 2f);
        if (building.transform.parent != null)
            foreach (Transform sibling in building.transform.parent)
            {
                if (sibling.gameObject == building) continue;
                // Route posée sur cette tour : elle tombe par terre
                if (FindBuilding(sibling, fallingNamePrefixes) == sibling.gameObject &&
                    GetBounds(sibling.gameObject).Intersects(support) && destroyedBuildings.Add(sibling.gameObject))
                {
                    MakeFall(sibling.gameObject);
                    continue;
                }
                bool samePiece = sibling.name.StartsWith(towerName, System.StringComparison.OrdinalIgnoreCase);
                bool attached = FindBuilding(sibling, attachedNamePrefixes) == sibling.gameObject;
                if (!samePiece && !attached || !destroyedBuildings.Add(sibling.gameObject)) continue;
                if (attached) SpawnExplosions(GetBounds(sibling.gameObject));
                Remove(sibling.gameObject);
            }

        Remove(building);
    }

    // La route garde son apparence mais devient un objet physique qui tombe.
    // Le collider exact de la ville ne peut pas tomber (MeshCollider non convexe) : on le remplace par une boîte.
    private void MakeFall(GameObject piece)
    {
        foreach (var meshCollider in piece.GetComponentsInChildren<MeshCollider>())
            Destroy(meshCollider);

        var filter = piece.GetComponent<MeshFilter>();
        var box = piece.AddComponent<BoxCollider>();
        if (filter != null && filter.sharedMesh != null)
        {
            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
        }

        var body = piece.AddComponent<Rigidbody>();
        body.mass = fallingMass;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.angularVelocity = Random.insideUnitSphere * fallTumble;

        if (debrisLifetime > 0f)
            Destroy(piece, debrisLifetime);
    }

    private void SpawnExplosions(Bounds bounds)
    {
        if (explosionPrefab == null) return;
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        float size = Mathf.Clamp(width / 10f, 1f, 6f);
        for (int i = 0; i < explosionCount; i++)
        {
            float t = explosionCount == 1 ? .5f : i / (float)(explosionCount - 1);
            Vector3 position = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, t), bounds.center.z);
            position += Random.insideUnitSphere * width * .25f;
            GameObject fx = Instantiate(explosionPrefab, position, Quaternion.identity);
            fx.transform.localScale *= size;
            Destroy(fx, explosionLifetime);
        }
    }

    private static Bounds GetBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void Remove(GameObject target)
    {
        foreach (Collider c in target.GetComponentsInChildren<Collider>())
            c.enabled = false;
        Destroy(target);
    }
}
