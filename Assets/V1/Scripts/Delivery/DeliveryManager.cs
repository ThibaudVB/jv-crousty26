using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace V1
{
    // Gère les commandes : chargement au magasin, livraison (automatique ou avec une touche), compteur.
    // A mettre sur un GameObject vide de la scène (ex : "DeliveryManager").
    public class DeliveryManager : MonoBehaviour
    {
        public static DeliveryManager Instance { get; private set; }

        [Header("Commandes")]
        [Min(1)]
        [SerializeField] private int ordersPerRun = 10;

        [Header("Références (auto si vide)")]
        [SerializeField] private Transform player;
        [SerializeField] private CroustyShop shop;
        [SerializeField] private List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();

        [Header("Livraison")]
        [Tooltip("Livre tout seul en passant à côté d'une adresse. Décoché : il faut appuyer sur la touche.")]
        [SerializeField] private bool autoDeliver = true;
        [SerializeField] private Key deliverKey = Key.E;
        [SerializeField] private float throwDuration = 0.8f;
        [SerializeField] private float throwArcHeight = 4f;
        [SerializeField] private float throwStartHeight = 2.5f;

        [Tooltip("Optionnel : objet lancé. Si vide, un cube orange est créé.")]
        [SerializeField] private GameObject croustyProjectilePrefab;

        // Prévient le HUD quand les compteurs changent
        public event Action Changed;

        // Infos lues par le HUD
        public Transform Player => player;
        public CroustyShop Shop => shop;
        public IReadOnlyList<DeliveryPoint> Points => deliveryPoints;
        public bool AutoDeliver => autoDeliver;
        public Key DeliverKey => deliverKey;

        public int CroustyLeft { get; private set; }
        public int OrdersTotal { get; private set; }
        public bool HasStarted { get; private set; }
        public DeliveryPoint PointInRange { get; private set; }

        private int inFlight;
        private bool warnedNoPoints;
        private readonly List<GameObject> flyingProjectiles = new List<GameObject>();

        public bool AllDelivered =>
            HasStarted && CroustyLeft == 0 && inFlight == 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("DeliveryManager : il y en a déjà un dans la scène, celui-ci le remplace.");
            Instance = this;

            if (player == null)
            {
                BusController bus = FindAnyObjectByType<BusController>();
                if (bus != null)
                    player = bus.transform;
            }

            if (shop == null)
                shop = FindAnyObjectByType<CroustyShop>();

            if (deliveryPoints.Count == 0)
                deliveryPoints.AddRange(
                    FindObjectsByType<DeliveryPoint>()
                );

            if (player == null)
                Debug.LogWarning("DeliveryManager : aucun joueur (BusController) trouvé.");
        }

        // Si le manager est coupé pendant un lancer, les coroutines s'arrêtent :
        // on termine les livraisons en cours pour que la tournée ne reste pas bloquée.
        private void OnDisable()
        {
            StopAllCoroutines();

            foreach (GameObject projectile in flyingProjectiles)
                if (projectile != null)
                    Destroy(projectile);
            flyingProjectiles.Clear();

            foreach (DeliveryPoint point in deliveryPoints)
                if (point != null && point.IsPending)
                    point.CompleteDelivery();
            inFlight = 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (player == null)
                return;

            // RECUPERATION AU MAGASIN (automatique)
            if (shop != null &&
                shop.IsInRange(player.position) &&
                CanLoadOrders())
            {
                LoadOrders();
            }

            // LIVRAISON
            PointInRange = FindDeliverablePoint();

            if (PointInRange != null && (autoDeliver || DeliverKeyPressed()))
            {
                ThrowCrousty(PointInRange);
            }
        }

        private bool DeliverKeyPressed()
        {
            return Keyboard.current != null &&
                   Keyboard.current[deliverKey].wasPressedThisFrame;
        }

        private bool CanLoadOrders()
        {
            // On ne recharge que quand la tournée précédente est terminée
            return !HasStarted || AllDelivered;
        }

        private void LoadOrders()
        {
            // Retire les adresses détruites ou les cases vides de la liste
            deliveryPoints.RemoveAll(point => point == null);

            if (deliveryPoints.Count == 0)
            {
                if (!warnedNoPoints)
                    Debug.LogWarning("DeliveryManager : aucun DeliveryPoint dans la scène.");
                warnedNoPoints = true;
                return;
            }

            // Désactive les anciens points
            foreach (DeliveryPoint point in deliveryPoints)
                point.SetOrderActive(false);

            int count = Mathf.Min(ordersPerRun, deliveryPoints.Count);

            if (count < ordersPerRun)
                Debug.LogWarning(
                    $"DeliveryManager : seulement {deliveryPoints.Count} points de livraison " +
                    $"pour {ordersPerRun} commandes, on en prend {count}."
                );

            // Tirage au hasard des adresses
            List<DeliveryPoint> pool = new List<DeliveryPoint>(deliveryPoints);
            for (int i = 0; i < count; i++)
            {
                int index = Random.Range(i, pool.Count);
                (pool[i], pool[index]) = (pool[index], pool[i]);
                pool[i].SetOrderActive(true);
            }

            OrdersTotal = count;
            CroustyLeft = count;
            inFlight = 0;
            HasStarted = true;
            Changed?.Invoke();
        }

        private DeliveryPoint FindDeliverablePoint()
        {
            if (CroustyLeft <= 0)
                return null;

            DeliveryPoint best = null;
            float bestDistance = float.MaxValue;

            foreach (DeliveryPoint point in deliveryPoints)
            {
                if (point == null || !point.IsWaiting || !point.IsInRange(player.position))
                    continue;

                float distance = point.DistanceTo(player.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private void ThrowCrousty(DeliveryPoint target)
        {
            CroustyLeft--;
            inFlight++;
            target.MarkPending();
            Changed?.Invoke();

            StartCoroutine(ThrowRoutine(target));
        }

        private IEnumerator ThrowRoutine(DeliveryPoint target)
        {
            GameObject projectile = CreateProjectile();
            flyingProjectiles.Add(projectile);

            Vector3 start = player.position + Vector3.up * throwStartHeight;
            Vector3 end = target.DropPosition;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, throwDuration);

                // Trajectoire en cloche, arrive toujours sur la cible
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * throwArcHeight;

                projectile.transform.position = position;
                projectile.transform.Rotate(360f * Time.deltaTime, 540f * Time.deltaTime, 0f);

                yield return null;
            }

            flyingProjectiles.Remove(projectile);
            Destroy(projectile);

            // L'adresse a pu être détruite pendant le vol
            if (target != null)
                target.CompleteDelivery();
            inFlight--;
            Changed?.Invoke();
        }

        private GameObject CreateProjectile()
        {
            if (croustyProjectilePrefab != null)
            {
                GameObject instance = Instantiate(croustyProjectilePrefab);
                foreach (Collider c in instance.GetComponentsInChildren<Collider>())
                    c.enabled = false;
                return instance;
            }

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Crousty (projectile)";
            cube.transform.localScale = Vector3.one * 0.5f;
            Destroy(cube.GetComponent<Collider>());
            JojoVisuals.SetColor(cube, new Color(1f, 0.6f, 0.1f));
            return cube;
        }
    }
}
