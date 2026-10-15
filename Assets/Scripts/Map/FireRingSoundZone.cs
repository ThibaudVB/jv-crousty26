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

        // Le bus est passé de l'autre côté sans sortir de la zone. Le bord du cercle est solide,
        // donc le seul moyen de changer de côté ici, c'est de passer dans le trou.
        bool crossed = continuing && side * lastSide < 0f;
        lastSide = side;

        if (!crossed || Time.time - lastPlayTime < cooldown) return;
        lastPlayTime = Time.time;
        // Le son et le moment joué (startTime / maximumDuration) se règlent dans Resources/Crousty/SoundBank
        CroustySoundDesignManager.Play(CroustySound.FireRing);
    }
}
