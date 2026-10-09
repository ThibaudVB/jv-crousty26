using UnityEngine;
using System.Collections.Generic;

// Le manager controle le delai; ce composant place la voiture et verifie le contact.
[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PoliceCarAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField, Min(0f)] private float rearGap = 8f;
    [SerializeField] private LayerMask groundLayers = ~0;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private BoxCollider body;
    private BoxCollider busBox;
    private float bottomOffset;
    private float frontOffset;
    private float sideOffset;
    private bool initialized;
    [SerializeField, Min(30f)] private float turningSpeed = 160f;
    private readonly List<Vector3> trail = new List<Vector3>(256);
    private float captureAlignment;
    private bool hasPose;
    public float FollowingDistance => rearGap;

    private void Awake() => InitializeBody();

    public void InitializeBody()
    {
        if (initialized) return;
        initialized = true;
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body = GetComponent<BoxCollider>();
        Bounds localBounds = new Bounds(body.center, body.size);
        bool hasMesh = false;
        // Les bounds locales restent exactes meme si le prefab est inactif ou tourne.
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!hasMesh) { localBounds = new Bounds(point, Vector3.zero); hasMesh = true; }
                else localBounds.Encapsulate(point);
            }
        }
        body.center = localBounds.center;
        body.size = localBounds.size;
        bottomOffset = -localBounds.min.y * Mathf.Abs(transform.lossyScale.y);
        frontOffset = localBounds.max.z * Mathf.Abs(transform.lossyScale.z);
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        // Un trigger permet de verifier le contact sans pousser ni bloquer le bus.
        body.isTrigger = true;
        body.enabled = true;
    }

    public void SetTarget(Transform newTarget, Rigidbody newTargetRigidbody)
    {
        target = newTarget;
        trail.Clear();
        hasPose = false;
        targetRigidbody = newTargetRigidbody;
        busBox = targetRigidbody != null ? targetRigidbody.GetComponent<BoxCollider>() : null;
    }

    public void SetFollowingDistance(float distance, float lateralOffset = 0f, float alignment = 0f)
    {
        rearGap = Mathf.Max(0f, distance);
        sideOffset = lateralOffset;
        captureAlignment = Mathf.Clamp01(alignment);
    }

    public bool IsChasingTarget(Rigidbody candidate)
        => isActiveAndEnabled && target != null && targetRigidbody == candidate;

    public float GetCaptureGap(BoxCollider captureZone)
    {
        if (target == null || busBox == null || captureZone == null) return 0f;
        Vector3 forward = target.forward;
        Vector3 rear = busBox.transform.TransformPoint(busBox.center - Vector3.forward * busBox.size.z * 0.5f);
        Vector3 zoneRear = captureZone.transform.TransformPoint(captureZone.center - Vector3.forward * captureZone.size.z * 0.5f);
        // Un leger chevauchement donne un contact geometrique fiable a la fin du delai.
        return Mathf.Max(0f, Vector3.Dot(rear - zoneRear, forward) - 0.03f);
    }

    public bool IsTouchingCaptureZone(BoxCollider captureZone)
    {
        return body != null && captureZone != null && Physics.ComputePenetration(
            body, transform.position, transform.rotation,
            captureZone, captureZone.transform.position, captureZone.transform.rotation,
            out _, out _);
    }

    // La rotation avance au pas physique; ici on actualise seulement la position visuelle.
    private void LateUpdate() => UpdateFollowingPose();

    public void UpdateFollowingPose(float deltaTime = 0f)
    {
        if (target == null || targetRigidbody == null) return;
        InitializeBody();
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) return;
        Vector3 center = busBox != null ? busBox.transform.TransformPoint(busBox.center) : target.position;
        float busLength = busBox != null ? busBox.size.z * Mathf.Abs(busBox.transform.lossyScale.z) * 0.5f : 3f;
        float followingLength = busLength + frontOffset + rearGap;
        RecordTrail(center, forward);
        Vector3 pathPosition = SampleTrail(center, followingLength);
        // Regarde quelques metres plus loin sur la trajectoire pour arrondir les virages.
        Vector3 pathForward = Vector3.ProjectOnPlane(
            SampleTrail(center, Mathf.Max(0f, followingLength - 2f)) -
            SampleTrail(center, followingLength + 2f), Vector3.up).normalized;
        if (pathForward.sqrMagnitude < 0.01f) pathForward = forward;
        Vector3 capturePosition = center - forward * followingLength;
        Vector3 position = Vector3.Lerp(pathPosition, capturePosition, captureAlignment);
        Vector3 facing = Vector3.Slerp(pathForward, forward, captureAlignment).normalized;
        position += Vector3.Cross(Vector3.up, facing) * sideOffset;
        position.y = target.position.y;
        float ground = target.position.y;
        int count = Physics.RaycastNonAlloc(position + Vector3.up * 4f, Vector3.down,
            groundHits, 12f, groundLayers, QueryTriggerInteraction.Ignore);
        float highest = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
            if (groundHits[i].rigidbody == null && groundHits[i].normal.y > 0.65f &&
                groundHits[i].point.y <= target.position.y + 0.75f)
                highest = Mathf.Max(highest, groundHits[i].point.y);
        if (!float.IsNegativeInfinity(highest)) ground = highest;
        position.y = ground + bottomOffset + 0.03f;
        Quaternion desiredRotation = Quaternion.LookRotation(facing, Vector3.up);
        Quaternion rotation = !hasPose || captureAlignment >= 0.9999f
            ? desiredRotation
            : Quaternion.RotateTowards(transform.rotation, desiredRotation, turningSpeed * deltaTime);
        transform.SetPositionAndRotation(position, rotation);
        hasPose = true;
    }

    private void RecordTrail(Vector3 center, Vector3 forward)
    {
        // Apres un reset ou une teleportation, repartir de la nouvelle route.
        if (trail.Count == 0 || Vector3.Distance(center, trail[0]) > 40f)
        {
            trail.Clear();
            trail.Add(center);
            trail.Add(center - forward * 80f);
            return;
        }
        if (Vector3.Distance(center, trail[0]) < 0.2f) return;
        trail.Insert(0, center);
        float length = 0f;
        for (int i = 1; i < trail.Count; i++)
        {
            length += Vector3.Distance(trail[i - 1], trail[i]);
            if (length < 80f && i < 511) continue;
            if (i + 1 < trail.Count) trail.RemoveRange(i + 1, trail.Count - i - 1);
            break;
        }
    }

    private Vector3 SampleTrail(Vector3 center, float distance)
    {
        Vector3 newer = center;
        foreach (Vector3 older in trail)
        {
            float length = Vector3.Distance(newer, older);
            if (length > 0.001f && distance <= length)
                return Vector3.Lerp(newer, older, distance / length);
            distance -= length;
            newer = older;
        }
        return newer - target.forward * Mathf.Max(0f, distance);
    }
}
