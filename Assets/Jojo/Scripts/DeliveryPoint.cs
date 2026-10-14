using UnityEngine;

// Adresse de livraison. Au début de chaque tournée, le DeliveryManager
// en choisit quelques-unes au hasard : elles attendent alors une commande.
// Une adresse qui attend affiche un faisceau lumineux vert.
//
// A placer sur un GameObject devant une maison. (La ville procédurale en ajoute aussi toute seule.)
public class DeliveryPoint : MonoBehaviour
{
    [Header("Zone de livraison")]
    [Tooltip("Distance (m) à laquelle le bus lance le crousty")]
    public float deliveryRadius = 10f;
    [Tooltip("Écart de hauteur maximal (m) : évite de livrer depuis un autre étage")]
    [Min(0f)] public float maxHeightDifference = 6f;

    [Tooltip("Optionnel : endroit où le crousty atterrit (porte, boîte aux lettres...). Si vide : ce point.")]
    public Transform dropTarget;

    [Header("Marqueur")]
    [Tooltip("Optionnel : objet affiché quand une commande attend ici. Si vide, un faisceau lumineux est créé.")]
    public GameObject marker;
    public Color markerColor = new Color(0.2f, 1f, 0.3f);
    [Tooltip("Hauteur du faisceau (m)")]
    public float beamHeight = 150000f;
    [Tooltip("Largeur du faisceau (m)")]
    public float beamWidth = 2.5f;

    // Les étapes d'une adresse :
    // Inactive  -> pas de commande ici
    // Waiting   -> une commande attend, le bus doit passer
    // Pending   -> le crousty est en l'air vers l'adresse
    // Delivered -> livré !
    [Header("Cercle de livraison en vol")]
    [Tooltip("Hauteur minimale du cercle au-dessus de l'adresse (m). Utilisé si le DeliveryManager demande les cercles en vol.")]
    public float ringMinHeight = 50f;
    [Tooltip("Hauteur maximale du cercle (m). Une hauteur au hasard entre les deux est tirée à chaque nouvelle commande.")]
    public float ringMaxHeight = 60f;
    [Tooltip("Rayon du cercle (m) : le bus doit passer à l'intérieur")]
    public float ringRadius = 7f;
    [Tooltip("Épaisseur du tube du cercle (m)")]
    public float ringThickness = 0.6f;

    private Transform ring;
    private float lastRingSide;
    private bool ringPrimed;
    private float ringHeight = -1f; // tirée au hasard entre ringMinHeight et ringMaxHeight

    public Vector3 RingCenter
    {
        get
        {
            if (ringHeight < 0f) PickRingHeight();
            return transform.position + Vector3.up * ringHeight;
        }
    }

    private void PickRingHeight()
    {
        ringHeight = Random.Range(Mathf.Min(ringMinHeight, ringMaxHeight), Mathf.Max(ringMinHeight, ringMaxHeight));
    }

    private enum State { Inactive, Waiting, Pending, Delivered }
    private State state = State.Inactive;
    private bool markerVisible = true;

    public void SetMarkerVisible(bool visible)
    {
        if (markerVisible == visible) return;
        markerVisible = visible;
        RefreshMarker();
    }

    public bool IsWaiting => state == State.Waiting;
    public bool HasOpenOrder => state == State.Waiting || state == State.Pending;

    // Point d'arrivée du crousty
    public Vector3 DropPosition => dropTarget != null ? dropTarget.position : transform.position;

    private void Awake()
    {
        if (marker == null)
            marker = JojoVisuals.CreateBeacon(transform, markerColor, beamHeight, beamWidth);

        RefreshMarker();
    }

    public void SetOrderActive(bool active)
    {
        state = active ? State.Waiting : State.Inactive;
        // Nouvelle commande : le cercle change de hauteur
        if (active) PickRingHeight();
        RefreshMarker();
    }

    public void MarkPending()
    {
        state = State.Pending;
        RefreshMarker();
    }

    public void CompleteDelivery()
    {
        state = State.Delivered;
        RefreshMarker();
    }

    // Vrai si la position est assez près de l'adresse
    public bool IsInRange(Vector3 position)
    {
        return JojoVisuals.IsClose(transform.position, position, deliveryRadius, maxHeightDifference);
    }

    // Comme IsInRange, mais sans regarder la hauteur (livraison depuis le ciel)
    public bool IsInRangeAnyHeight(Vector3 position)
    {
        return JojoVisuals.IsClose(transform.position, position, deliveryRadius, float.PositiveInfinity);
    }

    // Appelé à chaque image par le DeliveryManager quand les cercles sont activés.
    // show = le bus vole et peut livrer. Renvoie vrai quand le bus vient de passer dans le cercle.
    public bool UpdateRing(Vector3 busPosition, bool show)
    {
        if (!show || state != State.Waiting)
        {
            if (ring != null) ring.gameObject.SetActive(false);
            ringPrimed = false;
            return false;
        }

        if (ring == null)
        {
            ring = JojoVisuals.CreateRing(transform, markerColor, ringRadius, ringThickness).transform;
            // Taille en mètres, même si l'adresse est agrandie ou réduite
            Vector3 scale = transform.lossyScale;
            ring.localScale = new Vector3(1f / Mathf.Max(0.001f, scale.x), 1f / Mathf.Max(0.001f, scale.y), 1f / Mathf.Max(0.001f, scale.z));
        }
        ring.gameObject.SetActive(true);
        ring.position = RingCenter;

        // Tant que le bus est loin, le cercle se tourne vers lui : on peut arriver de n'importe quel côté.
        // De près il ne bouge plus, sinon on ne pourrait jamais passer au travers.
        Vector3 toBus = busPosition - RingCenter;
        Vector3 flat = new Vector3(toBus.x, 0f, toBus.z);
        if (flat.magnitude > ringRadius * 3f)
            ring.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

        // Le bus a changé de côté du cercle, et il est passé dans le trou
        float side = Vector3.Dot(toBus, ring.forward);
        bool crossed = ringPrimed && side * lastRingSide <= 0f && lastRingSide != 0f &&
            Vector3.ProjectOnPlane(toBus, ring.forward).magnitude <= ringRadius;
        lastRingSide = side;
        ringPrimed = true;
        return crossed;
    }

    // Le faisceau n'est visible que quand une commande attend
    private void RefreshMarker()
    {
        if (marker != null)
            marker.SetActive(state == State.Waiting && markerVisible);
        if (state != State.Waiting || !markerVisible)
        {
            if (ring != null) ring.gameObject.SetActive(false);
            ringPrimed = false;
        }
    }

    // Dessine la zone dans la vue Scene
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, deliveryRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(RingCenter, ringRadius);
    }
}
