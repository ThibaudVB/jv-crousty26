using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class MapImpactProp : MonoBehaviour
{
    public enum ImpactType { FlyingCone, FallingLamppost }

    [Header("Type d'objet")]
    public ImpactType impactType;
    [Tooltip("Le bus traverse cet objet sans choc physique ; le trigger conserve sa chute visuelle.")]
    public bool passThroughBus;
    [Min(0f)] public float minimumImpactSpeedKmh = 2f;
    [Min(0.1f)] public float mass = 4f;

    [Header("Projection du plot")]
    [Range(0f, 1f)] public float horizontalSpeedMultiplier = 0.65f;
    [Min(0f)] public float maximumLaunchSpeed = 18f;
    [Min(0f)] public float upwardSpeed = 3f;
    [Min(0f)] public float upwardSpeedMultiplier = 0.18f;
    [Min(0f)] public float spinSpeed = 7f;

    [Header("Chute du lampadaire")]
    [Tooltip("Decalage local du pied par rapport au pivot du modele, a hauteur du bas du mesh.")]
    public Vector3 baseOffset;
    [Min(0.01f)] public float poleRadius = 0.18f;
    [Min(0.1f)] public float minimumFallSpeed = 3.5f;
    [Min(0.1f)] public float maximumFallSpeed = 5.5f;

    public bool HasBeenHit { get; private set; }
    public Rigidbody Body { get; private set; }
    internal static readonly HashSet<MapImpactProp> ActiveProps = new HashSet<MapImpactProp>();
    private readonly List<Collider> hitTriggers = new List<Collider>();
    private Vector3 localBase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => ActiveProps.Clear();

    private void OnEnable()
    {
        ActiveProps.Add(this);
        foreach (BusPropImpact bus in BusPropImpact.ActiveBuses)
            if (bus != null) bus.IgnorePropCollisions(this);
    }
    private void OnDisable() => ActiveProps.Remove(this);

    private void OnTriggerEnter(Collider other) => DetectBus(other);
    private void OnTriggerStay(Collider other) => DetectBus(other);
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<BusPropImpact>() != null)
            TryHit(-collision.relativeVelocity);
    }
    private void DetectBus(Collider other)
    {
        if (HasBeenHit || other.isTrigger) return;
        BusPropImpact bus = other.GetComponentInParent<BusPropImpact>();
        if (bus != null) bus.HitProp(this);
    }

    private void Awake()
    {
        // Also cover trees and lamps already placed in the authored scenes.
        foreach (Transform part in GetComponentsInChildren<Transform>(true))
            if (Regex.IsMatch(part.name, @"(^|[ _])(Lamp|StreetLamp|Lamppost|StreetLight|StreetTree|Tree|Palm|FanPalm|FlowerTree|Cypress)(_|[ (]|$)", RegexOptions.IgnoreCase))
            {
                passThroughBus = true;
                break;
            }

        // Actualise aussi les anciennes valeurs d'une scene deja chargee dans l'editeur.
        if (impactType == ImpactType.FallingLamppost &&
            Mathf.Approximately(minimumFallSpeed, 0.6f) && Mathf.Approximately(maximumFallSpeed, 1.8f))
        {
            minimumFallSpeed = 3.5f;
            maximumFallSpeed = 5.5f;
        }
        Bounds bounds = GetLocalMeshBounds();
        localBase = new Vector3(0f, bounds.min.y, 0f) + baseOffset;

        // Les MeshCollider non convexes de la map ne peuvent pas devenir dynamiques.
        foreach (MeshCollider meshCollider in GetComponentsInChildren<MeshCollider>())
            meshCollider.enabled = false;

        if (!HasEnabledSolidCollider())
        {
            if (impactType == ImpactType.FlyingCone)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
            else
            {
                // Le mat reste etroit : le volume vide sous la tete n'est pas un obstacle.
                CapsuleCollider pole = gameObject.AddComponent<CapsuleCollider>();
                pole.direction = 1;
                pole.radius = poleRadius;
                pole.height = Mathf.Max(poleRadius * 2f, bounds.size.y * 0.9f);
                pole.center = localBase + Vector3.up * (pole.height * 0.5f);
                BoxCollider head = gameObject.AddComponent<BoxCollider>();
                head.size = new Vector3(bounds.size.x, bounds.size.y * 0.15f, bounds.size.z);
                head.center = new Vector3(bounds.center.x,
                    bounds.max.y - head.size.y * 0.5f, bounds.center.z);
            }
        }

        Body = GetComponent<Rigidbody>();
        if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
        Body.mass = mass;
        Body.isKinematic = true;
        Body.useGravity = true;
        Body.constraints = RigidbodyConstraints.None;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Body.linearDamping = 0.05f;
        Body.angularDamping = 0.15f;
        Body.maxAngularVelocity = impactType == ImpactType.FlyingCone ? 15f : Mathf.Max(6f, maximumFallSpeed);
        if (impactType == ImpactType.FallingLamppost)
            Body.centerOfMass = localBase + Vector3.up * (bounds.size.y * 0.45f);
        CreateHitTriggers();
    }

    private void CreateHitTriggers()
    {
        // Meme forme que le collider physique, mais sans impulsion sur le bus.
        foreach (Collider solid in GetComponentsInChildren<Collider>())
        {
            if (!solid.enabled || solid.isTrigger || solid is MeshCollider) continue;
            GameObject sensor = new GameObject("Detection impact bus");
            sensor.layer = solid.gameObject.layer;
            sensor.transform.SetParent(solid.transform, false);
            Collider trigger = null;
            if (solid is BoxCollider box)
            {
                BoxCollider copy = sensor.AddComponent<BoxCollider>();
                copy.center = box.center;
                copy.size = box.size * 1.12f;
                trigger = copy;
            }
            else if (solid is CapsuleCollider capsule)
            {
                CapsuleCollider copy = sensor.AddComponent<CapsuleCollider>();
                copy.center = capsule.center;
                copy.radius = capsule.radius * 1.12f;
                copy.height = capsule.height;
                copy.direction = capsule.direction;
                trigger = copy;
            }
            else if (solid is SphereCollider sphere)
            {
                SphereCollider copy = sensor.AddComponent<SphereCollider>();
                copy.center = sphere.center;
                copy.radius = sphere.radius;
                trigger = copy;
            }
            if (trigger != null)
            {
                trigger.isTrigger = true;
                hitTriggers.Add(trigger);
            }
            else Destroy(sensor);
        }
    }

    private bool HasEnabledSolidCollider()
    {
        foreach (Collider candidate in GetComponentsInChildren<Collider>())
            if (candidate.enabled && !candidate.isTrigger) return true;
        return false;
    }

    private Bounds GetLocalMeshBounds()
    {
        Bounds combined = new Bounds(Vector3.up * 0.5f, Vector3.one);
        bool found = false;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(x, y, z));
                        corner = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (!found) { combined = new Bounds(corner, Vector3.zero); found = true; }
                        else combined.Encapsulate(corner);
                    }
        }
        return combined;
    }

    public bool TryHit(Vector3 busVelocity)
    {
        if (!isActiveAndEnabled || HasBeenHit || Body == null) return false;
        Vector3 flatVelocity = Vector3.ProjectOnPlane(busVelocity, Vector3.up);
        float speed = flatVelocity.magnitude;
        if (speed < minimumImpactSpeedKmh / 3.6f || speed < 0.01f) return false;
        Vector3 direction = flatVelocity / speed;
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 fallPivot = transform.TransformPoint(localBase + transform.InverseTransformDirection(direction) * poleRadius);
        HasBeenHit = true;
        foreach (var bus in BusPropImpact.ActiveBuses) if (bus != null) bus.IgnorePropCollisions(this);
        foreach (var fixture in GetComponentsInChildren<CroustyNightFixture>()) fixture.CutPower();
        foreach (Collider trigger in hitTriggers) trigger.enabled = false;

        if (impactType == ImpactType.FallingLamppost)
        {
            HingeJoint hinge = gameObject.AddComponent<HingeJoint>();
            hinge.autoConfigureConnectedAnchor = false;
            // Pivote sur le bord du pied : le bas du mat peut basculer sans penetrer le sol.
            hinge.anchor = localBase + transform.InverseTransformDirection(direction) * poleRadius;
            hinge.connectedAnchor = fallPivot;
            hinge.axis = transform.InverseTransformDirection(rotationAxis);
            JointLimits limits = hinge.limits;
            limits.min = -5f;
            limits.max = 100f;
            limits.bounciness = 0f;
            hinge.limits = limits;
            hinge.useLimits = true;
        }

        Body.isKinematic = false;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (impactType == ImpactType.FlyingCone)
        {
            Body.linearVelocity = direction * Mathf.Min(speed * horizontalSpeedMultiplier, maximumLaunchSpeed)
                + Vector3.up * (upwardSpeed + Mathf.Min(speed, 25f) * upwardSpeedMultiplier);
            Body.angularVelocity = rotationAxis * spinSpeed + Vector3.up * (spinSpeed * 0.3f);
        }
        else
        {
            Body.angularVelocity = rotationAxis * Mathf.Clamp(speed * 0.32f, minimumFallSpeed, maximumFallSpeed);
            // Vitesse tangentielle coherente avec le pivot, sinon le joint freine l'impulsion initiale.
            Body.linearVelocity = Vector3.Cross(Body.angularVelocity, Body.worldCenterOfMass - fallPivot);
        }
        Body.WakeUp();
        return true;
    }
}
