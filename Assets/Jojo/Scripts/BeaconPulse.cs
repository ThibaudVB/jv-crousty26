using UnityEngine;

// Fait "respirer" un objet : sa largeur grossit puis rétrécit en boucle.
// Utilisé sur le halo des faisceaux lumineux (magasin et adresses de livraison).
public class BeaconPulse : MonoBehaviour
{
    [Tooltip("Vitesse de la pulsation")]
    public float speed = 2.5f;
    [Tooltip("Force de la pulsation (0.25 = la largeur varie de 25 %)")]
    public float amount = 0.25f;

    private Vector3 startScale;

    private void Start()
    {
        startScale = transform.localScale;
    }

    private void Update()
    {
        // Mathf.Sin varie entre -1 et 1, donc pulse varie entre 1 - amount et 1 + amount
        float pulse = 1f + Mathf.Sin(Time.time * speed) * amount;

        // On ne change que la largeur (X et Z), pas la hauteur
        transform.localScale = new Vector3(startScale.x * pulse, startScale.y, startScale.z * pulse);
    }
}
