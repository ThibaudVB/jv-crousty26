using UnityEngine;

// Zone invisible autour d'un cercle de feu : le son ne se joue que quand le bus TRAVERSE le cercle,
// c'est-à-dire qu'il passe d'un côté à l'autre en passant dans le trou.
// Toucher le bord du cercle ou rebondir dessus ne fait rien.
// La ville procédurale l'ajoute toute seule sur les objets "FireRing_...".
[RequireComponent(typeof(Collider))]
public class FireRingSoundZone : MonoBehaviour
{
    [Tooltip("Temps minimum (s) entre deux sons")]
    public float cooldown = 1.5f;
    [Tooltip("Axe du trou du cercle, dans le repère de cet objet")]
    public Vector3 localAxis = Vector3.forward;
    [Tooltip("Collider du cercle. Sert à vérifier que le bus est bien DANS le trou (et pas dessous ou à côté).")]
    public Collider ringCollider;
    [Tooltip("Distance max des rayons qui cherchent le cercle autour du bus (m)")]
    public float searchDistance = 40f;
    [Tooltip("Nombre de directions (sur 8) où le cercle doit être trouvé autour du bus")]
    [Range(1, 8)] public int requiredHits = 7;

    private float lastPlayTime = -999f;
    private float lastSide;
    private float lastStepTime = -999f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) => Track(other);
    private void OnTriggerStay(Collider other) => Track(other);

    private void Track(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null || body.GetComponent<BusController>() == null) return;

        // Le bus a plusieurs colliders : une seule mesure par pas de physique
        if (Time.fixedTime == lastStepTime) return;
        bool continuing = Time.fixedTime - lastStepTime < 0.2f;
        lastStepTime = Time.fixedTime;

        // De quel côté du cercle est le centre du bus ?
        Vector3 axis = transform.TransformDirection(localAxis).normalized;
        Vector3 offset = body.worldCenterOfMass - transform.position;
        float side = Vector3.Dot(offset, axis);

        // Le bus est passé de l'autre côté du cercle...
        bool crossed = continuing && side * lastSide < 0f;
        lastSide = side;

        // ... et il était bien entouré par le cercle (pas dessous, ni à côté)
        if (!crossed || Time.time - lastPlayTime < cooldown || !IsInsideRing(body.worldCenterOfMass, axis)) return;
        lastPlayTime = Time.time;
        // Le son et le moment joué (startTime / maximumDuration) se règlent dans Resources/Crousty/SoundBank
        CroustySoundDesignManager.Play(CroustySound.FireRing);
    }

    // Rayons tirés tout autour du bus, dans le plan du cercle : dans le trou, ils touchent le cercle de partout.
    // Sous le cercle, seuls ceux vers le haut le touchent.
    private bool IsInsideRing(Vector3 point, Vector3 axis)
    {
        if (ringCollider == null) return true; // pas de collider : on ne peut pas vérifier
        Vector3 right = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
        Vector3 up = Vector3.Cross(right, axis).normalized;
        int hits = 0;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector3 direction = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
            if (ringCollider.Raycast(new Ray(point, direction), out _, searchDistance)) hits++;
        }
        return hits >= requiredHits;
    }
}
