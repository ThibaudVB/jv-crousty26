using UnityEngine;

namespace Jojo
{
    // Adresse de livraison. Le DeliveryManager en choisit certaines au hasard.
    // Seules les adresses avec une commande en attente affichent leur marqueur.
    public class DeliveryPoint : BeaconZone
    {
        [Tooltip("Optionnel : endroit où le crousty atterrit (porte, boîte aux lettres...). Sinon, ce point.")]
        [SerializeField] private Transform dropTarget;

        private enum State { Inactive, Waiting, Pending, Delivered }
        private State state = State.Inactive;

        public bool IsWaiting => state == State.Waiting;
        public bool IsPending => state == State.Pending;

        public Vector3 DropPosition =>
            dropTarget != null ? dropTarget.position : transform.position;

        protected override void Awake()
        {
            base.Awake();
            RefreshMarker();
        }

        // Valeurs par défaut quand on ajoute le script dans l'éditeur
        private void Reset()
        {
            radius = 10f;
            markerColor = new Color(0.2f, 1f, 0.3f);
            beamWidth = 2.5f;
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

        private void RefreshMarker()
        {
            if (marker != null)
                marker.SetActive(state == State.Waiting);
        }
    }
}
