using System.Collections.Generic;
using UnityEngine;

// Dégâts du bus : au premier choc contre un mur, un bâtiment ou une voiture, le bus s'abîme
// (carrosserie noircie, fumée et flammes sur le capot). C'est purement visuel : on ne perd pas.
// Une grosse chute (après un vol trop haut, ou en tombant d'un toit) compte aussi comme un choc.
// Ajouté automatiquement sur le bus par le DeliveryManager, rien à placer dans la scène.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusDamage : MonoBehaviour
{
    [Header("Chocs")]
    [Tooltip("Vitesse minimale vers l'obstacle (km/h) pour que le choc compte")]
    public float minImpactSpeedKmh = 25f;
    [Tooltip("Secondes entre deux chocs comptés")]
    public float hitCooldown = 1.5f;
    [Tooltip("Les contacts plus bas que ça (bordures de trottoir) ne comptent pas")]
    public float curbHeight = 0.6f;

    [Header("Chute")]
    [Tooltip("Hauteur de chute (m) à partir de laquelle l'atterrissage abîme le bus")]
    public float fallDamageHeight = 15f;

    [Header("Vitesse")]
    [Tooltip("Le bus roule un peu moins vite que la vitesse réglée sur le BusController")]
    [Range(0.3f, 1f)] public float speedMultiplier = 0.85f;

    [Header("Effets")]
    [Tooltip("Noircissement de la carrosserie une fois abîmé")]
    [Range(0f, 1f)] public float darkening = 0.45f;
    [Tooltip("Le bus abîmé a aussi des flammes sur le capot")]
    public bool onFire = true;
    public Color smokeColor = new Color(0.32f, 0.32f, 0.35f);
    public Color fireColor = new Color(1f, 0.42f, 0.05f);
    public Color fireCoreColor = new Color(1f, 0.85f, 0.2f);

    public bool IsDamaged { get; private set; }
    // Appelé à chaque choc
    public event System.Action Damaged;

    private Rigidbody rb;
    private BoxCollider body;
    private float lastHitTime = -999f;
    private BusWheelRig wheelRig;
    private BusFlightController flight;
    private float fallStartY;
    private bool wasGrounded = true;

    // Carrosserie : matériaux du bus et leur couleur d'origine
    private class Paint { public Material material; public string property; public Color color; }
    private readonly List<Paint> paints = new List<Paint>();
    private readonly List<Material> busMaterials = new List<Material>();

    // Point du moteur (repère local du bus) d'où sortent fumée et flammes
    private Vector3 enginePoint;
    private Transform fire;
    private Light fireLight;
    private readonly List<Transform> flames = new List<Transform>();
    private readonly List<Vector3> flameScales = new List<Vector3>();

    private class Puff { public Transform t; public Vector3 velocity; public float age, life, size; }
    private readonly List<Puff> puffs = new List<Puff>();
    private Transform smokeRoot;
    private int nextPuff;
    private float puffTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        body = GetComponent<BoxCollider>();
        wheelRig = GetComponent<BusWheelRig>();
        flight = GetComponent<BusFlightController>();
    }

    private void Start()
    {
        // Start : après les Awake qui peuvent encore régler la vitesse du bus
        BusController bus = GetComponent<BusController>();
        if (bus != null)
            bus.maxSpeedKmh *= speedMultiplier;

        fallStartY = rb.position.y;
        CollectPaint();
        BuildEffects();
    }

    private void OnDestroy()
    {
        foreach (Material material in busMaterials)
            if (material != null) Destroy(material);
        if (smokeRoot != null) Destroy(smokeRoot.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!CanTakeHit() || collision.transform.IsChildOf(transform)) return;

        float floor = (body != null ? body.bounds.min.y : rb.position.y) + curbHeight;
        float impact = 0f;
        Vector3 point = transform.position;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            // Sol, toit ou bordure : pas un choc
            if (Mathf.Abs(contact.normal.y) > 0.6f || contact.point.y < floor) continue;

            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed > impact)
            {
                impact = speed;
                point = contact.point;
            }
        }

        if (impact * 3.6f >= minImpactSpeedKmh)
            TakeHit(point);
    }

    private bool CanTakeHit()
    {
        return Time.time - lastHitTime >= hitCooldown &&
            (DeliveryManager.Instance == null || !DeliveryManager.Instance.HasFailed);
    }

    // ---------- Chute ----------

    private void FixedUpdate()
    {
        float y = rb.position.y;
        bool grounded = IsGrounded();

        // Pendant le vol piloté, la chute ne commence qu'à la fin du vol (quand le bus retombe)
        if (flight != null && flight.isActiveAndEnabled && flight.CurrentMode == BusFlightController.FlightMode.Flying)
            fallStartY = y;
        else if (!grounded)
            fallStartY = Mathf.Max(fallStartY, y);
        else
        {
            // Atterrissage : on compare avec le point le plus haut atteint en l'air
            if (!wasGrounded && fallStartY - y >= fallDamageHeight && CanTakeHit())
                TakeHit(transform.position);
            fallStartY = y;
        }

        wasGrounded = grounded;
    }

    private bool IsGrounded()
    {
        if (wheelRig != null && wheelRig.IsReady && wheelRig.IsGrounded)
            return true;

        // Rayon vers le bas depuis le centre du bus jusqu'un peu sous le bas de la carrosserie
        Vector3 origin = body != null ? body.bounds.center : rb.position + Vector3.up;
        float distance = (body != null ? origin.y - body.bounds.min.y : 1f) + 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
            if (!hit.collider.transform.IsChildOf(transform) && hit.normal.y > 0.5f)
                return true;
        return false;
    }

    private void TakeHit(Vector3 point)
    {
        lastHitTime = Time.time;

        if (!IsDamaged)
        {
            IsDamaged = true;
            ApplyPaint();
            if (onFire && fire != null)
                fire.gameObject.SetActive(true);
        }

        Damaged?.Invoke();

        CroustySoundDesignManager.Play(CroustySound.Horn, point);
        // Petite bouffée de fumée à l'endroit du choc
        for (int i = 0; i < 6; i++)
            EmitPuff(point + Random.insideUnitSphere * 0.5f, 0.5f, 0.9f);
    }

    // ---------- Carrosserie ----------

    private void CollectPaint()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

            foreach (Material material in renderer.materials)
            {
                busMaterials.Add(material);
                string property = material.HasProperty("_BaseColor") ? "_BaseColor"
                    : material.HasProperty("_Color") ? "_Color" : null;
                if (property != null)
                    paints.Add(new Paint { material = material, property = property, color = material.GetColor(property) });
            }
        }
    }

    private void ApplyPaint()
    {
        Color burnt = new Color(0.08f, 0.07f, 0.07f);
        foreach (Paint paint in paints)
        {
            Color color = Color.Lerp(paint.color, burnt, darkening);
            color.a = paint.color.a;
            paint.material.SetColor(paint.property, color);
        }
    }

    // ---------- Fumée et flammes ----------

    private void BuildEffects()
    {
        // Sur le capot, à l'avant du bus (le bus avance vers +Z)
        Vector3 center = Vector3.up, size = new Vector3(2f, 2f, 6f);
        if (body != null)
        {
            center = body.center;
            size = body.size;
        }
        enginePoint = center + new Vector3(0f, size.y * 0.5f, size.z * 0.3f);

        // Les flammes suivent le bus. On compense son échelle pour garder des tailles en mètres.
        fire = new GameObject("Feu bus").transform;
        fire.SetParent(transform, false);
        fire.localPosition = enginePoint;
        Vector3 scale = transform.lossyScale;
        fire.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.01f, Mathf.Abs(scale.y)), 1f / Mathf.Max(0.01f, Mathf.Abs(scale.z)));

        for (int i = 0; i < 5; i++)
        {
            bool core = i >= 3;
            GameObject flame = JojoVisuals.CreateShape(PrimitiveType.Cube, fire, core ? fireCoreColor : fireColor);
            flame.name = "Flamme";
            float angle = i * 2.4f;
            flame.transform.localPosition = core
                ? new Vector3((i - 3.5f) * 0.3f, 0.3f, 0f)
                : new Vector3(Mathf.Cos(angle) * 0.5f, 0.4f, Mathf.Sin(angle) * 0.4f);
            flame.transform.localRotation = Quaternion.Euler(0f, 45f + i * 20f, 0f);
            Vector3 flameScale = core ? new Vector3(0.35f, 0.9f, 0.35f) : new Vector3(0.6f, 1.3f, 0.6f);
            flame.transform.localScale = flameScale;
            flames.Add(flame.transform);
            flameScales.Add(flameScale);
        }

        fireLight = new GameObject("Lumiere feu").AddComponent<Light>();
        fireLight.transform.SetParent(fire, false);
        fireLight.transform.localPosition = Vector3.up;
        fireLight.type = LightType.Point;
        fireLight.color = fireColor;
        fireLight.range = 10f;
        fireLight.intensity = 4f;

        fire.gameObject.SetActive(false);

        // Fumée : quelques sphères réutilisées, dans le monde pour laisser une traînée
        smokeRoot = new GameObject("Fumee bus").transform;
        effectRoots.Add(smokeRoot);
        for (int i = 0; i < 30; i++)
        {
            GameObject puff = JojoVisuals.CreateShape(PrimitiveType.Sphere, smokeRoot,
                i % 2 == 0 ? smokeColor : smokeColor * 0.7f);
            puff.SetActive(false);
            puffs.Add(new Puff { t = puff.transform });
        }
    }

    private void EmitPuff(Vector3 position, float minSize, float maxSize)
    {
        if (puffs.Count == 0) return;
        Puff puff = puffs[nextPuff];
        nextPuff = (nextPuff + 1) % puffs.Count;

        puff.t.position = position;
        puff.velocity = Vector3.up * Random.Range(1.5f, 2.8f) + Random.insideUnitSphere * 0.5f;
        puff.age = 0f;
        puff.life = Random.Range(1.3f, 2f);
        puff.size = Random.Range(minSize, maxSize);
        puff.t.localScale = Vector3.zero;
        puff.t.gameObject.SetActive(true);
    }

    private void Update()
    {
        UpdateSmoke();
        UpdateFire();
    }

    private void UpdateSmoke()
    {
        // Le bus abîmé fume en continu
        if (IsDamaged)
        {
            puffTimer += 7f * Time.deltaTime;
            Vector3 engine = transform.TransformPoint(enginePoint);
            while (puffTimer >= 1f)
            {
                puffTimer -= 1f;
                EmitPuff(engine + Random.insideUnitSphere * 0.4f, 0.5f, 1.1f);
            }
        }

        foreach (Puff puff in puffs)
        {
            if (!puff.t.gameObject.activeSelf) continue;
            puff.age += Time.deltaTime;
            float a = puff.age / puff.life;
            if (a >= 1f)
            {
                puff.t.gameObject.SetActive(false);
                continue;
            }
            puff.t.position += puff.velocity * Time.deltaTime;
            // Grossit puis disparaît
            puff.t.localScale = Vector3.one * (puff.size * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, a)));
        }
    }

    private void UpdateFire()
    {
        if (fire == null || !fire.gameObject.activeSelf) return;

        for (int i = 0; i < flames.Count; i++)
        {
            float flicker = 0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 7f, i * 3.1f);
            Vector3 s = flameScales[i];
            s.y *= flicker;
            flames[i].localScale = s;
            Vector3 p = flames[i].localPosition;
            p.y = s.y * 0.5f;
            flames[i].localPosition = p;
        }

        fireLight.intensity = 3f + 2f * Mathf.PerlinNoise(Time.time * 9f, 0.5f);
    }
}
