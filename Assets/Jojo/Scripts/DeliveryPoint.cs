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
    private enum State { Inactive, Waiting, Pending, Delivered }
    private State state = State.Inactive;

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

    // Le faisceau n'est visible que quand une commande attend
    private void RefreshMarker()
    {
        if (marker != null)
            marker.SetActive(state == State.Waiting);
    }

    // Dessine la zone dans la vue Scene
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, deliveryRadius);
    }
}
