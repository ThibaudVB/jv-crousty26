using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Au lancement du jeu : prend tous les objets du layer "Roads" et génère
/// le NavMesh dessus. Rien d'autre à configurer : la NavMesh Surface
/// est créée et réglée automatiquement par ce script.
/// </summary>
[DefaultExecutionOrder(-1000)] // s'exécute avant tout le reste (les voitures y compris)
public class NavMeshAutoBaker : MonoBehaviour
{
    [Header("Réglages")]
    [Tooltip("Nom exact du layer des routes")]
    public string roadsLayerName = "Roads";

    [Tooltip("PhysicsColliders = utilise les colliders des routes. RenderMeshes = utilise leur mesh visible")]
    public NavMeshCollectGeometry geometry = NavMeshCollectGeometry.PhysicsColliders;

    [Tooltip("Coché = bake au lancement. Décoché = appelle Bake() toi-même après la génération de ta map")]
    public bool bakeOnAwake = true;

    [Header("Événement")]
    [Tooltip("Appelé quand le NavMesh est prêt (fais apparaître les voitures ici si besoin)")]
    public UnityEvent onBakeFinished;

    public bool IsBaked { get; private set; }

    private NavMeshSurface surface;
    private bool isBaking;

    private void Awake()
    {
        if (bakeOnAwake)
            Bake();
    }

    /// <summary>Génère le NavMesh sur tous les objets du layer Roads.</summary>
    public void Bake()
    {
        // Protection : si "On Bake Finished" rappelle Bake(), on bloque la boucle infinie
        if (isBaking)
        {
            Debug.LogWarning("NavMeshAutoBaker : Bake() rappelé pendant le bake, ignoré. " +
                             "Vérifie que 'On Bake Finished' n'appelle pas Bake().", this);
            return;
        }

        isBaking = true;
        try
        {
            BakeInternal();
        }
        finally
        {
            isBaking = false;
        }
    }

    private void BakeInternal()
    {
        int layer = LayerMask.NameToLayer(roadsLayerName);
        if (layer < 0)
        {
            Debug.LogError($"NavMeshAutoBaker : le layer '{roadsLayerName}' n'existe pas. " +
                           "Vérifie l'orthographe dans Edit > Project Settings > Tags and Layers.", this);
            return;
        }

        // Compte les objets trouvés (utile pour vérifier que le layer est bien appliqué)
        int found = 0;
        foreach (Transform t in FindObjectsByType<Transform>())
        {
            if (t.gameObject.layer != layer)
                continue;

            found++;

            bool hasCollider = t.GetComponent<Collider>() != null;
            bool hasMesh = t.GetComponent<MeshRenderer>() != null;
            Debug.Log($"NavMeshAutoBaker : objet trouvé '{t.name}' | position {t.position} | " +
                      $"collider : {hasCollider} | mesh : {hasMesh}", t);
        }

        if (found == 0)
        {
            Debug.LogError($"NavMeshAutoBaker : aucun objet sur le layer '{roadsLayerName}'. " +
                           "Mets ce layer sur tes routes.", this);
            return;
        }

        // Crée la NavMesh Surface si elle n'existe pas encore, puis la règle
        if (surface == null)
        {
            surface = GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = gameObject.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.All;
        surface.layerMask = 1 << layer;
        surface.useGeometry = geometry;

        surface.BuildNavMesh();

        // Vérifie ce qui a vraiment été généré
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0)
        {
            Debug.LogError("NavMeshAutoBaker : le NavMesh est VIDE (aucune surface générée). " +
                           "Les objets du layer n'ont probablement ni collider ni mesh utilisable.", this);
        }
        else
        {
            Bounds b = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (Vector3 v in tri.vertices)
                b.Encapsulate(v);

            Debug.Log($"NavMeshAutoBaker : NavMesh généré -> {tri.indices.Length / 3} triangles | " +
                      $"centre {b.center} | taille {b.size}", this);
        }

        IsBaked = true;
        Debug.Log($"NavMeshAutoBaker : NavMesh généré sur {found} objet(s) du layer '{roadsLayerName}'.", this);

        onBakeFinished?.Invoke();
    }
}