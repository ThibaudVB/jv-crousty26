using System.Collections.Generic;
using UnityEngine;

// Dégâts du bus : au premier gros choc (mur, bâtiment, voiture ou grosse chute),
// le bus s'abîme : carrosserie noircie, fumée, flammes et vitesse maximale réduite.
// Le garage (BusGarageRepair) éteint le feu.
//
// A placer à la main sur le bus (le GameObject qui a le BusController et le Rigidbody).
// Pour choisir où sort le feu : créer des objets vides enfants du bus et les glisser dans "Fire Point".
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BusDamage : MonoBehaviour
{
    [Header("Chocs")]
    [Tooltip("Vitesse minimale contre l'obstacle (km/h) pour que le choc compte")]
    public float minImpactSpeedKmh = 25f;
    [Tooltip("Secondes minimum entre deux chocs comptés")]
    public float hitCooldown = 1.5f;
    [Tooltip("Les contacts plus bas que ça (bordures de trottoir) ne comptent pas (m)")]
    public float curbHeight = 0.6f;

    [Header("Chute")]
    [Tooltip("Hauteur de chute (m) à partir de laquelle l'atterrissage abîme le bus")]
    public float fallDamageHeight = 15f;

    [Header("Résistance")]
    [Tooltip("Nombre de chocs dommageables avant destruction. Le garage remet ce compteur à zéro.")]
    [Min(1)] public int hitsToDestroy = 10;

    [Header("Carrosserie")]
    [Tooltip("Noircissement de la carrosserie une fois abîmé (0 = rien, 1 = tout noir)")]
    [Range(0f, 1f)] public float darkening = 0.45f;
    public Color burntColor = new Color(0.08f, 0.07f, 0.07f);

    [Header("Feu")]
    [Tooltip("Le bus abîmé prend feu")]
    public bool onFire = true;
    [Tooltip("Objets enfants du bus qui marquent où sort le feu. Si vide : position ci-dessous.")]
    public Transform[] firePoint;
    [Tooltip("Position du feu par rapport au bus, si Fire Point est vide")]
    public Vector3 customFirePosition = new Vector3(0f, 1.5f, 2f);
    public Color fireColor = new Color(1f, 0.42f, 0.05f);
    public Color fireCoreColor = new Color(1f, 0.85f, 0.2f);
    public float fireLightIntensity = 4f;
    public float fireLightRange = 10f;

    [Header("Fumée")]
    public Color smokeColor = new Color(0.32f, 0.32f, 0.35f);
    [Tooltip("Bouffées de fumée par seconde quand le bus est abîmé")]
    public float smokePerSecond = 7f;
    [Tooltip("Nombre de bouffées créées au départ et réutilisées")]
    [Min(1)] public int smokePuffCount = 30;

    // Infos pour les autres scripts
    public bool IsDamaged { get; private set; }
    public bool DamageEnabled { get; private set; } = true;
    public int HitsTaken { get; private set; }
    public float DamagePenalty { get; private set; }
    public float SpeedMultiplier => isActiveAndEnabled ? 1f - DamagePenalty : 1f;
    public event System.Action Damaged; // appelé à chaque choc (le gérant réagit)
    public event System.Action Repaired;

    // Composants du bus
    private Rigidbody rb;
    private BoxCollider body;
    private BusWheelRig wheelRig;
    private BusFlightController flight;
    private PoliceCatchZone catchZone;

    // Chocs et chute
    private float lastHitTime = -999f;
    private float highestY;
    private bool wasGrounded = true;

    // Couleurs d'origine de la carrosserie
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Color> originalColors = new List<Color>();

    // Feu : un groupe de flammes par emplacement
    private readonly List<GameObject> fires = new List<GameObject>();
    private readonly List<Transform> flames = new List<Transform>();
    private readonly List<Vector3> flameScales = new List<Vector3>();
    private readonly List<Light> fireLights = new List<Light>();

    // Fumée : des sphères réutilisées (plus rapide que d'en créer sans arrêt)
    private class Puff
    {
        public Transform transform;
        public Vector3 velocity;
        public float age, life, size;
    }
    private readonly List<Puff> puffs = new List<Puff>();
    private Transform smokeRoot;
    private int nextPuff;
    private float puffTimer;
    private bool firesVisible;
    private bool extinguished; // éteint par le garage

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        body = GetComponent<BoxCollider>();
        wheelRig = GetComponent<BusWheelRig>();
        flight = GetComponent<BusFlightController>();
        catchZone = GetComponentInChildren<PoliceCatchZone>(true);
    }

    private void Start()
    {
        highestY = rb.position.y;
        SaveOriginalColors();
        CreateFires();
        CreateSmoke();
    }

    private void OnDestroy()
    {
        foreach (Material material in materials)
            if (material != null) Destroy(material);
        if (smokeRoot != null)
            Destroy(smokeRoot.gameObject);
    }

    private void Update()
    {
        UpdateSmoke();
        UpdateFlames();
    }

    // Appelé par le garage
    public void ExtinguishFire()
    {
        bool wasDamaged = IsDamaged;
        IsDamaged = false;
        HitsTaken = 0;
        DamagePenalty = 0f;
        lastHitTime = Time.time;
        for (int i = 0; i < materials.Count; i++)
            materials[i].SetColor("_BaseColor", originalColors[i]);
        extinguished = true;
        ShowFires(false);
        foreach (Puff puff in puffs)
            puff.transform.gameObject.SetActive(false);
        if (wasDamaged)
            Repaired?.Invoke();
    }

    public void ApplyTutorialImpact()
    {
        if (IsDamaged)
            return;

        lastHitTime = -999f;
        TakeHit(transform.position);
    }

    public void SetDamageEnabled(bool damageEnabled)
    {
        DamageEnabled = damageEnabled;
    }

    // ---------- CHOCS ----------

    private void OnCollisionEnter(Collision collision)
    {
        if (!CanTakeHit() || collision.transform.IsChildOf(transform))
            return;
        if (TryRoadblockHit(collision)) return;
        if (LandedTrick()) return;
        var garage = GetComponent<BusGarageRepair>();
        if (garage != null && garage.IsGarage(collision.collider.transform)) return;
        var police = collision.collider.GetComponentInParent<PoliceCarAI>();
        var crash = GetComponent<BusCarCrash>();
        if (police != null)
        {
            if (police.IsDestroyed || crash != null && crash.TryResolvePoliceCollision(collision)) return;
            // Police only inflict damage with an actual rear-end pursuit impact.
            if (!police.IsRearRamCollision(collision)) return;
            TakeHit(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
            if (Camera.main != null && Camera.main.TryGetComponent<BusCameraFollow>(out var follow))
                follow.ShakePoliceImpact();
            return;
        }
        if (crash != null && crash.WillDestroy(collision.collider)) return;
        var buildingCrash = GetComponent<BusBuildingCrash>();
        if (buildingCrash != null && buildingCrash.WillDestroy(collision.collider)) return;

        float floorY = (body != null ? body.bounds.min.y : rb.position.y) + curbHeight;
        float strongestImpact = 0f;
        Vector3 impactPoint = transform.position;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            // Sol, toit ou bordure de trottoir : ce n'est pas un choc
            bool isFloorOrRoof = Mathf.Abs(contact.normal.y) > 0.6f;
            bool isCurb = contact.point.y < floorY;
            if (isFloorOrRoof || isCurb)
                continue;

            // Vitesse vers l'obstacle (m/s)
            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (impact > strongestImpact)
            {
                strongestImpact = impact;
                impactPoint = contact.point;
            }
        }

        // m/s -> km/h : x 3.6
        if (strongestImpact * 3.6f >= minImpactSpeedKmh)
            TakeHit(impactPoint);
    }

    // Figure réussie à l'atterrissage (BusAirTricks) : pas de dégâts
    private bool LandedTrick()
    {
        var tricks = GetComponent<BusAirTricks>();
        return tricks != null && tricks.ProtectsLanding;
    }

    private bool CanTakeHit()
    {
        if (!DamageEnabled)
            return false;

        bool cooldownOver = Time.time - lastHitTime >= hitCooldown;
        bool gameLost = DeliveryManager.Instance != null && DeliveryManager.Instance.HasFailed;
        return cooldownOver && HitsTaken < Mathf.Max(1, hitsToDestroy) && !gameLost && (catchZone == null || !catchZone.IsCaught);
    }

    private bool TryRoadblockHit(Collision collision)
    {
        if (collision.collider.GetComponentInParent<PoliceRoadblock>() == null) return false;
        // Even nudging or pushing a barricade costs health. Shared hitCooldown prevents
        // multiple colliders from charging several hits in the same physics step.
        TakeHit(collision.contactCount > 0 ? collision.GetContact(0).point : rb.position);
        return true;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (CanTakeHit()) TryRoadblockHit(collision);
    }

    public void TakePoliceProjectileHit(Vector3 point)
    {
        if (!CanTakeHit()) return;
        TakeHit(point);
        if (Camera.main != null && Camera.main.TryGetComponent<BusCameraFollow>(out var follow))
            follow.ShakePoliceImpact();
    }

    private void TakeHit(Vector3 point)
    {
        if (!CanTakeHit()) return;
        lastHitTime = Time.time;
        HitsTaken++;
        // 2.5 percent per hit with the default ten-hit resistance; never more than 25%.
        DamagePenalty = .25f * Mathf.Clamp01((float)HitsTaken / Mathf.Max(1, hitsToDestroy));
        IsDamaged = true;
        DarkenBody();

        extinguished = false;
        if (onFire)
            ShowFires(true);

        Damaged?.Invoke();
        CroustySoundDesignManager.Play(CroustySound.Horn, point);

        // Petite bouffée de fumée à l'endroit du choc
        for (int i = 0; i < 6; i++)
            EmitPuff(point + Random.insideUnitSphere * 0.5f, 0.5f, 0.9f);

        if (HitsTaken >= Mathf.Max(1, hitsToDestroy) && catchZone != null)
            catchZone.SetProgress(1f);
    }

    // ---------- CHUTE ----------

    private void FixedUpdate()
    {
        float y = rb.position.y;
        bool grounded = IsGrounded();
        bool flying = flight != null && flight.isActiveAndEnabled &&
            flight.CurrentMode == BusFlightController.FlightMode.Flying;

        if (flying)
        {
            // Pendant le vol piloté, la chute ne compte qu'à partir de la fin du vol
            highestY = y;
        }
        else if (!grounded)
        {
            // En l'air : on retient le point le plus haut
            highestY = Mathf.Max(highestY, y);
        }
        else
        {
            // On vient d'atterrir : la chute était-elle trop haute ?
            bool justLanded = !wasGrounded;
            if (justLanded && highestY - y >= fallDamageHeight && CanTakeHit() && !LandedTrick())
                TakeHit(transform.position);
            highestY = y;
        }

        wasGrounded = grounded;
    }

    private bool IsGrounded()
    {
        if (wheelRig != null && wheelRig.IsReady && wheelRig.IsGrounded)
            return true;

        // Rayon vers le bas depuis le centre du bus, jusqu'un peu sous la carrosserie
        Vector3 origin = body != null ? body.bounds.center : rb.position + Vector3.up;
        float length = (body != null ? origin.y - body.bounds.min.y : 1f) + 0.5f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, length, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            bool isBus = hit.collider.transform.IsChildOf(transform);
            bool isFloor = hit.normal.y > 0.5f;
            if (!isBus && isFloor)
                return true;
        }
        return false;
    }

    // ---------- CARROSSERIE ----------

    private void SaveOriginalColors()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;

            // renderer.materials crée une copie propre au bus : on peut la modifier sans toucher aux autres objets
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    materials.Add(material);
                    originalColors.Add(material.GetColor("_BaseColor"));
                }
            }
        }
    }

    private void DarkenBody()
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Color color = Color.Lerp(originalColors[i], burntColor,
                Mathf.Lerp(darkening, .8f, Mathf.InverseLerp(.075f, .25f, DamagePenalty)));
            color.a = originalColors[i].a; // garde la transparence (vitres)
            materials[i].SetColor("_BaseColor", color);
        }
    }

    // ---------- FEU ----------

    private void CreateFires()
    {
        // Le feu est accroché à la carrosserie animée (créée par BusWheelRig) :
        // il suit les figures en l'air (galipettes, 360) au lieu de rester sur le bus physique, qui reste droit.
        Transform body = transform.Find("Carrosserie - Animation arcade");
        if (body == null)
            body = transform;

        bool hasPoint = false;
        if (firePoint != null)
        {
            foreach (Transform point in firePoint)
            {
                if (point != null)
                {
                    if (point.parent == transform && body != transform)
                        point.SetParent(body, true); // même place à l'écran, mais suit la carrosserie
                    CreateFire(point, Vector3.zero);
                    hasPoint = true;
                }
            }
        }

        if (!hasPoint)
            CreateFire(body, body.InverseTransformPoint(transform.TransformPoint(customFirePosition)));
    }

    private void CreateFire(Transform parent, Vector3 localPosition)
    {
        GameObject fire = new GameObject("Feu bus");
        fire.transform.SetParent(parent, false);
        fire.transform.localPosition = localPosition;

        // Compense l'échelle du parent pour que les flammes gardent leur taille en mètres
        Vector3 parentScale = parent.lossyScale;
        fire.transform.localScale = new Vector3(
            1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
            1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
            1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));

        // 3 grandes flammes orange autour, 2 petites jaunes au centre
        for (int i = 0; i < 5; i++)
        {
            bool isCore = i >= 3;
            Color color = isCore ? fireCoreColor : fireColor;
            GameObject flame = JojoVisuals.CreateShape(PrimitiveType.Cube, fire.transform, color);
            flame.name = "Flamme";

            float angle = i * 2.4f;
            if (isCore)
                flame.transform.localPosition = new Vector3((i - 3.5f) * 0.3f, 0.3f, 0f);
            else
                flame.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.5f, 0.4f, Mathf.Sin(angle) * 0.4f);

            flame.transform.localRotation = Quaternion.Euler(0f, 45f + i * 20f, 0f);
            Vector3 scale = isCore ? new Vector3(0.35f, 0.9f, 0.35f) : new Vector3(0.6f, 1.3f, 0.6f);
            flame.transform.localScale = scale;

            flames.Add(flame.transform);
            flameScales.Add(scale);
        }

        GameObject lightObject = new GameObject("Lumiere feu");
        lightObject.transform.SetParent(fire.transform, false);
        lightObject.transform.localPosition = Vector3.up;
        Light fireLight = lightObject.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = fireColor;
        fireLight.range = fireLightRange;
        fireLight.intensity = fireLightIntensity;
        fireLights.Add(fireLight);

        fire.SetActive(false);
        fires.Add(fire);
    }

    private void UpdateFlames()
    {
        if (!firesVisible)
            return;

        // Les flammes grandissent et rétrécissent au hasard (bruit de Perlin = hasard "doux")
        for (int i = 0; i < flames.Count; i++)
        {
            float flicker = 0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 7f, i * 3.1f);
            Vector3 scale = flameScales[i];
            scale.y *= flicker;
            flames[i].localScale = scale;

            // Garde le bas de la flamme posé au même endroit
            Vector3 position = flames[i].localPosition;
            position.y = scale.y * 0.5f;
            flames[i].localPosition = position;
        }

        for (int i = 0; i < fireLights.Count; i++)
        {
            float flicker = 0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 9f, i * 0.5f);
            fireLights[i].intensity = fireLightIntensity * flicker;
        }
    }

    private void ShowFires(bool visible)
    {
        firesVisible = visible;
        foreach (GameObject fire in fires)
            fire.SetActive(visible);
    }

    // ---------- FUMEE ----------

    private void CreateSmoke()
    {
        // Les bouffées ne sont pas enfants du bus : elles restent sur place et font une traînée
        smokeRoot = new GameObject("Fumee bus").transform;

        for (int i = 0; i < smokePuffCount; i++)
        {
            Color color = i % 2 == 0 ? smokeColor : smokeColor * 0.7f;
            GameObject puff = JojoVisuals.CreateShape(PrimitiveType.Sphere, smokeRoot, color);
            puff.SetActive(false);
            puffs.Add(new Puff { transform = puff.transform });
        }
    }

    // Lance une bouffée (on réutilise la plus ancienne)
    private void EmitPuff(Vector3 position, float minSize, float maxSize)
    {
        if (puffs.Count == 0) return;
        Puff puff = puffs[nextPuff];
        nextPuff = (nextPuff + 1) % puffs.Count;

        puff.transform.position = position;
        puff.transform.localScale = Vector3.zero;
        puff.velocity = Vector3.up * Random.Range(1.5f, 2.8f) + Random.insideUnitSphere * 0.5f;
        puff.age = 0f;
        puff.life = Random.Range(1.3f, 2f);
        puff.size = Random.Range(minSize, maxSize);
        puff.transform.gameObject.SetActive(true);
    }

    private void UpdateSmoke()
    {
        // Le bus abîmé fume en continu depuis chaque emplacement du feu
        if (IsDamaged && !extinguished)
        {
            puffTimer += smokePerSecond * Mathf.Lerp(1f, 2.5f, DamagePenalty / .25f) * Time.deltaTime;
            while (puffTimer >= 1f)
            {
                puffTimer -= 1f;
                foreach (GameObject fire in fires)
                    EmitPuff(fire.transform.position + Random.insideUnitSphere * 0.4f, 0.5f, 1.1f);
            }
        }

        // Chaque bouffée monte, grossit puis disparaît
        foreach (Puff puff in puffs)
        {
            if (!puff.transform.gameObject.activeSelf)
                continue;

            puff.age += Time.deltaTime;
            float progress = puff.age / puff.life; // 0 = naissance, 1 = fin
            if (progress >= 1f)
            {
                puff.transform.gameObject.SetActive(false);
                continue;
            }

            puff.transform.position += puff.velocity * Time.deltaTime;
            puff.transform.localScale = Vector3.one * (puff.size * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, progress)));
        }
    }

    // Montre les emplacements du feu dans la vue Scene quand le bus est sélectionné
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = fireColor;
        bool hasPoint = false;
        if (firePoint != null)
        {
            foreach (Transform point in firePoint)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.4f);
                    hasPoint = true;
                }
            }
        }

        if (!hasPoint)
            Gizmos.DrawWireSphere(transform.TransformPoint(customFirePosition), 0.4f);
    }
}
