using UnityEngine;

// Magasin Crousty : les commandes sont chargées automatiquement
// quand le bus entre dans le rayon.
public class CroustyShop : MonoBehaviour
{
    public float pickupRadius = 8f;
    [Tooltip("Écart vertical maximal pour charger les commandes devant le magasin.")]
    [Min(0f)] public float maxHeightDifference = 6f;

    [Header("Marqueur")]
    [Tooltip("Optionnel : objet affiché au-dessus du magasin. Si vide, un faisceau lumineux jaune est créé.")]
    public GameObject marker;
    public Color markerColor = new Color(1f, 0.85f, 0.1f);
    public float beamHeight = 150f;
    public float beamWidth = 3f;

    [Tooltip("Icône sur l'écran, visible à travers les bâtiments")]
    public string screenLabel = "Crousty";

    private void Start()
    {
        if (marker == null)
            marker = JojoVisuals.CreateBeacon(transform, markerColor, beamHeight, beamWidth);
    }

    public bool IsInRange(Vector3 position)
    {
        return Mathf.Abs(position.y - transform.position.y) <= maxHeightDifference &&
            FlatDistance(position, transform.position) <= pickupRadius;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
