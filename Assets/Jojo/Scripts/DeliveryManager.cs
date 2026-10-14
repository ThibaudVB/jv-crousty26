using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère les tournées de livraison :
// 1. Le bus passe au magasin (CroustyShop) : il charge des commandes et le chrono démarre.
// 2. Il passe près d'une adresse qui attend (DeliveryPoint) : le crousty est lancé tout seul.
// 3. Chaque livraison rapporte des points. Tout livrer avant la fin du chrono donne un bonus.
// 4. On retourne au magasin pour la tournée suivante (3 commandes, puis 5, 10, 15...).
//
// A placer à la main sur un GameObject vide de la scène (ex : "DeliveryManager").
// Le HUD, le gérant, la mini map et le GPS lisent ses infos.
public class DeliveryManager : MonoBehaviour
{
    // Accès depuis n'importe quel script : DeliveryManager.Instance
    public static DeliveryManager Instance { get; private set; }

    [Header("Références (cherchées automatiquement si vides)")]
    [Tooltip("Le bus du joueur")]
    public Transform player;
    [Tooltip("Le magasin où on charge les commandes")]
    public CroustyShop shop;
    [Tooltip("Toutes les adresses de livraison possibles")]
    public List<DeliveryPoint> deliveryPoints = new List<DeliveryPoint>();

    [Header("Commandes")]
    [Tooltip("Nombre de commandes de la toute première tournée")]
    [Min(1)] public int firstRunOrders = 3;
    [Tooltip("Ensuite : 1 x pas, 2 x pas, 3 x pas... (avec 5 : 5, 10, 15...)")]
    [Min(1)] public int ordersStep = 5;
    [Tooltip("Nombre d'adresses proches parmi lesquelles on tire au hasard")]
    [Min(6)] public int nearbyAddressPool = 8;

    [Header("Chrono")]
    [Tooltip("Temps accordé par commande (3 commandes x 15 s = 45 s)")]
    [Min(1f)] public float secondsPerOrder = 15f;
    [Tooltip("Une alarme sonne quand il reste ce nombre de secondes")]
    [Min(0f)] public float alarmSecondsLeft = 15f;

    [Header("Points")]
    [Min(0)] public int pointsPerDelivery = 100;
    [Tooltip("Bonus quand toutes les commandes de la tournée sont livrées")]
    [Min(0)] public int completionBonus = 500;
    [Tooltip("Points en plus pour chaque seconde restante à la fin de la tournée")]
    [Min(0)] public int pointsPerRemainingSecond = 10;

    [Header("Livraison en hauteur")]
    [Tooltip("Coché : on peut livrer à n'importe quelle hauteur (en volant, depuis un toit...). " +
             "Décoché : le bus doit être à moins de \"Max Height Difference\" de l'adresse.")]
    public bool deliverFromAnyHeight = true;

    [Header("Parachute")]
    [Tooltip("Le crousty ouvre un parachute quand il est lancé de haut")]
    public bool useParachute = true;
    [Tooltip("Le parachute s'ouvre si le crousty part de plus haut que ça au-dessus de l'adresse (m)")]
    [Min(0f)] public float parachuteMinHeight = 8f;
    [Tooltip("Vitesse de descente sous le parachute (m/s)")]
    [Min(0.5f)] public float parachuteFallSpeed = 6f;
    [Tooltip("Durée maximale de la descente (s), même lancé de très haut")]
    [Min(0.5f)] public float parachuteMaxSeconds = 6f;
    [Tooltip("Largeur de la toile du parachute (m)")]
    public float parachuteSize = 2.5f;
    public Color parachuteColor = new Color(0.9f, 0.15f, 0.15f);
    [Tooltip("Balancement du crousty sous le parachute (degrés)")]
    public float parachuteSwing = 15f;

    [Header("Lancer du crousty")]
    [Tooltip("Durée du vol du crousty jusqu'à l'adresse (s)")]
    public float throwDuration = 0.8f;
    [Tooltip("Hauteur de la courbe (m)")]
    public float throwArcHeight = 4f;
    [Tooltip("Hauteur de départ au-dessus du bus (m)")]
    public float throwStartHeight = 2.5f;
    [Tooltip("Optionnel : objet lancé. Si vide, un cube orange est créé.")]
    public GameObject croustyProjectilePrefab;
    [Tooltip("Couleur du cube si aucun objet n'est donné")]
    public Color projectileColor = new Color(1f, 0.6f, 0.1f);
    [Tooltip("Taille de l'objet lancé (m) : le modèle est mis à cette taille")]
    public float projectileSize = 0.5f;

    // ---------- Infos lues par les autres scripts (HUD, gérant...) ----------

    public int OrdersTotal { get; private set; }   // commandes de la tournée en cours
    public int CroustyLeft { get; private set; }   // crousty encore dans le bus
    public int Delivered { get; private set; }     // crousty arrivés à destination
    public int TotalDelivered { get; private set; } // livraisons réussies sur toute la partie
    public bool HasStarted { get; private set; }   // au moins une tournée a été chargée
    public bool RunActive { get; private set; }    // une tournée est en cours (chrono lancé)
    public bool HasFailed { get; private set; }    // partie perdue (chrono ou police)
    public float RunElapsed { get; private set; }  // secondes écoulées dans la tournée
    public int Score { get; private set; }
    public string PlayerName { get; private set; }
    public int LastRunScore { get; private set; }  // points gagnés pendant la tournée en cours
    public int BestScore { get; private set; }

    // Durée de la tournée en cours (ou de la prochaine si on n'a pas encore commencé)
    public float RunTimeLimit => secondsPerOrder * (HasStarted ? OrdersTotal : NextRunOrders);
    public float RunRemaining => RunActive ? Mathf.Max(0f, RunTimeLimit - RunElapsed) : 0f;

    // Commandes de la prochaine tournée : 3, puis 5, 10, 15...
    public int NextRunOrders => runsLoaded == 0 ? firstRunOrders : ordersStep * runsLoaded;

    // Tout est livré (aucun crousty restant ni en l'air)
    public bool AllDelivered =>
        HasStarted && !HasFailed && Delivered == OrdersTotal && CroustyLeft == 0 && inFlight == 0;

    // Il faut passer au magasin : début de partie, ou tournée terminée
    public bool NeedsPickup => !HasFailed && !RunActive && (!HasStarted || AllDelivered);

    // Événements : d'autres scripts peuvent s'y abonner avec +=
    public event System.Action<int, int> ScoreChanged;            // (score total, points gagnés)
    public event System.Action RunStarted;
    public event System.Action RunCompleted;
    public event System.Action<DeliveryPoint> DeliveryCompleted;

    // ---------- Variables internes ----------

    private const string BestScoreKey = "Crousty.BestScore";
    private int runsLoaded;          // nombre de tournées déjà chargées
    private int inFlight;            // crousty en train de voler
    private bool alarmPlayed;
    private bool mustLeaveShop;      // après un chargement, il faut sortir du magasin avant de recharger
    private PoliceGameOverManager gameOver;

    private void Awake()
    {
        Instance = this;
        PlayerName = CroustyPlayerProfile.PlayerName;
        BestScore = CroustyLeaderboardScores.BestForPlayer(PlayerName);
        gameOver = FindAnyObjectByType<PoliceGameOverManager>();

        // Si une référence n'est pas remplie dans l'Inspector, on la cherche dans la scène
        if (player == null)
        {
            BusController bus = FindAnyObjectByType<BusController>();
            if (bus != null)
                player = bus.transform;
            else
                Debug.LogWarning("DeliveryManager : aucun bus (BusController) trouvé.", this);
        }

        if (shop == null)
            shop = FindAnyObjectByType<CroustyShop>();

        if (deliveryPoints.Count == 0)
            deliveryPoints.AddRange(FindObjectsByType<DeliveryPoint>());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Jeu en pause, partie perdue ou pas de bus : on ne fait rien
        if (player == null || HasFailed || Time.timeScale <= 0f)
            return;

        // Le bus s'est fait attraper par la police
        if (gameOver != null && gameOver.IsGameOver)
        {
            FailRun(false);
            return;
        }

        UpdateTimer();
        if (HasFailed)
            return;

        // 1. Chargement au magasin
        bool atShop = shop != null && shop.IsInRange(player.position);
        if (!atShop)
            mustLeaveShop = false;
        if (atShop && NeedsPickup && !mustLeaveShop)
            LoadOrders();

        // 2. Livraison en passant devant une adresse
        DeliveryPoint target = FindDeliverablePoint();
        if (target != null)
            ThrowCrousty(target);
    }

    // ---------- CHRONO ET POINTS ----------

    private void UpdateTimer()
    {
        if (!RunActive)
            return;

        RunElapsed += Time.deltaTime;

        if (RunElapsed >= RunTimeLimit)
        {
            FailRun(true);
            return;
        }

        if (!alarmPlayed && RunRemaining <= alarmSecondsLeft)
        {
            alarmPlayed = true;
            CroustySoundDesignManager.Play(CroustySound.Alarm);
        }
    }

    // Appelé par la police quand elle attrape le bus
    public void EndRunOnCapture()
    {
        FailRun(false);
    }

    private void FailRun(bool timeIsUp)
    {
        if (HasFailed)
            return;

        HasFailed = true;
        RunActive = false;
        SaveBestScore();

        // Temps écoulé : on affiche l'écran de défaite
        if (timeIsUp)
        {
            if (gameOver == null)
                gameOver = FindAnyObjectByType<PoliceGameOverManager>();

            if (gameOver != null)
            {
                gameOver.TriggerTimeoutGameOver();
            }
            else
            {
                Time.timeScale = 0f;
                Debug.LogError("DeliveryManager : écran de défaite (PoliceGameOverManager) manquant.", this);
            }
        }
    }

    private void AddScore(int points)
    {
        if (points <= 0)
            return;

        Score += points;
        LastRunScore += points;
        ScoreChanged?.Invoke(Score, points);
    }

    private void SaveBestScore()
    {
        if (Score > BestScore)
        {
            BestScore = Score;
            CroustyLeaderboardScores.Record(Score, PlayerName);
        }
        // Preserve the old overall statistic for compatible tools and older scenes.
        if (Score > PlayerPrefs.GetInt(BestScoreKey, 0))
        {
            PlayerPrefs.SetInt(BestScoreKey, Score);
            PlayerPrefs.Save();
        }
    }

    // ---------- ADRESSES (utilisé par la ville procédurale) ----------

    public void RegisterPoint(DeliveryPoint point)
    {
        if (point != null && !deliveryPoints.Contains(point))
            deliveryPoints.Add(point);
    }

    public void UnregisterPoint(DeliveryPoint point)
    {
        // Une adresse qui attend encore sa commande doit rester dans la liste
        if (point != null && point.HasOpenOrder)
            return;

        deliveryPoints.Remove(point);
    }

    // ---------- CHARGEMENT DES COMMANDES ----------

    private void LoadOrders()
    {
        // On retire les adresses détruites ou désactivées
        deliveryPoints.RemoveAll(point => point == null || !point.isActiveAndEnabled);
        if (deliveryPoints.Count == 0)
        {
            Debug.LogWarning("DeliveryManager : aucun DeliveryPoint dans la scène.", this);
            return;
        }

        foreach (DeliveryPoint point in deliveryPoints)
            point.SetOrderActive(false);

        int wanted = NextRunOrders;
        List<DeliveryPoint> candidates = FindNearbyAddresses(wanted);
        if (candidates.Count == 0)
            return;

        int count = Mathf.Min(wanted, candidates.Count);
        if (count < wanted)
            Debug.LogWarning($"DeliveryManager : seulement {candidates.Count} adresses pour {wanted} commandes.", this);

        // Tirage au hasard : on prend une adresse, on la retire de la liste, et on recommence
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            candidates[index].SetOrderActive(true);
            candidates.RemoveAt(index);
        }

        // Début de la tournée
        OrdersTotal = count;
        CroustyLeft = count;
        Delivered = 0;
        inFlight = 0;
        LastRunScore = 0;
        RunElapsed = 0f;
        HasStarted = true;
        RunActive = true;
        alarmPlayed = false;
        mustLeaveShop = true;
        runsLoaded++;
        RunStarted?.Invoke();
    }

    // Les adresses les plus proches du bus, sans en prendre deux dans le même pâté de maisons
    private List<DeliveryPoint> FindNearbyAddresses(int wanted)
    {
        List<DeliveryPoint> sorted = new List<DeliveryPoint>(deliveryPoints);
        sorted.Sort((a, b) => DistanceToPlayer(a).CompareTo(DistanceToPlayer(b)));

        int maxCount = Mathf.Max(wanted, nearbyAddressPool);
        List<DeliveryPoint> result = new List<DeliveryPoint>();
        HashSet<Vector2Int> usedBlocks = new HashSet<Vector2Int>();
        ProceduralCity city = FindAnyObjectByType<ProceduralCity>();

        foreach (DeliveryPoint point in sorted)
        {
            // Trop près du magasin : ce serait une livraison sans bouger
            if (shop != null)
            {
                float tooClose = shop.pickupRadius + point.deliveryRadius + 2f;
                if (Vector3.Distance(point.transform.position, shop.transform.position) < tooClose)
                    continue;
            }

            // Dans la ville procédurale : une seule adresse par pâté de maisons
            if (city != null && city.WorldRoot != null && point.transform.IsChildOf(city.WorldRoot))
            {
                Vector3 local = point.transform.position - city.Origin;
                Vector2Int block = new Vector2Int(
                    Mathf.FloorToInt(local.x / ProceduralCity.BlockSize),
                    Mathf.FloorToInt(local.z / ProceduralCity.BlockSize));

                if (usedBlocks.Contains(block))
                    continue;
                usedBlocks.Add(block);
            }

            result.Add(point);
            if (result.Count >= maxCount)
                break;
        }
        return result;
    }

    private float DistanceToPlayer(DeliveryPoint point)
    {
        return Vector3.Distance(point.transform.position, player.position);
    }

    // ---------- LIVRAISON ----------

    // L'adresse qui attend la plus proche du bus, s'il est assez près
    private DeliveryPoint FindDeliverablePoint()
    {
        if (CroustyLeft <= 0)
            return null;

        DeliveryPoint best = null;
        float bestDistance = float.MaxValue;

        foreach (DeliveryPoint point in deliveryPoints)
        {
            if (point == null || !point.IsWaiting)
                continue;

            bool inRange = deliverFromAnyHeight
                ? point.IsInRangeAnyHeight(player.position)
                : point.IsInRange(player.position);
            if (!inRange)
                continue;

            float distance = DistanceToPlayer(point);
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
        if (!RunActive)
            return;

        CroustyLeft--;
        inFlight++;
        target.MarkPending();
        StartCoroutine(ThrowRoutine(target));
    }

    // Coroutine : fait voler le crousty du bus jusqu'à l'adresse, image par image
    private IEnumerator ThrowRoutine(DeliveryPoint target)
    {
        GameObject projectile = CreateProjectile();
        Vector3 start = player.position + Vector3.up * throwStartHeight;
        Vector3 end = target.DropPosition;

        // Lancé de haut : le crousty descend en parachute, plus lentement
        float drop = start.y - end.y;
        bool parachute = useParachute && drop > parachuteMinHeight;
        float duration = throwDuration;
        GameObject canopy = null;
        if (parachute)
        {
            duration = Mathf.Clamp(drop / parachuteFallSpeed, throwDuration, parachuteMaxSeconds);
            canopy = CreateParachute(projectile.transform);
        }

        float t = 0f; // 0 = départ, 1 = arrivée
        while (t < 1f)
        {
            // Partie perdue ou adresse supprimée pendant le vol
            if (HasFailed || target == null)
            {
                Destroy(projectile);
                inFlight--;
                yield break;
            }

            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            t = Mathf.Min(t, 1f);

            if (parachute)
                MoveUnderParachute(projectile.transform, canopy, start, end, t, duration);
            else
                MoveInArc(projectile.transform, start, end, t);

            yield return null; // attend l'image suivante
        }

        Destroy(projectile);
        inFlight--;
        if (HasFailed || target == null)
            yield break;

        // Livré !
        target.CompleteDelivery();
        Delivered++;
        TotalDelivered++;
        AddScore(pointsPerDelivery);
        CroustySoundDesignManager.Play(CroustySound.DeliveryCoin);

        if (Delivered == OrdersTotal)
            CompleteRun();

        DeliveryCompleted?.Invoke(target);
    }

    private void CompleteRun()
    {
        int secondsLeft = Mathf.FloorToInt(RunRemaining);
        RunActive = false;
        AddScore(completionBonus + secondsLeft * pointsPerRemainingSecond);
        SaveBestScore();
        CroustySoundDesignManager.Play(CroustySound.RunComplete);
        RunCompleted?.Invoke();
    }

    // Lancer normal : ligne droite + une bosse en forme de cloche (sinus) = trajectoire en arc
    private void MoveInArc(Transform projectile, Vector3 start, Vector3 end, float t)
    {
        Vector3 position = Vector3.Lerp(start, end, t);
        position.y += Mathf.Sin(t * Mathf.PI) * throwArcHeight;
        projectile.position = position;
        projectile.Rotate(360f * Time.deltaTime, 540f * Time.deltaTime, 0f);
    }

    // Descente en parachute : le crousty glisse vite au-dessus de l'adresse,
    // puis descend tout droit en se balançant
    private void MoveUnderParachute(Transform projectile, GameObject canopy, Vector3 start, Vector3 end, float t, float duration)
    {
        // Horizontal : rapide au début, puis presque immobile (courbe "ease out")
        float side = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.Lerp(start, end, side);
        // Vertical : descente régulière
        position.y = Mathf.Lerp(start.y, end.y, t);
        projectile.position = position;

        // Balancement de gauche à droite
        float swing = Mathf.Sin(t * duration * 3f) * parachuteSwing;
        projectile.rotation = Quaternion.Euler(0f, 0f, swing);

        // La toile s'ouvre en 0.3 seconde
        float open = Mathf.Clamp01(t * duration / 0.3f);
        canopy.transform.localScale = new Vector3(open, 1f, open);
    }

    // Parachute fait de formes simples : une toile (sphère aplatie) et 4 cordes
    private GameObject CreateParachute(Transform crousty)
    {
        // Le parachute ne doit pas grandir si le crousty est un gros objet : on compense son échelle
        GameObject parachute = new GameObject("Parachute");
        parachute.transform.SetParent(crousty, false);
        Vector3 scale = crousty.lossyScale;
        parachute.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, scale.x), 1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));

        float height = parachuteSize; // hauteur de la toile au-dessus du crousty
        GameObject canopy = new GameObject("Toile");
        canopy.transform.SetParent(parachute.transform, false);
        GameObject cloth = JojoVisuals.CreateShape(PrimitiveType.Sphere, canopy.transform, parachuteColor);
        cloth.transform.localPosition = Vector3.up * height;
        cloth.transform.localScale = new Vector3(parachuteSize, parachuteSize * 0.35f, parachuteSize);

        // 4 cordes du crousty jusqu'au bord de la toile
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f + 45f;
            Vector3 edge = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (parachuteSize * 0.45f) + Vector3.up * height;
            GameObject rope = JojoVisuals.CreateShape(PrimitiveType.Cube, canopy.transform, Color.white);
            rope.transform.localPosition = edge * 0.5f;
            rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, edge);
            rope.transform.localScale = new Vector3(0.03f, edge.magnitude, 0.03f);
        }

        canopy.transform.localScale = new Vector3(0f, 1f, 0f); // fermé au départ
        return canopy;
    }

    private GameObject CreateProjectile()
    {
        if (croustyProjectilePrefab != null)
        {
            GameObject instance = Instantiate(croustyProjectilePrefab);
            // Pas de collision : le crousty ne doit pas taper le bus
            foreach (Collider c in instance.GetComponentsInChildren<Collider>())
                c.enabled = false;
            FitToSize(instance, projectileSize);
            return instance;
        }

        GameObject cube = JojoVisuals.CreateShape(PrimitiveType.Cube, null, projectileColor);
        cube.name = "Crousty (projectile)";
        cube.transform.localScale = Vector3.one * projectileSize;
        return cube;
    }

    // Met le modèle à la bonne taille, quelles que soient les unités du fichier 3D
    private static void FitToSize(GameObject instance, float size)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > 0.0001f)
            instance.transform.localScale *= size / largest;
    }
}
