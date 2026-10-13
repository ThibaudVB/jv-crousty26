using UnityEngine;

public class PedestrianFlee : MonoBehaviour
{
    [Header("Détection")]
    [Tooltip("Distance de base (depuis la carrosserie du bus) à laquelle le personnage le remarque")]
    public float detectionRadius = 12f;
    [Tooltip("Distance en plus par m/s de vitesse du bus : un bus rapide est repéré de plus loin")]
    public float extraRadiusPerSpeed = 0.8f;
    [Tooltip("Il arrête de fuir quand le bus est plus loin que (rayon x ce facteur)")]
    public float stopFactor = 1.3f;

    [Header("Fuite")]
    [Tooltip("Vitesse minimale de fuite (m/s), même si le bus est lent")]
    public float minFleeSpeed = 5f;
    [Tooltip("Vitesse de fuite = vitesse du bus x ce facteur. Garde-le au-dessus de 1 pour ne jamais être rattrapé")]
    public float speedMultiplier = 1.1f;
    public float turnSpeed = 12f;

    [Header("Debug")]
    [SerializeField] private bool isFleeing;
    public bool showGizmos = true;

    // Le bus est cherché une seule fois pour tous les personnages
    private static Rigidbody busBody;
    private static Collider busCollider;
    private static float nextSearchTime;

    private static bool EnsureBus()
    {
        if (busBody != null) return true;

        // On limite la recherche à une fois par seconde s'il n'y a pas de bus
        if (Time.time < nextSearchTime) return false;
        nextSearchTime = Time.time + 1f;

        BusController bus = FindFirstObjectByType<BusController>();
        if (bus == null) return false;

        busBody = bus.GetComponent<Rigidbody>();
        busCollider = bus.GetComponent<Collider>();
        return busBody != null;
    }

    private void Update()
    {
        if (!EnsureBus()) return;

        Vector3 pos = transform.position;

        // Vitesse horizontale du bus
        Vector3 busVelocity = busBody.linearVelocity;
        busVelocity.y = 0f;
        float busSpeed = busVelocity.magnitude;

        // Direction opposée au point de la carrosserie le plus proche (et pas au centre du bus, qui est long)
        Vector3 reference = busCollider != null ? busCollider.ClosestPoint(pos) : busBody.position;
        Vector3 away = pos - reference;
        away.y = 0f;

        // Cas limite : le personnage est dans le collider du bus -> on s'éloigne du centre
        if (away.sqrMagnitude < 0.0001f)
        {
            away = pos - busBody.position;
            away.y = 0f;
        }

        float distance = away.magnitude;
        float radius = detectionRadius + busSpeed * extraRadiusPerSpeed;

        if (!isFleeing && distance < radius)
            isFleeing = true;
        else if (isFleeing && distance > radius * stopFactor)
            isFleeing = false;

        if (!isFleeing) return;

        Vector3 direction = distance > 0.001f ? away / distance : transform.forward;
        float speed = Mathf.Max(minFleeSpeed, busSpeed * speedMultiplier);

        // Déplacement direct : traverse les murs, reste à sa hauteur
        transform.position += direction * speed * Time.deltaTime;

        // Se tourne dans le sens de la fuite
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}