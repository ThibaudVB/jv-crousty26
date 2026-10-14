using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PoliceAirProjectile : MonoBehaviour
{
    private bool burningDumpster, landed, exploded;
    private Rigidbody body, bus;
    private GameObject explosionPrefab, warning;
    private Material warningMaterial, fireMaterial;
    private Texture2D fireTexture;
    private float explodeAt = float.PositiveInfinity, expiresAt;
    private const float BlastRadius = 4.5f;

    public void Initialize(GameObject model, bool dumpster, Rigidbody player)
    {
        burningDumpster = dumpster; bus = player;
        var crash = player.GetComponent<BusCarCrash>();
        explosionPrefab = crash != null ? crash.explosionPrefab : null;
        float size = dumpster ? 4.8f : 1.8f;
        PoliceModelVisual.Create(model, transform, new Vector3(size, 0f, 0f));
        var collider = gameObject.AddComponent<BoxCollider>();
        collider.size = dumpster ? new Vector3(4.4f, 3.4f, 3.4f) : Vector3.one * 1.4f;
        collider.center = Vector3.up * collider.size.y * .5f;
        body = GetComponent<Rigidbody>(); body.mass = dumpster ? 180f : 15f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = Vector3.down * 16f;
        expiresAt = Time.time + 20f;
        CreateWarning();
        if (dumpster) CreateFire();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (DeliveryManager.Instance != null && DeliveryManager.Instance.HasFailed)
        { Destroy(gameObject); return; }
        if (Time.time >= explodeAt) Explode();
        else if (Time.time >= expiresAt) Destroy(gameObject);
        if (warning != null)
        {
            float pulse = .7f + Mathf.Sin(Time.time * 12f) * .25f;
            warningMaterial.color = new Color(1f, pulse * .25f, .04f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded) return;
        var damage = collision.collider.GetComponentInParent<BusDamage>();
        if (!burningDumpster && damage != null) { Explode(); return; }
        if (burningDumpster && damage != null) damage.TakePoliceProjectileHit(transform.position);
        if (landed) return;
        bool ground = false;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > .45f) ground = true;
        if (!ground) { if (!burningDumpster) explodeAt = Time.time + .2f; return; }
        landed = true;
        if (burningDumpster)
        {
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            expiresAt = Time.time + 12f;
            if (warning != null) Destroy(warning);
        }
        else
        {
            float fuse = .8f;
            if (bus != null)
            {
                Vector3 velocity = Vector3.ProjectOnPlane(bus.linearVelocity, Vector3.up);
                if (velocity.magnitude > 3f)
                    fuse = Mathf.Clamp(Vector3.Dot(transform.position - bus.position, velocity.normalized) / velocity.magnitude, .15f, 2.2f);
            }
            explodeAt = Time.time + fuse;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!burningDumpster) return;
        var damage = collision.collider.GetComponentInParent<BusDamage>();
        if (damage != null) damage.TakePoliceProjectileHit(transform.position);
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        if (explosionPrefab != null)
            Destroy(Instantiate(explosionPrefab, transform.position, Quaternion.identity), 5f);
        CroustySoundDesignManager.Play(CroustySound.Explosion, transform.position);
        if (bus != null)
        {
            var collider = bus.GetComponent<BoxCollider>();
            Vector3 closest = collider != null ? collider.ClosestPoint(transform.position) : bus.position;
            if (Vector3.Distance(closest, transform.position) <= BlastRadius)
            {
                // A building can shelter the bus from a blast.
                bool blocked = Physics.Linecast(transform.position + Vector3.up * .5f,
                    bus.worldCenterOfMass, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.rigidbody != bus && hit.rigidbody != body;
                if (!blocked) bus.GetComponent<BusDamage>()?.TakePoliceProjectileHit(transform.position);
            }
        }
        Destroy(gameObject);
    }

    private void CreateWarning()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out var hit, 160f, ~0, QueryTriggerInteraction.Ignore)) return;
        warning = new GameObject("Zone de largage police");
        warning.transform.position = hit.point + Vector3.up * .08f;
        var line = warning.AddComponent<LineRenderer>();
        warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        warningMaterial.color = new Color(1f, .2f, .02f);
        line.sharedMaterial = warningMaterial; line.useWorldSpace = false; line.loop = true;
        line.widthMultiplier = .16f; line.positionCount = 48;
        float radius = burningDumpster ? 3.6f : BlastRadius;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }

    private void CreateFire()
    {
        var root = new GameObject("Flammes de la poubelle"); root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.up * 2.4f;
        root.transform.localScale = Vector3.one * 2f;
        var fire = root.AddComponent<ParticleSystem>(); fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = fire.main; main.startLifetime = .65f; main.startSpeed = 2f;
        main.startSize = new ParticleSystem.MinMaxCurve(.35f, .8f); main.maxParticles = 70;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .2f, .01f), new Color(1f, .8f, .05f));
        var shape = fire.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1.5f, .2f, 1f);
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var emission = fire.emission; emission.rateOverTime = 40f;
        fireMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        fireMaterial.SetFloat("_Surface", 1f);
        fireMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fireMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        fireMaterial.SetFloat("_ZWrite", 0f);
        fireMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fireMaterial.renderQueue = 3000;
        fireTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        fireTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float alpha = Mathf.Clamp01(1f - new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude);
            fireTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
        }
        fireTexture.Apply(false, true); fireMaterial.mainTexture = fireTexture;
        var fade = fire.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .1f, 0f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        fire.GetComponent<ParticleSystemRenderer>().sharedMaterial = fireMaterial;
        fire.Play();
        var light = root.AddComponent<Light>(); light.color = new Color(1f, .3f, .03f); light.intensity = 4f; light.range = 7f;
    }

    private void OnDestroy()
    {
        if (warning != null) Destroy(warning);
        if (warningMaterial != null) Destroy(warningMaterial);
        if (fireMaterial != null) Destroy(fireMaterial);
        if (fireTexture != null) Destroy(fireTexture);
    }
}
