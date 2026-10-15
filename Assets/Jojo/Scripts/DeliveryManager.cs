using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère les tournées de livraison :
// 1. Le bus passe au magasin : des adresses dispersees sont preparees sans limite de temps.
// 2. Il passe près d'une adresse qui attend (DeliveryPoint) : le crousty est lancé tout seul.
// 3. Chaque livraison rapporte des points. Terminer la tournee donne un bonus fixe.
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
    [Min(0f)] public float minimumAddressSpacing = 160f;
    private readonly List<DeliveryPoint> visibleDestinations = new List<DeliveryPoint>(10);
    public IReadOnlyList<DeliveryPoint> VisibleDestinations => visibleDestinations;
    private bool preparingOrders;

    [HideInInspector] public float secondsPerOrder = 15f; // Legacy serialized data.
    [HideInInspector] public float alarmSecondsLeft = 15f;

    [Header("Points")]
    [Min(0)] public int pointsPerDelivery = 100;
    [Tooltip("Bonus quand toutes les commandes de la tournée sont livrées")]
    [Min(0)] public int completionBonus = 500;
    [HideInInspector] public int pointsPerRemainingSecond;

    [Header("Livraison en hauteur")]
    [Tooltip("Coché : on peut livrer à n'importe quelle hauteur (en volant, depuis un toit...). " +
             "Décoché : le bus doit être à moins de \"Max Height Difference\" de l'adresse.")]
    public bool deliverFromAnyHeight = true;

    [Header("Livraison en vol : cercles")]
    [Tooltip("Coché : pendant le vol, un cercle apparaît au-dessus de chaque adresse et on ne livre " +
             "qu'en passant dedans (avec un son). Au sol, la livraison ne change pas. " +
             "La hauteur et la taille du cercle se règlent sur chaque DeliveryPoint.")]
    public bool flightDeliveryNeedsRing = false;

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
    public bool RunActive { get; private set; }    // une tournée est en cours
    public bool HasFailed { get; private set; }    // partie perdue (police ou destruction)
    public bool TutorialPickupMarkerHidden { get; private set; }
    public float RunElapsed { get; private set; }  // secondes écoulées dans la tournée
    public int Score { get; private set; }
    public int Money => Mathf.Max(0, Score - moneySpent);
    private int moneySpent;
    public string PlayerName { get; private set; }
    public int LastRunScore { get; private set; }  // points gagnés pendant la tournée en cours
    public int BestScore { get; private set; }

    // Durée de la tournée en cours (ou de la prochaine si on n'a pas encore commencé)
    public float RunTimeLimit => float.PositiveInfinity;
    public float RunRemaining => RunActive ? float.PositiveInfinity : 0f;

    // Commandes de la prochaine tournée : 3, puis 5, 10, 15...
    public int NextRunOrders => runsLoaded == 0 ? firstRunOrders :
        runsLoaded == 1 && tutorialSecondRunOrders > 0 ? tutorialSecondRunOrders :
        ordersStep * (tutorialSecondRunOrders > 0 ? runsLoaded - 1 : runsLoaded);

    public void ConfigureTutorialOrderCounts(int firstRun, int secondRun)
    {
        firstRunOrders = Mathf.Max(1, firstRun);
        tutorialSecondRunOrders = Mathf.Max(1, secondRun);
    }

    public void SetTutorialPickupMarkerHidden(bool hidden)
    {
        TutorialPickupMarkerHidden = hidden;
    }
    public int RunsLoaded => runsLoaded;

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
    private int tutorialSecondRunOrders;
    private int inFlight;            // crousty en train de voler
    private bool mustLeaveShop;      // après un chargement, il faut sortir du magasin avant de recharger
    private PoliceGameOverManager gameOver;
    private BusFlightController flight;

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
        if (atShop && NeedsPickup && !mustLeaveShop && !preparingOrders)
            StartCoroutine(PrepareOrders());
        RefreshVisibleDestinations();

        // 2. En vol, avec les cercles : on livre seulement en passant dedans
        if (flightDeliveryNeedsRing)
        {
            bool flying = IsBusFlying();
            DeliveryPoint ringTarget = UpdateRings(flying);
            if (ringTarget != null)
            {
                CroustySoundDesignManager.Play(CroustySound.FireRing);
                ThrowCrousty(ringTarget);
            }
            if (flying)
                return;
        }

        // 3. Livraison en passant devant une adresse
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

    // ---------- BOOST DE POINTS (figures du bus) ----------

    private float boostMultiplier = 1f;
    private float boostEndTime;

    // Multiplicateur appliqué aux points gagnés (1 = pas de boost)
    public float ScoreMultiplier => Time.time < boostEndTime ? boostMultiplier : 1f;
    public float BoostRemaining => Mathf.Max(0f, boostEndTime - Time.time);
    public event System.Action<float, float> ScoreBoostStarted; // (multiplicateur, durée)

    // Appelé par BusAirTricks : on garde le meilleur multiplicateur et le temps le plus long
    public void StartScoreBoost(float multiplier, float duration)
    {
        if (multiplier <= 1f || duration <= 0f)
            return;
        boostMultiplier = Mathf.Max(multiplier, ScoreMultiplier);
        boostEndTime = Mathf.Max(boostEndTime, Time.time + duration);
        ScoreBoostStarted?.Invoke(boostMultiplier, BoostRemaining);
    }

    private void AddScore(int points)
    {
        if (points <= 0)
            return;

        points = Mathf.RoundToInt(points * ScoreMultiplier);

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

    private IEnumerator PrepareOrders()
    {
        preparingOrders = true;
        var city = FindAnyObjectByType<ProceduralCity>();
        try
        {
            if (city != null && city.WorldRoot != null)
                yield return city.PrepareDeliveryAddresses(NextRunOrders);
            if (!HasFailed) LoadOrders();
        }
        finally
        {
            city?.FinishDeliveryPreparation();
            preparingOrders = false;
        }
    }

    public bool TrySpendMoney(int amount)
    {
        if (amount < 0 || amount > Money) return false;
        moneySpent += amount;
        return true;
    }

    // Unlock purchases advance immediately, without a second trip to the shop.
    public void ContinueAfterPowerChoice()
    {
        if (!HasFailed && AllDelivered && !preparingOrders)
            StartCoroutine(PrepareOrders());
    }

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
        mustLeaveShop = true;
        runsLoaded++;
        visibleDestinations.Clear();
        RefreshVisibleDestinations();
        RunStarted?.Invoke();
    }

    // Farthest-point sampling spreads the run across distinct streets, rather than the nearest block.
    private List<DeliveryPoint> FindNearbyAddresses(int wanted)
    {
        List<DeliveryPoint> sorted = new List<DeliveryPoint>(deliveryPoints);
        sorted.RemoveAll(point => point == null || !point.isActiveAndEnabled);

        int maxCount = Mathf.Max(wanted, nearbyAddressPool);
        List<DeliveryPoint> result = new List<DeliveryPoint>();
        ProceduralCity city = FindAnyObjectByType<ProceduralCity>();

        bool procedural = city != null && city.WorldRoot != null;
        sorted.RemoveAll(point => shop != null && Vector3.Distance(point.transform.position, shop.transform.position) <
            (procedural ? 160f : shop.pickupRadius + point.deliveryRadius + 2f));
        for (int selection = 0; selection < maxCount && sorted.Count > 0; selection++)
        {
            DeliveryPoint point = null; float best = -1f;
            foreach (var candidate in sorted)
            {
                float distance = DistanceToPlayer(candidate);
                foreach (var previous in result)
                    distance = Mathf.Min(distance, Vector3.Distance(previous.transform.position, candidate.transform.position));
                if (distance > best) { best = distance; point = candidate; }
            }
            if (point == null) break;
            if (procedural && result.Count > 0 && best < minimumAddressSpacing) break;
            result.Add(point);
            sorted.Remove(point);
        }
        return result;
    }

    public bool HasEnoughDispersedAddresses(int wanted) => FindNearbyAddresses(wanted).Count >= wanted;

    private void RefreshVisibleDestinations()
    {
        visibleDestinations.RemoveAll(point => point == null || !point.IsWaiting);
        if (visibleDestinations.Count < 10)
        {
            var candidates = new List<DeliveryPoint>();
            foreach (var point in deliveryPoints)
                if (point != null && point.IsWaiting && !visibleDestinations.Contains(point)) candidates.Add(point);
            candidates.Sort((a, b) => DistanceToPlayer(a).CompareTo(DistanceToPlayer(b)));
            foreach (var point in candidates)
            { if (visibleDestinations.Count >= 10) break; visibleDestinations.Add(point); }
        }
        foreach (var point in deliveryPoints)
            if (point != null) point.SetMarkerVisible(visibleDestinations.Contains(point));
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

    // Vrai pendant le pouvoir de vol (et la chute qui suit)
    private bool IsBusFlying()
    {
        if (flight == null && player != null)
            flight = player.GetComponent<BusFlightController>();
        return flight != null && flight.IsAirborne;
    }

    // Affiche / cache les cercles et renvoie l'adresse dont le cercle vient d'être traversé
    private DeliveryPoint UpdateRings(bool flying)
    {
        bool show = flying && RunActive && CroustyLeft > 0;
        DeliveryPoint crossed = null;
        foreach (DeliveryPoint point in deliveryPoints)
        {
            if (point == null)
                continue;
            // Tous les cercles sont mis à jour, même après en avoir trouvé un, pour bien cacher les autres
            if (point.UpdateRing(player.position, show && visibleDestinations.Contains(point)) && crossed == null)
                crossed = point;
        }
        return crossed;
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
        RunActive = false;
        AddScore(completionBonus);
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
