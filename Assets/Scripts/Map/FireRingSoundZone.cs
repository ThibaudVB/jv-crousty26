using UnityEngine;

// Zone invisible au milieu d'un cercle de feu : quand le bus la traverse, on joue le son.
// Même principe que la zone du garage, mais au lieu de réparer ça fait un bruit.
// La ville procédurale l'ajoute toute seule sur les objets "FireRing_...".
[RequireComponent(typeof(Collider))]
public class FireRingSoundZone : MonoBehaviour
{
    [Tooltip("Temps minimum (s) entre deux sons, le bus a plusieurs colliders")]
    public float cooldown = 1.5f;

    private float lastPlayTime = -999f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var bus = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<BusController>()
            : other.GetComponentInParent<BusController>();
        if (bus == null) return;

        if (Time.time - lastPlayTime < cooldown) return;
        lastPlayTime = Time.time;

        // Le son et le moment joué (startTime / maximumDuration) se règlent dans Resources/Crousty/SoundBank
        CroustySoundDesignManager.Play(CroustySound.FireRing);
    }
}
