using UnityEngine;

// Ailes du bus pendant le vol (touche F, BusFlightController) :
// elles se déplient au décollage, battent pendant le vol (plus vite en montée,
// en panique quand le bus retombe) et se replient à l'atterrissage.
//
// A placer à la main sur le bus (le GameObject qui a le BusFlightController).
// Glisser le modèle d'UNE aile dans "Wing Model" : il est copié en miroir pour l'autre côté.
[DisallowMultipleComponent]
public class BusWings : MonoBehaviour
{
    [Header("Modèle")]
    [Tooltip("Modèle 3D d'une aile (obligatoire)")]
    public GameObject wingModel;
    [Tooltip("Rotation de correction si l'aile n'est pas dans le bon sens")]
    public Vector3 modelRotation = Vector3.zero;
    public Color wingColor = new Color(1f, 0.98f, 0.92f);

    [Header("Placement")]
    [Tooltip("Longueur d'une aile par rapport à la longueur du bus")]
    [Range(0.1f, 1f)] public float wingSize = 0.4f;
    [Tooltip("Position le long du bus (0 = milieu, 0.5 = avant, -0.5 = arrière)")]
    [Range(-0.5f, 0.5f)] public float alongBus = 0.1f;
    [Tooltip("Hauteur de la charnière (0 = milieu du bus, 0.5 = toit)")]
    [Range(-1.5f, 1.5f)] public float height = -0.15f;
    [Tooltip("Inclinaison des ailes vers l'arrière (degrés)")]
    public float sweepAngle = 15f;

    [Header("Battement")]
    [Tooltip("Battements par seconde")]
    public float flapSpeed = 2.2f;
    [Tooltip("Amplitude du battement (degrés)")]
    public float flapAmplitude = 35f;
    [Tooltip("Angle moyen des ailes (0 = à plat, 90 = vers le haut)")]
    public float restAngle = 10f;
    [Tooltip("Angle des ailes repliées (degrés)")]
    public float foldedAngle = -80f;
    [Tooltip("Les ailes battent plus vite quand le bus monte")]
    public float climbFlapMultiplier = 1.6f;
    [Tooltip("Battement en panique quand le vol est fini et que le bus retombe")]
    public float fallFlapMultiplier = 2.6f;
    [Tooltip("Durée du dépliage / repliage (s)")]
    public float deploySeconds = 0.45f;

    private BusFlightController flight;
    private Rigidbody rb;
    private Transform wingsRoot;
    private Transform[] hinges = new Transform[2]; // les charnières qui tournent (droite, gauche)
    private Material material;

    private float deploy;              // 0 = repliées, 1 = dépliées
    private float flapPhase;           // avancement du battement
    private float flapMultiplier = 1f;

    private void Start()
    {
        flight = GetComponent<BusFlightController>();
        rb = GetComponent<Rigidbody>();

        if (flight == null)
        {
            Debug.LogWarning("BusWings : il faut un BusFlightController sur le même objet.", this);
            enabled = false;
            return;
        }

        MeshFilter source = wingModel != null ? wingModel.GetComponentInChildren<MeshFilter>(true) : null;
        if (source == null || source.sharedMesh == null)
        {
            Debug.LogWarning("BusWings : glisser un modèle d'aile dans \"Wing Model\".", this);
            enabled = false;
            return;
        }

        CreateWings(source.sharedMesh);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }

    // ---------- CREATION ----------

    private void CreateWings(Mesh mesh)
    {
        material = CreateWingMaterial();

        // Taille du bus en mètres, d'après sa boîte de collision
        BoxCollider body = GetComponent<BoxCollider>();
        Vector3 busScale = transform.lossyScale;
        busScale = new Vector3(Mathf.Max(0.01f, Mathf.Abs(busScale.x)), Mathf.Max(0.01f, Mathf.Abs(busScale.y)), Mathf.Max(0.01f, Mathf.Abs(busScale.z)));
        Vector3 busSize = body != null ? Vector3.Scale(body.size, busScale) : new Vector3(2.5f, 3f, 10f);
        Vector3 busCenter = body != null ? body.center : Vector3.zero;

        // Objet racine qui annule l'échelle du bus : à l'intérieur, on travaille en mètres
        wingsRoot = new GameObject("Ailes").transform;
        wingsRoot.SetParent(transform, false);
        wingsRoot.localPosition = busCenter;
        wingsRoot.localScale = new Vector3(1f / busScale.x, 1f / busScale.y, 1f / busScale.z);

        // Dans le modèle, l'aile s'étend le long de X. La charnière est le bout le plus proche de 0.
        Bounds bounds = mesh.bounds;
        float direction = bounds.center.x >= 0f ? 1f : -1f;
        float scale = wingSize * busSize.z / Mathf.Max(0.01f, bounds.size.x);
        Vector3 hingePoint = new Vector3(direction > 0f ? bounds.min.x : bounds.max.x, bounds.min.y, bounds.center.z);
        Vector3 wingScale = new Vector3(scale * direction, scale, scale);
        Quaternion wingRotation = Quaternion.Euler(modelRotation);

        for (int i = 0; i < 2; i++)
        {
            // i = 0 : droite, i = 1 : gauche (une échelle X négative fait un miroir)
            float side = i == 0 ? 1f : -1f;

            Transform sideRoot = new GameObject(i == 0 ? "Aile droite" : "Aile gauche").transform;
            sideRoot.SetParent(wingsRoot, false);
            sideRoot.localPosition = new Vector3(side * busSize.x * 0.45f, busSize.y * height, busSize.z * alongBus);
            sideRoot.localScale = new Vector3(side, 1f, 1f);

            Transform hinge = new GameObject("Charniere").transform;
            hinge.SetParent(sideRoot, false);
            hinges[i] = hinge;

            GameObject wing = new GameObject("Aile", typeof(MeshFilter), typeof(MeshRenderer));
            wing.transform.SetParent(hinge, false);
            wing.transform.localRotation = wingRotation;
            wing.transform.localScale = wingScale;
            // On décale l'aile pour que sa charnière tombe pile sur le point de rotation
            wing.transform.localPosition = -(wingRotation * Vector3.Scale(wingScale, hingePoint));
            wing.GetComponent<MeshFilter>().sharedMesh = mesh;

            Material[] materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int m = 0; m < materials.Length; m++)
                materials[m] = material;
            wing.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        wingsRoot.gameObject.SetActive(false);
    }

    // Copie un matériau opaque du bus (même éclairage) avec la couleur des ailes
    private Material CreateWingMaterial()
    {
        Material template = null;
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
        {
            Material candidate = renderer.sharedMaterial;
            // On évite les vitres (matériaux transparents)
            if (candidate != null && candidate.HasProperty("_BaseColor") && candidate.renderQueue < 2450)
            {
                template = candidate;
                break;
            }
        }

        Material result = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        result.name = "Ailes bus";
        if (result.HasProperty("_BaseMap")) result.SetTexture("_BaseMap", null);
        result.SetColor("_BaseColor", wingColor);
        result.DisableKeyword("_EMISSION");
        if (result.HasProperty("_Cull")) result.SetFloat("_Cull", 0f); // visible des deux côtés
        return result;
    }

    // ---------- ANIMATION ----------

    // LateUpdate : après le mouvement du bus
    private void LateUpdate()
    {
        bool airborne = flight.isActiveAndEnabled && flight.IsAirborne;
        if (!airborne)
        {
            deploy = 0f;
            wingsRoot.gameObject.SetActive(false);
            return;
        }

        // deploy va doucement vers 1 en vol, vers 0 au sol
        float target = airborne ? 1f : 0f;
        deploy = Mathf.MoveTowards(deploy, target, Time.deltaTime / Mathf.Max(0.05f, deploySeconds));

        if (deploy <= 0f)
        {
            wingsRoot.gameObject.SetActive(false);
            return;
        }
        wingsRoot.gameObject.SetActive(true);

        // Vitesse du battement : normal, en montée, ou en panique pendant la chute
        float wantedMultiplier = 1f;
        if (flight.IsFalling)
            wantedMultiplier = fallFlapMultiplier;
        else if (rb != null && rb.linearVelocity.y > 1f)
            wantedMultiplier = climbFlapMultiplier;
        flapMultiplier = Mathf.MoveTowards(flapMultiplier, wantedMultiplier, Time.deltaTime * 3f);

        flapPhase += Time.deltaTime * flapSpeed * flapMultiplier * Mathf.PI * 2f;
        float flap = Mathf.Sin(flapPhase) * flapAmplitude;

        // Dépliage avec un petit rebond à la fin
        float open = EaseOutBack(deploy);
        float angle = Mathf.LerpUnclamped(foldedAngle, restAngle + flap, open);
        float grow = Mathf.Clamp01(deploy * 2f);

        foreach (Transform hinge in hinges)
        {
            hinge.localRotation = Quaternion.Euler(0f, sweepAngle, angle);
            hinge.localScale = Vector3.one * grow;
        }
    }

    // Courbe qui dépasse un peu 1 avant d'y revenir (effet de rebond)
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
