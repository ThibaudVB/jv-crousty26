using UnityEngine;

// Adresse de livraison. Le DeliveryManager en choisit certaines au hasard.
// Seules les adresses avec une commande en attente affichent leur marqueur.
public class DeliveryPoint : MonoBehaviour
{
    public float deliveryRadius = 10f;
    [Tooltip("Écart vertical maximal pour livrer depuis la rue, sans atteindre un autre étage.")]
    [Min(0f)] public float maxHeightDifference = 6f;

    [Tooltip("Optionnel : endroit où le crousty atterrit (porte, boîte aux lettres...). Sinon, ce point.")]
    public Transform dropTarget;

    [Header("Marqueur")]
    [Tooltip("Optionnel : objet affiché quand une commande attend ici. Si vide, un faisceau lumineux vert est créé.")]
    public GameObject marker;
    public Color markerColor = new Color(0.2f, 1f, 0.3f);
    public float beamHeight = 150f;
    public float beamWidth = 2.5f;

    private enum State { Inactive, Waiting, Pending, Delivered }
    private State state = State.Inactive;

    public bool IsWaiting => state == State.Waiting;
    public bool HasOpenOrder => state == State.Waiting || state == State.Pending;

    public Vector3 DropPosition =>
        dropTarget != null ? dropTarget.position : transform.position;

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

    // Le crousty est en l'air vers ce point
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

    public bool IsInRange(Vector3 position)
    {
        if (Mathf.Abs(position.y - transform.position.y) > maxHeightDifference)
            return false;
        position.y = 0f;
        Vector3 here = transform.position;
        here.y = 0f;
        return Vector3.Distance(position, here) <= deliveryRadius;
    }

    private void RefreshMarker()
    {
        if (marker != null)
            marker.SetActive(state == State.Waiting);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, deliveryRadius);
    }
}
