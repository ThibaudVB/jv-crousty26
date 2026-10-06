using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Parcours")]
    [Tooltip("Laisser vide = le waypoint le plus proche au démarrage")]
    public TrafficWaypoint startWaypoint;
    public float reachDistance = 2.5f;

    [Header("Vitesse")]
    public float minSpeedKmh = 30f;
    public float maxSpeedKmh = 50f;
    public float acceleration = 8f;
    public float brakeDeceleration = 20f;
    public float turnSpeed = 120f;
    [Range(0.1f, 1f)] public float cornerSpeedFactor = 0.4f;

    [Header("Détection d'obstacles")]
    public float frontOffset = 2.5f;
    public float rayHeight = 0.6f;
    public float raylateralOffset = -1.2f;
    public float detectionDistance = 10f;
    public float stopDistance = 3f;
    public LayerMask obstacleLayers = ~0;

    [Header("Debug")]
    [SerializeField] private float currentSpeedKmh;

    private Rigidbody rb;
    private TrafficWaypoint previousWaypoint;
    private TrafficWaypoint targetWaypoint;
    private float cruiseSpeed;
    private float speed; 

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; 
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        cruiseSpeed = Random.Range(minSpeedKmh, maxSpeedKmh) / 3.6f;
    }

    private void Start()
    {
        if (startWaypoint == null)
            startWaypoint = FindNearestWaypoint();

        if (startWaypoint == null)
        {
            Debug.LogWarning("TrafficCar : aucun TrafficWaypoint dans la scène.", this);
            enabled = false;
            return;
        }

        previousWaypoint = startWaypoint;
        targetWaypoint = startWaypoint.GetRandomNext() ?? startWaypoint;
    }

    private void FixedUpdate()
    {
        if (targetWaypoint == null) return;

        Vector3 toTarget = targetWaypoint.transform.position - rb.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= reachDistance)
        {
            TrafficWaypoint next = targetWaypoint.GetRandomNext(previousWaypoint);

            if (next == null) 
            {
                speed = Mathf.MoveTowards(speed, 0f, brakeDeceleration * Time.fixedDeltaTime);
                return;
            }

            previousWaypoint = targetWaypoint;
            targetWaypoint = next;
            toTarget = targetWaypoint.transform.position - rb.position;
            toTarget.y = 0f;
        }

        Vector3 dir = toTarget.normalized;

        // Ralentit dans les virages
        float angle = Vector3.Angle(transform.forward, dir);
        float desiredSpeed = cruiseSpeed *
            Mathf.Lerp(1f, cornerSpeedFactor, Mathf.Clamp01(angle / 90f));

        // Freine si obstacle devant
        desiredSpeed = Mathf.Min(desiredSpeed, GetObstacleSpeedLimit());

        float rate = desiredSpeed < speed ? brakeDeceleration : acceleration;
        speed = Mathf.MoveTowards(speed, desiredSpeed, rate * Time.fixedDeltaTime);

        // Rotation puis avance
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(
                rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime));
        }

        rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);

        currentSpeedKmh = speed * 3.6f;
    }

    private float GetObstacleSpeedLimit()
    {
        Vector3 origin = transform.position
            + transform.forward * frontOffset
            + Vector3.up * rayHeight
            + transform.right * raylateralOffset;

        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit,
                detectionDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            // Ignore ses propres colliders
            if (hit.transform.IsChildOf(transform))
                return float.MaxValue;

            if (hit.distance <= stopDistance)
                return 0f;

            // Ralentit progressivement entre detectionDistance et stopDistance
            float t = (hit.distance - stopDistance) / (detectionDistance - stopDistance);
            return cruiseSpeed * Mathf.Clamp01(t);
        }

        return float.MaxValue;
    }

    private TrafficWaypoint FindNearestWaypoint()
    {
        TrafficWaypoint[] all =
            FindObjectsByType<TrafficWaypoint>();

        TrafficWaypoint best = null;
        float bestDist = float.MaxValue;

        foreach (TrafficWaypoint w in all)
        {
            float d = (w.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = w;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = transform.position
            + transform.forward * frontOffset
            + Vector3.up * rayHeight
            + transform.right * raylateralOffset;
        Gizmos.DrawLine(origin, origin + transform.forward * detectionDistance);
    }
}