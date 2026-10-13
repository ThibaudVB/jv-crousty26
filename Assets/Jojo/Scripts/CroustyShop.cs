using UnityEngine;

// Magasin Crousty : le bus vient y charger ses commandes.
// Le chargement est fait par le DeliveryManager quand le bus est assez près.
// Un faisceau lumineux jaune montre le magasin, seulement quand il faut y retourner.
//
// A placer sur le magasin. (La ville procédurale en ajoute aussi un toute seule.)
public class CroustyShop : MonoBehaviour
{
    [Header("Zone de chargement")]
    [Tooltip("Distance (m) à laquelle le bus charge les commandes")]
    public float pickupRadius = 8f;
    [Tooltip("Écart de hauteur maximal (m) : évite de charger depuis un toit ou un pont")]
    [Min(0f)] public float maxHeightDifference = 6f;

    [Header("Marqueur")]
    [Tooltip("Optionnel : objet affiché au-dessus du magasin. Si vide, un faisceau lumineux est créé.")]
    public GameObject marker;
    public Color markerColor = new Color(1f, 0.85f, 0.1f);
    [Tooltip("Hauteur du faisceau (m)")]
    public float beamHeight = 150f;
    [Tooltip("Largeur du faisceau (m)")]
    public float beamWidth = 3f;

    [Tooltip("Nom affiché sur le repère à l'écran")]
    public string screenLabel = "Crousty";

    private void Start()
    {
        if (marker == null)
            marker = JojoVisuals.CreateBeacon(transform, markerColor, beamHeight, beamWidth);
    }

    private void Update()
    {
        // Le faisceau n'est visible que quand il faut venir chercher des commandes
        DeliveryManager manager = DeliveryManager.Instance;
        if (marker != null && manager != null)
            marker.SetActive(manager.NeedsPickup);
    }

    // Vrai si la position est assez près du magasin
    public bool IsInRange(Vector3 position)
    {
        return JojoVisuals.IsClose(transform.position, position, pickupRadius, maxHeightDifference);
    }

    // Dessine la zone dans la vue Scene
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
