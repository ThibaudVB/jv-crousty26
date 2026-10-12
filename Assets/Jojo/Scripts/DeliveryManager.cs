using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Gère les commandes : chargement au magasin, livraison avec E, compteur.
// A mettre sur un GameObject vide de la scène (ex : "DeliveryManager").
public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    [Header("Commandes")]
    [Min(1)] public int ordersPerRun = 6;
    [Min(10f)] public float runTimeLimit = 90f;
    [Min(6)] public int nearbyAddressPool = 8;
    [Min(0)] public int pointsPerDelivery = 100;
    [Min(0)] public int completionBonus = 500;
    [Min(0)] public int pointsPerRemainingSecond = 10;

    [Header("Références (auto si vide)")]
    public Transform player;
    public CroustyShop shop;
    public List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();

    [Header("Livraison")]
    public Key deliverKey = Key.E;
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
    public DeliveryPoint PointInRange { get; private set; }
    public bool RunActive { get; private set; }
    public bool HasFailed { get; private set; }
    public float RunElapsed { get; private set; }
    public float RunRemaining => RunActive ? Mathf.Max(0,runTimeLimit - RunElapsed) : 0;
    public int Score { get; private set; }
    public int LastRunScore { get; private set; }
    public int BestScore => PlayerPrefs.GetInt("Crousty.BestScore",0);
    public event System.Action<int,int> ScoreChanged;
    public event System.Action RunStarted;
    public event System.Action RunCompleted;

    public event System.Action<DeliveryPoint> DeliveryCompleted;

    private int inFlight;
    private PoliceGameOverManager gameOver;
    private bool alarmPlayed, mustLeaveShop;

    public bool AllDelivered =>
        HasStarted && !HasFailed && Delivered == OrdersTotal && CroustyLeft == 0 && inFlight == 0;

    private void Awake()
    {
        Instance = this;
        gameOver = FindAnyObjectByType<PoliceGameOverManager>();

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
        if (player == null || HasFailed || Time.timeScale <= 0f)
            return;
        if (gameOver != null && gameOver.IsGameOver) { FailRun(false); return; }
        AdvanceRun(Time.deltaTime);
        if (HasFailed) return;
        if (shop == null || !shop.IsInRange(player.position)) mustLeaveShop = false;

        // RECUPERATION AU MAGASIN (automatique)
        if (shop != null &&
            shop.IsInRange(player.position) &&
            CanLoadOrders())
        {
            LoadOrders();
        }

        // LIVRAISON
        PointInRange = FindDeliverablePoint();

        if (PointInRange != null &&
            Keyboard.current != null &&
            Keyboard.current[deliverKey].wasPressedThisFrame)
        {
            ThrowCrousty(PointInRange);
        }
    }

    private bool CanLoadOrders()
    {
        // On ne recharge que quand la tournée précédente est terminée
        return !HasFailed && !RunActive && (!HasStarted || AllDelivered && !mustLeaveShop);
    }

    private void AdvanceRun(float seconds)
    {
        if (!RunActive || HasFailed) return;
        RunElapsed += Mathf.Max(0,seconds);
        if (RunElapsed >= runTimeLimit) { FailRun(true); return; }
        if (!alarmPlayed && RunRemaining <= 15f)
        {
            alarmPlayed = true;
            CroustySoundDesignManager.Play(CroustySound.Alarm);
        }
    }
    public void EndRunOnCapture() => FailRun(false);
    private void FailRun(bool timeout)
    {
        if (HasFailed) return;
        HasFailed = true;
        RunActive = false;
        PointInRange = null;
        SaveBestScore();
        if (timeout)
        {
            if (gameOver == null) gameOver = FindAnyObjectByType<PoliceGameOverManager>();
            if (gameOver != null) gameOver.TriggerTimeoutGameOver();
            else { Time.timeScale = 0; Debug.LogError("DeliveryManager: ecran de defaite manquant.",this); }
        }
    }
    private void SaveBestScore()
    {
        if (Score > BestScore) { PlayerPrefs.SetInt("Crousty.BestScore",Score); PlayerPrefs.Save(); }
    }
    private void AwardScore(int points)
    {
        if (points <= 0) return;
        Score += points;
        LastRunScore += points;
        ScoreChanged?.Invoke(Score,points);
    }

    public void RegisterPoint(DeliveryPoint point)
    {
        if (point != null && !deliveryPoints.Contains(point)) deliveryPoints.Add(point);
    }

    public void UnregisterPoint(DeliveryPoint point)
    {
        // Chunks containing waiting orders or airborne parcels must remain loaded.
        if (point != null && point.HasOpenOrder) return;
        deliveryPoints.Remove(point);
    }

    private void LoadOrders()
    {
        if (HasFailed) return;
        deliveryPoints.RemoveAll(point => point == null || !point.isActiveAndEnabled);
        if (deliveryPoints.Count < ordersPerRun) return;
        // Désactive les anciens points
        foreach (DeliveryPoint point in deliveryPoints)
            point.SetOrderActive(false);

        if (deliveryPoints.Count == 0)
        {
            Debug.LogWarning("DeliveryManager : aucun DeliveryPoint dans la scène.");
            return;
        }

        int count = Mathf.Min(ordersPerRun, deliveryPoints.Count);

        if (count < ordersPerRun)
            Debug.LogWarning(
                $"DeliveryManager : seulement {deliveryPoints.Count} points de livraison " +
                $"pour {ordersPerRun} commandes, on en prend {count}."
            );

        // Tirage au hasard des adresses
        var ordered = new List<DeliveryPoint>(deliveryPoints);
        ordered.Sort((a,b) => (a.transform.position - player.position).sqrMagnitude.CompareTo((b.transform.position - player.position).sqrMagnitude));
        var pool = new List<DeliveryPoint>();
        var blocks = new HashSet<Vector2Int>();
        var city = FindAnyObjectByType<ProceduralCity>();
        foreach (var point in ordered)
        {
            // Do not deliver two meals immediately inside the pickup radius, or
            // choose both sides of the same procedural block for a six-stop sprint.
            if (shop != null && (point.transform.position - shop.transform.position).sqrMagnitude <
                Mathf.Pow(shop.pickupRadius + point.deliveryRadius + 2f,2)) continue;
            if (city != null && city.WorldRoot != null && point.transform.IsChildOf(city.WorldRoot))
            {
                Vector3 p = point.transform.position - city.Origin;
                var block = new Vector2Int(Mathf.FloorToInt(p.x / ProceduralCity.BlockSize),Mathf.FloorToInt(p.z / ProceduralCity.BlockSize));
                if (!blocks.Add(block)) continue;
            }
            pool.Add(point);
            if (pool.Count >= Mathf.Max(count,nearbyAddressPool)) break;
        }
        if (pool.Count < count) return;
        int poolSize = pool.Count;
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(i, poolSize);
            (pool[i], pool[index]) = (pool[index], pool[i]);
            pool[i].SetOrderActive(true);
        }

        OrdersTotal = count;
        CroustyLeft = count;
        Delivered = 0;
        inFlight = 0;
        HasStarted = true;
        RunElapsed = 0;
        RunActive = true;
        LastRunScore = 0;
        alarmPlayed = false;
        mustLeaveShop = true;
        RunStarted?.Invoke();
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
        if (!RunActive || HasFailed || CroustyLeft <= 0 || target == null || !target.IsWaiting) return;
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
            if (HasFailed || target == null) { Destroy(projectile); inFlight = Mathf.Max(0,inFlight - 1); yield break; }
            t += Time.deltaTime / Mathf.Max(0.01f, throwDuration);

            // Trajectoire en cloche, arrive toujours sur la cible
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * throwArcHeight;

            projectile.transform.position = position;
            projectile.transform.Rotate(360f * Time.deltaTime, 540f * Time.deltaTime, 0f);

            yield return null;
        }

        Destroy(projectile);
        if (HasFailed || target == null) { inFlight = Mathf.Max(0,inFlight - 1); yield break; }
        target.CompleteDelivery();
        inFlight--;
        Delivered++;
        AwardScore(pointsPerDelivery);
        CroustySoundDesignManager.Play(CroustySound.DeliveryCoin);
        if (Delivered == OrdersTotal)
        {
            int bonus = completionBonus + Mathf.FloorToInt(Mathf.Max(0,runTimeLimit - RunElapsed)) * pointsPerRemainingSecond;
            RunActive = false;
            AwardScore(bonus);
            SaveBestScore();
            CroustySoundDesignManager.Play(CroustySound.RunComplete);
            RunCompleted?.Invoke();
        }
        DeliveryCompleted?.Invoke(target);
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
