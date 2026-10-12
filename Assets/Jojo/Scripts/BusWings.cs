using UnityEngine;

// Ailes du bus pendant le superpouvoir de vol (touche F) :
// elles se déplient au décollage, battent pendant le vol (plus vite en montée,
// en panique quand le bus retombe) et se replient à l'atterrissage.
// Le modèle contient une seule aile (Resources/bus/CroustyAile) : elle est dupliquée en miroir pour l'autre côté.
// Ajouté automatiquement sur le bus par le DeliveryManager.
[DisallowMultipleComponent]
public class BusWings : MonoBehaviour
{
    [Tooltip("Modèle d'une aile. Si vide : Resources/bus/CroustyAile")]
    public GameObject wingModel;
    [Tooltip("Rotation de correction du modèle si l'aile n'est pas dans le bon sens")]
    public Vector3 modelRotation = Vector3.zero;

    [Header("Placement")]
    [Tooltip("Longueur d'une aile par rapport à la longueur du bus")]
    [Range(0.1f, 1f)] public float wingSize = 0.4f;
    [Tooltip("Position le long du bus (0 = milieu, 0.5 = avant, -0.5 = arrière)")]
    [Range(-0.5f, 0.5f)] public float alongBus = 0.1f;
    [Tooltip("Hauteur de la charnière (0 = milieu du bus, 0.5 = toit)")]
    [Range(-1.5f, 1.5f)] public float height = -0.15f;
    [Tooltip("Inclinaison des ailes vers l'arrière (degrés)")]
    public float sweepAngle = 15f;
    public Color wingColor = new Color(1f, 0.98f, 0.92f);

    [Header("Battement")]
    [Tooltip("Battements par seconde")]
    public float flapSpeed = 2.2f;
    [Tooltip("Amplitude du battement (degrés)")]
    public float flapAmplitude = 35f;
    [Tooltip("Angle moyen des ailes (0 = à plat, 90 = vers le haut)")]
    public float restAngle = 10f;
    [Tooltip("Les ailes battent plus vite quand le bus monte")]
    public float climbFlapMultiplier = 1.6f;
    [Tooltip("Battement en panique quand le vol est fini et que le bus retombe")]
    public float fallFlapMultiplier = 2.6f;
    [Tooltip("Durée du dépliage / repliage")]
    public float deploySeconds = 0.45f;

    private BusFlightController flight;
    private Rigidbody rb;
    private Transform root;
    private readonly Transform[] hinges = new Transform[2];
    private Material material;
    private float deploy;
    private float flapPhase;
    private float flapMultiplier = 1f;

    private void Start()
    {
        flight = GetComponent<BusFlightController>();
        rb = GetComponent<Rigidbody>();
        if (flight == null || !Build())
            enabled = false;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    private bool Build()
    {
        if (wingModel == null) wingModel = Resources.Load<GameObject>("bus/CroustyAile");
        MeshFilter source = wingModel != null ? wingModel.GetComponentInChildren<MeshFilter>(true) : null;
        if (source == null || source.sharedMesh == null)
        {
            Debug.LogWarning("BusWings : modèle d'aile introuvable (Resources/bus/CroustyAile).", this);
            return false;
        }

        Mesh mesh = source.sharedMesh;
        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        Material[] materials = BuildMaterials(sourceRenderer, mesh.subMeshCount);

        // Taille du bus en mètres, d'après sa boîte de collision
        BoxCollider body = GetComponent<BoxCollider>();
        Vector3 lossy = transform.lossyScale;
        lossy = new Vector3(Mathf.Max(0.01f, Mathf.Abs(lossy.x)), Mathf.Max(0.01f, Mathf.Abs(lossy.y)), Mathf.Max(0.01f, Mathf.Abs(lossy.z)));
        Vector3 center = body != null ? body.center : Vector3.Scale(Vector3.up * 1.5f, new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z));
        Vector3 size = body != null ? Vector3.Scale(body.size, lossy) : new Vector3(2.5f, 3f, 10f);

        // Racine en mètres : on compense l'échelle du bus
        root = new GameObject("Ailes").transform;
        root.SetParent(transform, false);
        root.localPosition = center;
        root.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        // L'aile s'étend le long de X dans le modèle, la charnière est à l'extrémité la plus proche de 0
        Bounds bounds = mesh.bounds;
        float spanSign = bounds.center.x >= 0f ? 1f : -1f;
        float scale = wingSize * size.z / Mathf.Max(0.01f, bounds.size.x);
        Vector3 hingePoint = new Vector3(spanSign > 0f ? bounds.min.x : bounds.max.x, bounds.min.y, bounds.center.z);
        Vector3 wingScale = new Vector3(scale * spanSign, scale, scale);
        Quaternion wingRotation = Quaternion.Euler(modelRotation);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? 1f : -1f;

            // Côté gauche = miroir du côté droit
            Transform sideRoot = new GameObject(i == 0 ? "Aile droite" : "Aile gauche").transform;
            sideRoot.SetParent(root, false);
            sideRoot.localPosition = new Vector3(side * size.x * 0.45f, size.y * height, size.z * alongBus);
            sideRoot.localScale = new Vector3(side, 1f, 1f);

            Transform hinge = new GameObject("Charniere").transform;
            hinge.SetParent(sideRoot, false);

            GameObject wing = new GameObject("Aile", typeof(MeshFilter), typeof(MeshRenderer));
            wing.transform.SetParent(hinge, false);
            wing.transform.localRotation = wingRotation;
            wing.transform.localScale = wingScale;
            // La charnière du modèle tombe pile sur le point de rotation
            wing.transform.localPosition = -(wingRotation * Vector3.Scale(wingScale, hingePoint));
            wing.GetComponent<MeshFilter>().sharedMesh = mesh;
            wing.GetComponent<MeshRenderer>().sharedMaterials = materials;

            hinges[i] = hinge;
        }

        root.gameObject.SetActive(false);
        return true;
    }

    // Matériau éclairé comme le bus, de la couleur des ailes, visible des deux côtés
    private Material[] BuildMaterials(MeshRenderer sourceRenderer, int count)
    {
        Material template = null;
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
        {
            Material candidate = renderer.sharedMaterial;
            // Pas de vitre ni de matériau transparent
            if (candidate != null && candidate.HasProperty("_BaseColor") && candidate.renderQueue < 2450)
            {
                template = candidate;
                break;
            }
        }

        if (template == null)
            return sourceRenderer != null ? sourceRenderer.sharedMaterials : new Material[0];

        material = new Material(template) { name = "Ailes bus" };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
        material.SetColor("_BaseColor", wingColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", wingColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;

        var materials = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        return materials;
    }

    private void LateUpdate()
    {
        if (root == null) return;

        bool airborne = flight.isActiveAndEnabled && flight.IsAirborne;
        deploy = Mathf.MoveTowards(deploy, airborne ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, deploySeconds));
        if (deploy <= 0f)
        {
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);

        // Battement : plus rapide en montée, en panique pendant la chute
        float target = flight.IsFalling ? fallFlapMultiplier
            : rb != null && rb.linearVelocity.y > 1f ? climbFlapMultiplier : 1f;
        flapMultiplier = Mathf.MoveTowards(flapMultiplier, target, Time.deltaTime * 3f);
        flapPhase += Time.deltaTime * flapSpeed * flapMultiplier * Mathf.PI * 2f;
        float flap = Mathf.Sin(flapPhase) * flapAmplitude;

        // Dépliage : les ailes sortent repliées le long du bus puis s'ouvrent avec un petit rebond
        float open = EaseOutBack(deploy);
        float angle = Mathf.LerpUnclamped(-80f, restAngle + flap, open);
        float grow = Mathf.Clamp01(deploy * 2f);

        foreach (Transform hinge in hinges)
        {
            hinge.localRotation = Quaternion.Euler(0f, sweepAngle, angle);
            hinge.localScale = Vector3.one * grow;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
