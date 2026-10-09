using UnityEngine;

namespace V1
{
    // Effets du dérapage : traces de pneus, fumée, étincelles qui changent de couleur
    // avec la charge du boost (bleu, orange, violet), flamme pendant le boost, sons.
    // Tout est créé par code. Ajouté automatiquement par BusController.
    [RequireComponent(typeof(BusController))]
    public class BusDriftEffects : MonoBehaviour
    {
        [Header("Traces de pneus")]
        public float skidWidth = 0.35f;
        public float skidLifetime = 8f;
        public Color skidColor = new Color(0.05f, 0.05f, 0.05f, 0.6f);

        [Header("Fumée")]
        public float smokeRate = 35f;

        [Header("Étincelles (niveaux de boost 1, 2, 3)")]
        public Color[] levelColors =
        {
            new Color(0.3f, 0.65f, 1f),
            new Color(1f, 0.55f, 0.1f),
            new Color(0.85f, 0.3f, 1f)
        };

        [Header("Sons")]
        [Range(0f, 1f)] public float screechVolume = 0.45f;
        [Range(0f, 1f)] public float boostVolume = 0.9f;

        private BusController bus;
        private Rigidbody rb;

        private TrailRenderer[] skids;
        private ParticleSystem[] smokes;
        private ParticleSystem[] sparks;
        private ParticleSystem flame;

        private AudioSource screechSource;
        private AudioSource boostSource;

        private static Material particleMaterial;

        private void Awake()
        {
            bus = GetComponent<BusController>();
            rb = GetComponent<Rigidbody>();

            Bounds local = LocalBounds();
            float rearZ = local.min.z + local.size.z * 0.15f;
            float halfWidth = local.extents.x * 0.8f;
            float groundY = local.min.y + 0.05f;

            Vector3 rearLeft = new Vector3(local.center.x - halfWidth, groundY, rearZ);
            Vector3 rearRight = new Vector3(local.center.x + halfWidth, groundY, rearZ);
            Vector3 exhaust = new Vector3(local.center.x, groundY + 0.6f, local.min.z);

            skids = new[] { CreateSkid(rearLeft), CreateSkid(rearRight) };
            smokes = new[] { CreateSmoke(rearLeft), CreateSmoke(rearRight) };
            sparks = new[] { CreateSparks(rearLeft), CreateSparks(rearRight) };
            flame = CreateFlame(exhaust);

            screechSource = ProceduralAudio.CreateSource(gameObject, ProceduralAudio.Screech, true, 0f, 6f, 80f);
            boostSource = ProceduralAudio.CreateSource(gameObject, ProceduralAudio.Whoosh, false, boostVolume, 6f, 80f);
        }

        private void OnEnable()
        {
            if (bus != null)
                bus.Boosted += OnBoosted;
        }

        private void OnDisable()
        {
            if (bus != null)
                bus.Boosted -= OnBoosted;
        }

        private void Update()
        {
            bool grounded = IsGrounded();
            bool sliding = grounded && (bus.IsDrifting ||
                (bus.IsHandbraking && bus.SpeedKmh > 15f) ||
                (bus.SlipAngle > 12f && bus.SlipAngle < 150f && bus.SpeedKmh > 20f));

            foreach (TrailRenderer skid in skids)
                skid.emitting = sliding;

            float intensity = !sliding ? 0f : bus.IsDrifting ? 1f : 0.6f;

            foreach (ParticleSystem smoke in smokes)
            {
                ParticleSystem.EmissionModule emission = smoke.emission;
                emission.rateOverTime = smokeRate * intensity;
            }

            // Étincelles de la couleur du niveau de boost déjà chargé
            int level = bus.IsDrifting ? bus.DriftLevel : 0;
            foreach (ParticleSystem spark in sparks)
            {
                ParticleSystem.EmissionModule emission = spark.emission;
                emission.rateOverTime = level > 0 ? 25f + 15f * level : 0f;

                if (level > 0)
                {
                    ParticleSystem.MainModule main = spark.main;
                    main.startColor = levelColors[Mathf.Min(level, levelColors.Length) - 1];
                }
            }

            ParticleSystem.EmissionModule flameEmission = flame.emission;
            flameEmission.rateOverTime = bus.IsBoosting ? 60f : 0f;

            UpdateScreech(intensity);
        }

        private void UpdateScreech(float intensity)
        {
            float target = intensity * screechVolume;
            screechSource.volume = Mathf.MoveTowards(screechSource.volume, target, Time.deltaTime * 3f);
            screechSource.pitch = 0.85f + Mathf.Clamp01(bus.SpeedKmh / 120f) * 0.3f;

            if (screechSource.volume > 0f && !screechSource.isPlaying)
                screechSource.Play();
            else if (screechSource.volume <= 0f && screechSource.isPlaying)
                screechSource.Stop();
        }

        private void OnBoosted(int level)
        {
            boostSource.pitch = 0.9f + 0.15f * level;
            boostSource.volume = boostVolume;
            boostSource.Play();

            BusCameraFollow cameraFollow = FindAnyObjectByType<BusCameraFollow>();
            if (cameraFollow != null)
                cameraFollow.Shake(0.15f + 0.1f * level);
        }

        private bool IsGrounded()
        {
            // Si le bus tombe ou vole, pas de traces
            return Mathf.Abs(rb.linearVelocity.y) < 4f &&
                   Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 2.5f,
                       ~0, QueryTriggerInteraction.Ignore);
        }

        // ---------- CREATION DES EFFETS ----------

        private TrailRenderer CreateSkid(Vector3 localPosition)
        {
            GameObject emitter = new GameObject("Trace de pneu");
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = localPosition;
            // La trace est posée à plat sur le sol
            emitter.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            TrailRenderer trail = emitter.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.TransformZ;
            trail.time = skidLifetime;
            trail.minVertexDistance = 0.25f;
            trail.widthMultiplier = skidWidth;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = ParticleMaterial;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(skidColor, 0f), new GradientColorKey(skidColor, 1f) },
                new[] { new GradientAlphaKey(skidColor.a, 0f), new GradientAlphaKey(skidColor.a, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            trail.colorGradient = gradient;

            return trail;
        }

        private ParticleSystem CreateSmoke(Vector3 localPosition)
        {
            ParticleSystem smoke = CreateParticles("Fumee", localPosition);

            ParticleSystem.MainModule main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.45f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 300;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            FadeOut(smoke);
            smoke.Play();
            return smoke;
        }

        private ParticleSystem CreateSparks(Vector3 localPosition)
        {
            ParticleSystem spark = CreateParticles("Etincelles", localPosition);

            ParticleSystem.MainModule main = spark.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.gravityModifier = 1f;
            main.maxParticles = 200;

            ParticleSystem.ShapeModule shape = spark.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;
            // Les étincelles partent vers l'arrière
            shape.rotation = new Vector3(0f, 180f, 0f);

            FadeOut(spark);
            spark.Play();
            return spark;
        }

        private ParticleSystem CreateFlame(Vector3 localPosition)
        {
            ParticleSystem fire = CreateParticles("Flamme boost", localPosition);

            ParticleSystem.MainModule main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.05f));
            main.maxParticles = 200;

            ParticleSystem.ShapeModule shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(0f, 180f, 0f);

            FadeOut(fire);
            fire.Play();
            return fire;
        }

        private ParticleSystem CreateParticles(string name, Vector3 localPosition)
        {
            GameObject emitter = new GameObject(name);
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = localPosition;

            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;

            ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ParticleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return particles;
        }

        private static void FadeOut(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            color.color = gradient;
        }

        // Matériau transparent avec une texture ronde et douce, partagé par tous les effets
        private static Material ParticleMaterial
        {
            get
            {
                if (particleMaterial != null)
                    return particleMaterial;

                Shader shader = Shader.Find("Sprites/Default");
                particleMaterial = new Material(shader) { name = "V1 Particules" };
                particleMaterial.mainTexture = CreateSoftDot();
                return particleMaterial;
            }
        }

        private static Texture2D CreateSoftDot()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        // Boîte englobante du modèle du bus, dans l'espace local du bus
        private Bounds LocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, new Vector3(2.5f, 3f, 10f));

            bool first = true;
            Bounds local = new Bounds();

            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;

                Bounds world = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = transform.InverseTransformPoint(corner);

                    if (first)
                    {
                        local = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        local.Encapsulate(point);
                    }
                }
            }

            return local;
        }
    }
}
