using UnityEngine;

// Visual follower: the speed pressure manager owns the distance and capture rules.
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PoliceCarAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField, Min(0f)] private float rearGap = 50f;
    [SerializeField] private LayerMask groundLayers = ~0;
    private readonly RaycastHit[] groundHits = new RaycastHit[32];
    private Rigidbody rb;
    private BoxCollider busBox;
    private float bottomOffset;
    private float halfLength;

    public float FollowingDistance => rearGap;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        BoxCollider body = GetComponent<BoxCollider>();
        bottomOffset = transform.position.y - body.bounds.min.y;
        halfLength = body.size.z * Mathf.Abs(transform.lossyScale.z) * 0.5f;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bottomOffset = transform.position.y - bounds.min.y;
            // Cache the model length in its local forward direction, before following turns.
            halfLength = Mathf.Max(halfLength, Mathf.Abs(Vector3.Dot(transform.forward,
                new Vector3(bounds.extents.x, 0f, bounds.extents.z))));
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        if (targetRigidbody != null) busBox = targetRigidbody.GetComponent<BoxCollider>();
    }

    public void SetTarget(Transform newTarget, Rigidbody newTargetRigidbody)
    {
        target = newTarget;
        targetRigidbody = newTargetRigidbody;
        busBox = targetRigidbody != null ? targetRigidbody.GetComponent<BoxCollider>() : null;
    }

    public void SetFollowingDistance(float distance)
    {
        rearGap = Mathf.Max(0f, distance);
    }

    public bool IsChasingTarget(Rigidbody candidate)
        => isActiveAndEnabled && target != null && targetRigidbody == candidate;

    private void LateUpdate()
    {
        if (target == null || targetRigidbody == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) return;
        Vector3 center = busBox != null ? busBox.transform.TransformPoint(busBox.center) : target.position;
        float busLength = busBox != null ? busBox.size.z * Mathf.Abs(busBox.transform.lossyScale.z) * 0.5f : 6f;
        Vector3 position = center - forward * (busLength + halfLength + rearGap);
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
        position.y = ground + bottomOffset + 0.08f;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
    }
}
