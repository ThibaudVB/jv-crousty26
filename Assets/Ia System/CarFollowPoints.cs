using UnityEngine;

public class CarFollowPoints : MonoBehaviour
{
    public CarPointRoute route;
    private int routeIndex;

    public void FollowRoute(CarPointRoute path, int index = 0)
    {
        route = path;
        routeIndex = index;
        GoToFirstPoint(path.transform.GetChild(index));
    }

    [Header("Point de départ (le seul que tu donnes)")]
    public Transform firstPoint;

    [Header("Tags des points")]
    public string routeTag = "route";
    public string exitTag = "sortie";

    [Header("Sens de circulation (tags sur le PARENT des points)")]
    public bool useLanes = true;
    public string leftLaneTag = "GaucheSens";
    public string rightLaneTag = "DroitSens";

    [Header("Déplacement")]
    public float speed = 10f;
    public float turnSpeed = 5f;
    public float reachDistance = 1.5f;

    [Header("Recherche devant la voiture")]
    public float frontOffset = 2f;                        // distance du centre de la voiture à son pare-chocs avant
    public float searchDistance = 10f;                    // portée de la recherche
    [Range(1f, 180f)] public float searchHalfAngle = 60f; // angle de chaque côté de l'axe avant

    [Header("Élargissement si rien trouvé")]
    public float fallbackHalfAngle = 60f;   // angle utilisé temporairement
    public float fallbackDuration = 1f;     // durée de l'élargissement
    public float retryInterval = 0.1f;      // fréquence des nouvelles tentatives

    [Header("Debug")]
    [SerializeField] private Transform currentPoint;
    [SerializeField] private string currentPointName;
    [SerializeField] private string currentLane;
    public bool showGizmos = true;
    public bool showSearchZone = true;
    public bool debugLogs = true;

    private Transform previousPoint;   // évite de repartir en arrière

    // Élargissement temporaire de l'angle
    private bool isWidening;
    private float wideningEndTime;
    private float nextRetryTime;
    private float baseHalfAngle;
    private Transform lastReached;

    public Transform CurrentPoint => currentPoint;
    public string CurrentLane => currentLane;
    public bool IsStopped => currentPoint == null;

    private Vector3 SearchOrigin => transform.position + transform.forward * frontOffset;

    void Start()
    {
        if (currentPoint != null) return;
        if (route != null) FollowRoute(route);
        else if (firstPoint != null) GoToFirstPoint(firstPoint);
    }

    // Appelé par toi, ou par le script d'intersection
    public void GoToFirstPoint(Transform point)
    {
        EndWidening();                  // une destination imposée annule l'élargissement
        previousPoint = null;
        currentLane = GetLane(point);   // le sens de la voiture = celui du point donné
        SetCurrentPoint(point);
    }

    // Destination imposée : la voiture va tout droit vers ce point, sans cône ni filtre
    public void ForceDestination(Transform point) => GoToFirstPoint(point);

    void Update()
    {
        if (isWidening) UpdateWidening();

        if (currentPoint == null) return;

        Vector3 dir = currentPoint.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude <= reachDistance)
        {
            OnPointReached();
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        if (route != null)
        {
            Vector3 target = currentPoint.position;
            target.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        }
        else transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnPointReached()
    {
        Transform reached = currentPoint;
        if (route != null)
        {
            if (++routeIndex < route.transform.childCount)
                SetCurrentPoint(route.transform.GetChild(routeIndex));
            else
            {
                CarPointRoute nextRoute = route.nextRoute;
                if (route.intersection != null)
                {
                    Transform entrance = route.intersection.ChooseEntrance(transform);
                    if (entrance != null) nextRoute = entrance.GetComponentInParent<CarPointRoute>();
                }
                if (nextRoute != null) FollowRoute(nextRoute);
                else SetCurrentPoint(null);
            }
            return;
        }

        // Si le point atteint a un sens, la voiture l'adopte
        string lane = GetLane(reached);
        if (lane != null) currentLane = lane;

        // Sortie atteinte : la voiture s'arrête (l'intersection prendra le relais)
        if (reached.CompareTag(exitTag))
        {
            if (debugLogs) Debug.Log($"{name} : sortie atteinte, arrêt.", this);
            previousPoint = reached;
            SetCurrentPoint(null);
            return;
        }

        // On regarde devant : d'abord un point route, sinon une sortie
        Transform next = FindAhead(routeTag, reached);
        if (next == null)
            next = FindAhead(exitTag, reached);

        if (next != null)
        {
            if (debugLogs) Debug.Log($"{name} : point trouvé devant -> {next.name} ({currentLane})", this);
            previousPoint = reached;
            SetCurrentPoint(next);
        }
        else
        {
            Debug.LogWarning($"{name} : rien trouvé devant après {reached.name} ({currentLane}).", this);
            StartWidening(reached);     // angle élargi pendant fallbackDuration
            SetCurrentPoint(null);
        }
    }

    Transform FindAhead(string tag, Transform reached)
    {
        Vector3 origin = SearchOrigin;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Transform best = null;
        float bestDist = searchDistance;

        foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag))
        {
            Transform t = go.transform;

            // On ignore le point qu'on vient d'atteindre et celui d'avant
            if (t == reached || t == previousPoint) continue;

            // On ignore les points de l'autre sens de circulation
            if (useLanes && !IsSameLane(t)) continue;

            Vector3 to = t.position - origin;
            to.y = 0f;

            float d = to.magnitude;
            if (d > bestDist) continue;

            // Doit être dans le cône devant la voiture
            if (d > 0.01f && Vector3.Angle(forward, to) > searchHalfAngle) continue;

            bestDist = d;
            best = t;
        }

        return best;
    }

    // ----- Élargissement temporaire de l'angle -----

    void StartWidening(Transform reached)
    {
        // Inutile si l'angle est déjà au moins aussi large
        if (isWidening || fallbackHalfAngle <= searchHalfAngle) return;

        lastReached = reached;
        baseHalfAngle = searchHalfAngle;
        searchHalfAngle = fallbackHalfAngle;

        isWidening = true;
        wideningEndTime = Time.time + fallbackDuration;
        nextRetryTime = Time.time;
    }

    void UpdateWidening()
    {
        if (Time.time >= nextRetryTime)
        {
            nextRetryTime = Time.time + retryInterval;

            Transform next = FindAhead(routeTag, lastReached);
            if (next == null)
                next = FindAhead(exitTag, lastReached);

            if (next != null)
            {
                if (debugLogs) Debug.Log($"{name} : point trouvé avec l'angle élargi -> {next.name}", this);
                previousPoint = lastReached;
                SetCurrentPoint(next);
                EndWidening();
                return;
            }
        }

        if (Time.time >= wideningEndTime)
        {
            if (debugLogs) Debug.LogWarning($"{name} : toujours rien après l'élargissement.", this);
            EndWidening();
        }
    }

    void EndWidening()
    {
        if (!isWidening) return;

        searchHalfAngle = baseHalfAngle;   // on remet l'angle d'origine
        isWidening = false;
    }

    // ----- Sens de circulation -----

    // Remonte les parents pour trouver GaucheSens / DroitSens (null si aucun)
    string GetLane(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag(leftLaneTag)) return leftLaneTag;
            if (t.CompareTag(rightLaneTag)) return rightLaneTag;
            t = t.parent;
        }
        return null;
    }

    bool IsSameLane(Transform point)
    {
        string lane = GetLane(point);

        // Point sans sens ou voiture sans sens : on accepte
        if (lane == null || string.IsNullOrEmpty(currentLane)) return true;

        return lane == currentLane;
    }

    // ----- Debug -----

    void SetCurrentPoint(Transform point)
    {
        currentPoint = point;

        if (point == null)
            currentPointName = "(aucun)";
        else
            currentPointName = point.parent != null
                ? point.parent.name + " / " + point.name
                : point.name;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Ligne vers le point visé + zone d'arrivée
        if (currentPoint != null)
        {
            Gizmos.color = currentPoint.CompareTag(exitTag) ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, currentPoint.position);
            Gizmos.DrawWireSphere(currentPoint.position, reachDistance);
        }

        if (!showSearchZone) return;

        // Cône de recherche devant la voiture
        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;
        fwd.Normalize();

        Vector3 origin = SearchOrigin;
        Gizmos.color = isWidening ? Color.magenta : Color.yellow;   // magenta pendant l'élargissement

        Vector3 left = Quaternion.Euler(0f, -searchHalfAngle, 0f) * fwd * searchDistance;
        Vector3 right = Quaternion.Euler(0f, searchHalfAngle, 0f) * fwd * searchDistance;
        Gizmos.DrawRay(origin, left);
        Gizmos.DrawRay(origin, right);

        const int steps = 20;
        Vector3 prev = origin + left;
        for (int i = 1; i <= steps; i++)
        {
            float a = Mathf.Lerp(-searchHalfAngle, searchHalfAngle, i / (float)steps);
            Vector3 p = origin + Quaternion.Euler(0f, a, 0f) * fwd * searchDistance;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
