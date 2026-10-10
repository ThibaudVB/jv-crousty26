using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(600)]
[RequireComponent(typeof(BusWheelRig), typeof(Rigidbody))]
public sealed class SceneBaseDriftSmoke : MonoBehaviour
{
    public Material smokeMaterial;
    [Min(0f)] public float minimumSpeedKmh = 14f;
    [Min(1f)] public float fullSmokeSpeedKmh = 45f;
    [Min(0f)] public float emissionRate = 24f;
    [Min(0f)] public float buildResponse = 4f;
    [Min(0f)] public float fadeResponse = 6f;
    [SerializeField, HideInInspector] private int styleRevision;

    private sealed class Emitter
    {
        public BusWheelRig.Wheel wheel;
        public ParticleSystem particles;
        public Vector3 previousContact;
        public bool hadContact;
        public float remainder;
    }

    private BusWheelRig rig;
    private Rigidbody body;
    private Emitter[] emitters;
    private Mesh cloudMesh;
    private Vector3 previousPosition;
    public float DriftIntensity { get; private set; }

    private void Awake()
    {
        ApplyCartoonStyleUpdate();
        rig = GetComponent<BusWheelRig>();
        body = GetComponent<Rigidbody>();
    }

    public bool ApplyCartoonStyleUpdate()
    {
        if (styleRevision >= 1) return false;
        emissionRate = 24f;
        styleRevision = 1;
        return true;
    }

    private void Start()
    {
        if (!rig.IsReady || smokeMaterial == null) return;
        cloudMesh = CreateCloudMesh();
        previousPosition = body.position;
        emitters = new Emitter[rig.wheels.Length];
        for (int i = 0; i < emitters.Length; i++)
            emitters[i] = new Emitter { wheel = rig.wheels[i], particles = CreateParticles(rig.wheels[i]) };
    }

    private ParticleSystem CreateParticles(BusWheelRig.Wheel wheel)
    {
        var holder = new GameObject("Fumee drift - " + wheel.label);
        holder.transform.SetParent(transform, false);
        var particles = holder.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = wheel.driven ? 48 : 16;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        var emission = particles.emission;
        emission.enabled = false; // Emit precisely at each grounded tire contact.
        var shape = particles.shape;
        shape.enabled = false;

        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(Color.white, 1f)
        }, new[] {
            new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(1f, 0.08f),
            new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f)
        });
        color.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1.2f),
            new Keyframe(0.7f, 1.8f), new Keyframe(1f, 1.5f)));
        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.1f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = smokeMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = cloudMesh;
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.enableGPUInstancing = false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color
        });
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
        return particles;
    }

    private static Mesh CreateCloudMesh()
    {
        // Icosphere subdivision 1: the base icosahedron, 20 faces, shared by all emitters.
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] corners = {
            new Vector3(-1, t, 0), new Vector3(1, t, 0),
            new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t),
            new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1),
            new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        int[] faces = {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        for (int i = 0; i < corners.Length; i++) corners[i] = corners[i].normalized * 0.7f;
        var vertices = new List<Vector3>(faces.Length);
        var normals = new List<Vector3>(faces.Length);
        var triangles = new List<int>(faces.Length);
        for (int i = 0; i < faces.Length; i += 3)
        {
            Vector3 a = corners[faces[i]], b = corners[faces[i + 1]], c = corners[faces[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            // Separate vertices per face keep the normals flat instead of smoothing away the facets.
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }
        var mesh = new Mesh { name = "Fumee - Icosphere subdivision 1" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
    }

    private void FixedUpdate()
    {
        if (emitters == null) return;
        float dt = Time.fixedDeltaTime;
        if ((body.position - previousPosition).sqrMagnitude > 25f)
            ClearSmoke();
        previousPosition = body.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        float speedKmh = horizontal.magnitude * 3.6f;
        float speedFactor = Mathf.InverseLerp(minimumSpeedKmh, fullSmokeSpeedKmh, speedKmh);
        Vector3 local = Quaternion.Inverse(body.rotation) * horizontal;
        float slipAngle = Mathf.Atan2(Mathf.Abs(local.x), Mathf.Abs(local.z) + 0.01f) * Mathf.Rad2Deg;
        float physicalDrift = Mathf.InverseLerp(4f, 18f, slipAngle);
        float arcadeDrift = rig.fullArcade ? Mathf.InverseLerp(0.25f, 0.85f, Mathf.Abs(rig.SteeringAmount)) : 0f;
        float target = rig.IsGrounded && !body.isKinematic
            ? Mathf.Max(physicalDrift, arcadeDrift) * speedFactor : 0f;
        DriftIntensity = Mathf.MoveTowards(DriftIntensity, target,
            (target > DriftIntensity ? buildResponse : fadeResponse) * dt);

        foreach (var emitter in emitters)
        {
            if (!emitter.wheel.collider.GetGroundHit(out var hit) || hit.normal.y < 0.35f ||
                speedKmh <= minimumSpeedKmh || body.isKinematic)
            {
                emitter.hadContact = false;
                emitter.remainder = 0f;
                continue;
            }
            Vector3 point = hit.point + hit.normal * 0.18f;
            Vector3 previous = emitter.hadContact ? emitter.previousContact : point;
            emitter.previousContact = point;
            emitter.hadContact = true;
            emitter.remainder += emissionRate * DriftIntensity * (emitter.wheel.driven ? 1f : 0.25f) * dt;
            int count = Mathf.FloorToInt(emitter.remainder);
            emitter.remainder -= count;
            Vector3 surfaceVelocity = hit.collider.attachedRigidbody != null
                ? hit.collider.attachedRigidbody.GetPointVelocity(hit.point) : Vector3.zero;
            Vector3 tireVelocity = Vector3.ProjectOnPlane(body.GetPointVelocity(hit.point) - surfaceVelocity, hit.normal);
            Vector3 outward = Vector3.ProjectOnPlane(emitter.wheel.collider.transform.position - body.worldCenterOfMass,
                hit.normal).normalized;
            for (int i = 0; i < count; i++)
            {
                // Spread particles along the traveled segment so fast drifts leave a continuous trail.
                var spawn = new ParticleSystem.EmitParams {
                    position = Vector3.Lerp(previous, point, (i + 0.5f) / count) +
                        Vector3.ProjectOnPlane(Random.insideUnitSphere * 0.12f, hit.normal),
                    velocity = surfaceVelocity + tireVelocity * 0.06f + outward * 0.25f +
                        hit.normal * Random.Range(0.45f, 0.75f),
                    startColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, DriftIntensity))
                };
                emitter.particles.Emit(spawn, 1);
            }
        }
    }

    private void ClearSmoke()
    {
        DriftIntensity = 0f;
        foreach (var emitter in emitters)
        {
            emitter.particles.Clear();
            emitter.hadContact = false;
            emitter.remainder = 0f;
        }
    }

    private void OnEnable()
    {
        if (emitters == null) return;
        ClearSmoke();
        previousPosition = body.position;
        foreach (var emitter in emitters) emitter.particles.Play();
    }

    private void OnDisable()
    {
        if (emitters == null) return;
        DriftIntensity = 0f;
        foreach (var emitter in emitters)
        {
            emitter.particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            emitter.hadContact = false;
            emitter.remainder = 0f;
        }
    }

    private void OnDestroy()
    {
        if (cloudMesh != null) Destroy(cloudMesh);
    }
}
