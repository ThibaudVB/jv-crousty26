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

    // Inner opening in local space; independent of the deliberately thick trigger volume.
    public Vector3 apertureExtents;
    private Vector3 lastPosition;
    private Rigidbody trackedBody;
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
        bool continuing = trackedBody == body && Time.fixedTime - lastStepTime < 0.2f;
        trackedBody = body;
        lastStepTime = Time.fixedTime;

        // De quel côté du cercle est le centre du bus ?
        Vector3 axis = transform.TransformDirection(localAxis).normalized;
        Vector3 offset = body.worldCenterOfMass - transform.position;
        float side = Vector3.Dot(offset, axis);

        // Crossing the thick trigger plane is not enough: test the actual opening.
        bool crossed = continuing && ((lastSide < 0f && side >= 0f) || (lastSide > 0f && side <= 0f));
        Vector3 position = transform.InverseTransformPoint(body.worldCenterOfMass);
        if (crossed)
        {
            Vector3 intersection = Vector3.Lerp(lastPosition, position, lastSide / (lastSide - side));
            crossed = InsideOpening(intersection);
        }
        lastPosition = position;
        lastSide = side;

        if (!crossed || Time.time - lastPlayTime < cooldown) return;
        lastPlayTime = Time.time;
        // Le son et le moment joué (startTime / maximumDuration) se règlent dans Resources/Crousty/SoundBank
        CroustySoundDesignManager.Play(CroustySound.FireRing);
    }
    public bool InsideOpening(Vector3 position)
    {
        Vector3 extents = apertureExtents;
        if (extents == Vector3.zero && GetComponent<BoxCollider>() is BoxCollider box)
            extents = box.size * .375f;
        int axis = Mathf.Abs(localAxis.x) > .5f ? 0 : Mathf.Abs(localAxis.y) > .5f ? 1 : 2;
        int u = (axis + 1) % 3, v = (axis + 2) % 3;
        if (extents[u] <= 0f || extents[v] <= 0f) return false;
        float x = position[u] / extents[u], y = position[v] / extents[v];
        return x * x + y * y <= 1f;
    }

}
