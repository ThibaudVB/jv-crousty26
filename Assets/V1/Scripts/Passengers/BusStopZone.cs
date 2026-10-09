using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace V1
{
    // Arrêt de bus : quand le bus s'arrête dans la zone, les passagers montent un par un.
    // Couleur de la zone : rouge = bus pas arrêté, vert = montée possible, gris = arrêt vide.
    [RequireComponent(typeof(Collider))]
    public class BusStopZone : MonoBehaviour
    {
        [Header("Passagers")]
        public List<BusStopPassenger> waitingPassengers = new List<BusStopPassenger>();
        public float delayBetweenPassengers = 0.5f;

        [Header("Arrêt du bus")]
        [Tooltip("Vitesse en dessous de laquelle le bus est considéré à l'arrêt")]
        public float stopSpeedKmh = 1f;
        [Tooltip("Temps (s) pendant lequel le bus doit rester à l'arrêt")]
        public float stopDuration = 0.5f;
        [Tooltip("Le centre du bus doit être dans la zone")]
        public bool requireBusCenterInside = true;

        [Header("Visuel (optionnel)")]
        public Renderer zoneRenderer;
        public Color waitingColor = new Color(1f, 0f, 0f, 0.5f);
        public Color readyColor = new Color(0f, 1f, 0f, 0.5f);
        public Color emptyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // Déclenché quand tous les passagers de l'arrêt sont montés
        public event Action<BusStopZone> AllBoarded;

        public bool IsEmpty { get; private set; }

        private Collider zoneCollider;
        private BusController busInside;
        private Rigidbody busBody;
        private float stoppedTimer;
        private bool boarding;
        private Material zoneMaterial;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;

            // Une seule copie du matériau, réutilisée à chaque changement de couleur
            if (zoneRenderer != null)
                zoneMaterial = zoneRenderer.material;

            SetColor(waitingColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            BusController bus = other.GetComponentInParent<BusController>();
            if (bus == null)
                return;

            busInside = bus;
            busBody = bus.GetComponent<Rigidbody>();
            stoppedTimer = 0f;
        }

        private void OnTriggerExit(Collider other)
        {
            BusController bus = other.GetComponentInParent<BusController>();
            if (bus == null || bus != busInside)
                return;

            busInside = null;
            busBody = null;
            stoppedTimer = 0f;
            if (!boarding && !IsEmpty)
                SetColor(waitingColor);
        }

        private void Update()
        {
            if (boarding || IsEmpty || busInside == null)
                return;

            if (IsBusStoppedInZone())
            {
                stoppedTimer += Time.deltaTime;
                SetColor(readyColor);

                if (stoppedTimer >= stopDuration)
                    StartCoroutine(BoardRoutine(busInside));
            }
            else
            {
                stoppedTimer = 0f;
                SetColor(waitingColor);
            }
        }

        private bool IsBusStoppedInZone()
        {
            if (busBody == null)
                return false;

            Vector3 v = busBody.linearVelocity;
            v.y = 0f;
            if (v.magnitude * 3.6f > stopSpeedKmh)
                return false;

            if (requireBusCenterInside)
            {
                Bounds b = zoneCollider.bounds;
                Vector3 p = busInside.transform.position;
                p.y = b.center.y; // on ignore la hauteur
                if (!b.Contains(p))
                    return false;
            }

            return true;
        }

        private IEnumerator BoardRoutine(BusController bus)
        {
            boarding = true;

            Transform door = bus.transform.Find("BusDoor");
            if (door == null)
                door = bus.transform;

            BusPassengers counter = bus.GetComponent<BusPassengers>();
            if (counter == null)
                counter = bus.gameObject.AddComponent<BusPassengers>();

            foreach (BusStopPassenger p in waitingPassengers.ToArray())
            {
                if (p == null)
                    continue;
                p.BoardBus(door, counter.AddPassenger);
                yield return new WaitForSeconds(delayBetweenPassengers);
            }

            waitingPassengers.Clear();
            boarding = false;
            IsEmpty = true;
            SetColor(emptyColor);
            AllBoarded?.Invoke(this);
        }

        private void SetColor(Color c)
        {
            if (zoneMaterial != null)
                zoneMaterial.color = c;
        }

        private void OnDestroy()
        {
            // Le matériau copié n'est pas détruit tout seul
            if (zoneMaterial != null)
                Destroy(zoneMaterial);
        }
    }
}
