using UnityEngine;
using System.Collections.Generic;

// Voiture de police qui conduit vraiment : elle suit le chemin pris par le bus (les memes rues),
// accelere, ralentit dans les virages et fonce droit sur le bus quand elle le voit de pres.
// Le manager decide quand elle apparait, abandonne ou capture.
[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PoliceCarAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("Vitesse de rotation du volant (degres par seconde)")]
    [SerializeField, Min(30f)] private float turningSpeed = 160f;

    [Header("Conduite")]
    [SerializeField, Min(1f)] private float maxSpeedKmh = 82f;
    [SerializeField, Min(1f)] private float acceleration = 16f;
    [SerializeField, Min(1f)] private float brakeDeceleration = 30f;
    [Tooltip("Vitesse maximale dans un virage a angle droit")]
    [SerializeField, Min(1f)] private float cornerSpeedKmh = 35f;
    [Tooltip("La voiture vise un point du chemin a cette distance devant elle")]
    [SerializeField, Min(1f)] private float lookAhead = 7f;
    [Tooltip("En dessous de cette distance, si rien ne gene, elle fonce droit sur le bus")]
    [SerializeField, Min(1f)] private float directChaseDistance = 25f;
    [Tooltip("Ecart garde entre son pare-chocs et l'arriere du bus")]
    [SerializeField, Min(0f)] private float bumperGap = 1f;

    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private readonly List<Vector3> path = new List<Vector3>(512);
    private Rigidbody rb;
    private BoxCollider body;
    private BoxCollider busBox;
    private float bottomOffset;
    private float frontOffset;
    private bool initialized;
    private float speed;
    private float yaw;
    private float slowRemaining;
    private float slowFactor = 1f;
    private float wobbleTime;

    public float SpeedKmh => speed * 3.6f;
    public bool IsSlowed => slowRemaining > 0f;
    // Distance a plat entre le centre de la voiture et celui du bus
    public float DistanceToTarget { get; private set; }
    // Distance a laquelle elle colle le bus
    public float FollowDistance => BusHalfLength + frontOffset + bumperGap;

    private float BusHalfLength => busBox != null
        ? busBox.size.z * Mathf.Abs(busBox.transform.lossyScale.z) * 0.5f : 3f;

    private void Awake() => InitializeBody();

    public void InitializeBody()
    {
        if (initialized) return;
        initialized = true;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
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
        targetRigidbody = newTargetRigidbody;
        busBox = targetRigidbody != null ? targetRigidbody.GetComponent<BoxCollider>() : null;
    }

    // Place la voiture sur le chemin du bus. route = points du chemin a suivre ensuite (du plus ancien au plus recent).
    public void Spawn(Vector3 position, Vector3 forward, IEnumerable<Vector3> route, float startSpeedKmh)
    {
        InitializeBody();
        path.Clear();
        path.AddRange(route);
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = target != null ? target.forward : Vector3.forward;
        yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
        speed = Mathf.Min(startSpeedKmh, maxSpeedKmh) / 3.6f;
        slowRemaining = 0f;
        position.y = GroundHeight(position, position.y + 1f) + bottomOffset + 0.03f;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        DistanceToTarget = target != null ? Flat(target.position - position).magnitude : 0f;
    }

    // Nouveau point du chemin du bus, enregistre par le manager.
    public void AddWaypoint(Vector3 point) => path.Add(point);

    // Riz : la voiture glisse et roule au ralenti pendant quelques secondes.
    public void Slow(float duration, float factor)
    {
        if (!IsSlowed) speed *= 0.5f;
        slowRemaining = Mathf.Max(slowRemaining, duration);
        slowFactor = Mathf.Clamp01(factor);
    }

    // Un pas de conduite. speedFactor < 1 : la voiture roule moins vite (aveuglee, abandon...).
    public void Drive(float dt, float speedFactor = 1f)
    {
        if (target == null || targetRigidbody == null || dt <= 0f) return;
        InitializeBody();

        Vector3 position = rb.position;
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 busCenter = busBox != null ? busBox.transform.TransformPoint(busBox.center) : target.position;
        Vector3 busForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        float busSpeed = Mathf.Max(0f, Vector3.Dot(targetRigidbody.linearVelocity, busForward));
        DistanceToTarget = Flat(busCenter - position).magnitude;

        // Oublie les points deja depasses
        while (path.Count > 0)
        {
            Vector3 toPoint = Flat(path[0] - position);
            if (toPoint.magnitude > 3f && Vector3.Dot(toPoint, forward) > 0f) break;
            path.RemoveAt(0);
        }

        // Cible : de pres et sans obstacle, droit derriere le bus. Sinon, un point du chemin.
        Vector3 chasePoint = busCenter - busForward * FollowDistance;
        bool direct = path.Count == 0 ||
            (DistanceToTarget < directChaseDistance && CanSee(position, busCenter));
        Vector3 aim = direct ? chasePoint : PointAhead(position, lookAhead);

        // Vitesse voulue : plein gaz, moins dans les virages, et on cale sa vitesse sur celle du bus de pres
        Vector3 toAim = Flat(aim - position);
        float turnAngle = toAim.sqrMagnitude > 0.01f ? Vector3.Angle(forward, toAim) : 0f;
        float maxSpeed = maxSpeedKmh / 3.6f * speedFactor * (IsSlowed ? slowFactor : 1f);
        float desired = Mathf.Min(maxSpeed, Mathf.Lerp(maxSpeed, cornerSpeedKmh / 3.6f, turnAngle / 90f));
        float gap = DistanceToTarget - FollowDistance;
        if (gap < 15f)
            desired = Mathf.Min(desired, busSpeed + gap * 1.5f);
        desired = Mathf.Max(0f, desired);
        speed = Mathf.MoveTowards(speed, desired, (desired > speed ? acceleration : brakeDeceleration) * dt);

        // Volant : on tourne vers la cible, d'autant moins qu'on roule doucement
        if (toAim.sqrMagnitude > 0.01f)
        {
            float aimYaw = Quaternion.LookRotation(toAim, Vector3.up).eulerAngles.y;
            yaw = Mathf.MoveTowardsAngle(yaw, aimYaw, turningSpeed * Mathf.Clamp01(speed / 4f) * dt);
        }
        forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        position += forward * speed * dt;
        float reference = path.Count > 0 ? Mathf.Max(position.y - bottomOffset, path[0].y) : target.position.y;
        position.y = GroundHeight(position, reference + 0.8f) + bottomOffset + 0.03f;

        // Sur le riz, la voiture zigzague
        float wobble = 0f;
        if (IsSlowed)
        {
            slowRemaining = Mathf.Max(0f, slowRemaining - dt);
            wobbleTime += dt;
            wobble = Mathf.Sin(wobbleTime * 14f) * 25f * Mathf.Clamp01(slowRemaining);
        }

        rb.MovePosition(position);
        rb.MoveRotation(Quaternion.Euler(0f, yaw + wobble, 0f));
    }

    public bool IsChasingTarget(Rigidbody candidate)
        => isActiveAndEnabled && target != null && targetRigidbody == candidate;

    public bool IsTouchingCaptureZone(BoxCollider captureZone)
    {
        return body != null && captureZone != null && Physics.ComputePenetration(
            body, rb.position, rb.rotation,
            captureZone, captureZone.transform.position, captureZone.transform.rotation,
            out _, out _);
    }

    // Rien entre la voiture et le bus (murs, batiments) ?
    private bool CanSee(Vector3 from, Vector3 to)
    {
        from.y += 1f;
        to.y = Mathf.Max(to.y, from.y);
        if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.rigidbody == targetRigidbody;
    }

    // Point du chemin a "distance" metres devant la voiture
    private Vector3 PointAhead(Vector3 position, float distance)
    {
        Vector3 previous = position;
        foreach (Vector3 point in path)
        {
            float length = Flat(point - previous).magnitude;
            if (distance <= length)
                return Vector3.Lerp(previous, point, length > 0.001f ? distance / length : 1f);
            distance -= length;
            previous = point;
        }
        return previous;
    }

    // Sol le plus haut sous la voiture, sans monter sur les toits
    private float GroundHeight(Vector3 position, float maxHeight)
    {
        int count = Physics.RaycastNonAlloc(new Vector3(position.x, maxHeight + 3f, position.z), Vector3.down,
            groundHits, 12f, groundLayers, QueryTriggerInteraction.Ignore);
        float highest = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
            if (groundHits[i].rigidbody == null && groundHits[i].normal.y > 0.65f &&
                groundHits[i].point.y <= maxHeight)
                highest = Mathf.Max(highest, groundHits[i].point.y);
        return float.IsNegativeInfinity(highest) ? position.y - bottomOffset - 0.03f : highest;
    }

    private static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
}
