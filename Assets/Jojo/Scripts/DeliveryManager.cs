using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère les commandes : chargement au magasin, livraison automatique, compteur.
// A mettre sur un GameObject vide de la scène (ex : "DeliveryManager").
public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    [Header("Commandes")]
    [Tooltip("Nombre de commandes de la toute première tournée")]
    [Min(1)] public int firstRunOrders = 3;
    [Tooltip("Ensuite : 1 x pas, 2 x pas, 3 x pas... (avec 5 : 5, 10, 15...)")]
    [Min(1)] public int ordersStep = 5;

    [Header("Références (auto si vide)")]
    public Transform player;
    public CroustyShop shop;
    public List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();

    [Header("Livraison")]
    public float throwDuration = 0.8f;
    public float throwArcHeight = 4f;
    public float throwStartHeight = 2.5f;

    [Tooltip("Optionnel : objet lancé. Si vide, un cube orange est créé.")]
    public GameObject croustyProjectilePrefab;

    // Infos lues par le HUD
    public int CroustyLeft { get; private set; }
    public int OrdersTotal { get; private set; }
    public int Delivered { get; private set; }
    public bool HasStarted { get; private set; }

    private int runsLoaded;
    private int inFlight;

    public bool AllDelivered =>
        HasStarted && CroustyLeft == 0 && inFlight == 0;

    // Il faut passer au magasin : début de partie ou tournée terminée
    public bool NeedsPickup => !HasStarted || AllDelivered;

    private void Awake()
    {
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
            NeedsPickup)
        {
            LoadOrders();
        }

        // LIVRAISON (automatique en passant à côté d'une adresse)
        DeliveryPoint target = FindDeliverablePoint();
        if (target != null)
            ThrowCrousty(target);
    }

    // 3, puis 5, 10, 15, 20...
    private int OrdersForRun(int run)
    {
        return run == 0 ? firstRunOrders : ordersStep * run;
    }

    private void LoadOrders()
    {
        // Désactive les anciens points
        foreach (DeliveryPoint point in deliveryPoints)
            point.SetOrderActive(false);

        if (deliveryPoints.Count == 0)
        {
            Debug.LogWarning("DeliveryManager : aucun DeliveryPoint dans la scène.");
            return;
        }

        int wanted = OrdersForRun(runsLoaded);
        int count = Mathf.Min(wanted, deliveryPoints.Count);

        if (count < wanted)
            Debug.LogWarning(
                $"DeliveryManager : seulement {deliveryPoints.Count} points de livraison " +
                $"pour {wanted} commandes, on en prend {count}."
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
        Delivered = 0;
        inFlight = 0;
        HasStarted = true;
        runsLoaded++;
    }

    private DeliveryPoint FindDeliverablePoint()
    {
        if (CroustyLeft <= 0)
            return null;

        DeliveryPoint best = null;
        float bestDistance = float.MaxValue;

        foreach (DeliveryPoint point in deliveryPoints)
        {
            if (!point.IsWaiting || !point.IsInRange(player.position))
                continue;

            float distance = Vector3.Distance(player.position, point.transform.position);
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

        StartCoroutine(ThrowRoutine(target));
    }

    private IEnumerator ThrowRoutine(DeliveryPoint target)
    {
        GameObject projectile = CreateProjectile();

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

        Destroy(projectile);

        target.CompleteDelivery();
        inFlight--;
        Delivered++;
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
