using System.Collections.Generic;
using UnityEngine;

// Poursuite : la police apparait derriere le bus quand il traine, roule sur le meme chemin que lui
// et l'arrete s'il reste trop lent pendant qu'elle le colle. Rouler vite longtemps la fait abandonner,
// le riz la ralentit, le PET l'aveugle et le vol la seme.
public class PoliceChaseManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody busRigidbody;
    [SerializeField] private GameObject policePrefab;
    [SerializeField] private PoliceCatchZone catchZone;

    [Header("Bus Fighter")]
    [SerializeField] private BusFlightController flightController;
    [SerializeField, Min(0f)] private float landingGraceSeconds = 1f;

    [Header("Allure du bus")]
    [Tooltip("En dessous de cette vitesse, la police arrive et peut arreter le bus")]
    [SerializeField, Min(1f)] private float safeSpeedKmh = 100f;
    [SerializeField, Min(0f)] private float startupGraceSeconds = 3f;

    [Header("Poursuite (metres)")]
    [Tooltip("Distance derriere le bus (sur son chemin) ou la police apparait")]
    [SerializeField, Min(5f)] private float spawnDistance = 40f;
    [Tooltip("Au-dela de cette distance, la police a perdu le bus")]
    [SerializeField, Min(10f)] private float loseDistance = 80f;
    [Tooltip("La police arrete le bus quand elle est a moins de cette distance de son pare-chocs")]
    [SerializeField, Min(0f)] private float catchRange = 3f;
    [Tooltip("Secondes au-dessus de la vitesse de securite avant que la police abandonne")]
    [SerializeField, Min(1f)] private float giveUpSeconds = 12f;
    [Tooltip("Secondes avant qu'une nouvelle voiture puisse apparaitre")]
    [SerializeField, Min(0f)] private float respawnDelay = 5f;

    public float BlindRemaining { get; private set; }
    public bool IsBlinded => BlindRemaining > 0f;
    public bool ChaseStarted => ActivePolice != null;
    public PoliceCarAI ActivePolice { get; private set; }
    public float PoliceDistance => ActivePolice != null ? ActivePolice.DistanceToTarget : loseDistance;

    // Chemin parcouru par le bus, du plus ancien au plus recent
    private readonly List<Vector3> trail = new List<Vector3>(512);
    private const float TrailSpacing = 1.5f;
    private const int MaxTrailPoints = 300;

    private float graceRemaining;
    private float respawnRemaining;
    private float fastSeconds;

    public bool TryBlindWithPet(Rigidbody source, float duration, float range)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || source != busRigidbody ||
            ActivePolice == null || catchZone == null || catchZone.IsCaught) return false;
        Vector3 offset = ActivePolice.transform.position - source.position;
        if (Vector3.Dot(offset, source.rotation * Vector3.forward) > 0f ||
            PoliceDistance > Mathf.Max(0f, range)) return false;
        BlindRemaining = Mathf.Clamp(duration, 0f, 5f);
        if (!IsBlinded) return false;
        catchZone.AdvanceCapture(false, 0f);
        return true;
    }

    private void Awake()
    {
        if (busRigidbody == null || policePrefab == null || policePrefab.GetComponent<PoliceCarAI>() == null)
        {
            Debug.LogError("Police : renseigner le bus et le prefab de voiture.", this);
            enabled = false;
            return;
        }

        if (catchZone == null)
            catchZone = busRigidbody.GetComponentInChildren<PoliceCatchZone>();

        if (catchZone == null)
        {
            Debug.LogError("Police : zone de capture et evenement de defaite manquants.", this);
            enabled = false;
            return;
        }

        // Recuperation automatique du mode avion.
        if (flightController == null)
            flightController = busRigidbody.GetComponent<BusFlightController>();

        graceRemaining = startupGraceSeconds;
    }

    private float ForwardSpeed()
    {
        Vector3 forward = Vector3.ProjectOnPlane(busRigidbody.rotation * Vector3.forward, Vector3.up).normalized;
        return Mathf.Max(0f, Vector3.Dot(busRigidbody.linearVelocity, forward)) * 3.6f;
    }

    private void FixedUpdate()
    {
        if (busRigidbody == null || catchZone == null || catchZone.IsCaught || Time.timeScale <= 0f)
            return;

        float dt = Time.fixedDeltaTime;

        // Bus Fighter : la police perd la trace pendant le vol.
        if (flightController != null && flightController.IsAirborne)
        {
            catchZone.AdvanceCapture(false, dt);
            RemovePolice();
            trail.Clear();
            graceRemaining = Mathf.Max(graceRemaining, landingGraceSeconds);
            return;
        }

        RecordTrail();

        if (graceRemaining > 0f)
        {
            graceRemaining = Mathf.Max(0f, graceRemaining - dt);
            catchZone.AdvanceCapture(false, dt);
            return;
        }

        float speed = ForwardSpeed();
        bool slow = speed < safeSpeedKmh;

        // Apparition : quand le bus traine
        if (ActivePolice == null)
        {
            catchZone.AdvanceCapture(false, dt);
            respawnRemaining = Mathf.Max(0f, respawnRemaining - dt);
            if (slow && respawnRemaining <= 0f)
                SpawnPolice(speed);
            return;
        }

        if (IsBlinded)
            BlindRemaining = Mathf.Max(0f, BlindRemaining - dt);

        // Rouler vite longtemps fait abandonner la police
        fastSeconds = slow ? 0f : fastSeconds + dt;
        bool givingUp = fastSeconds >= giveUpSeconds;

        float speedFactor = IsBlinded ? 0.4f : givingUp ? 0.6f : 1f;
        ActivePolice.Drive(dt, speedFactor);

        // Capture : bus trop lent, police collee a lui et en etat de conduire
        bool close = ActivePolice.DistanceToTarget <= ActivePolice.FollowDistance + catchRange ||
            ActivePolice.IsTouchingCaptureZone(catchZone.CaptureCollider);
        bool canCatch = slow && close && !IsBlinded && !ActivePolice.IsSlowed;
        catchZone.AdvanceCapture(canCatch, dt, canCatch);

        // Semee
        if (ActivePolice.DistanceToTarget > loseDistance ||
            (givingUp && ActivePolice.DistanceToTarget > spawnDistance))
        {
            RemovePolice();
            respawnRemaining = respawnDelay;
        }
    }

    private void RecordTrail()
    {
        Vector3 position = busRigidbody.position;
        if (trail.Count > 0)
        {
            float moved = Vector3.ProjectOnPlane(position - trail[trail.Count - 1], Vector3.up).magnitude;
            // Teleportation (reset du bus...) : on repart de zero
            if (moved > 20f)
            {
                trail.Clear();
                RemovePolice();
            }
            else if (moved < TrailSpacing)
                return;
        }

        trail.Add(position);
        if (trail.Count > MaxTrailPoints) trail.RemoveAt(0);
        if (ActivePolice != null) ActivePolice.AddWaypoint(position);
    }

    private void SpawnPolice(float busSpeedKmh)
    {
        // On remonte le chemin du bus pour trouver le point d'apparition
        int index = trail.Count - 1;
        float walked = 0f;
        while (index > 0 && walked < spawnDistance)
        {
            walked += Vector3.Distance(trail[index], trail[index - 1]);
            index--;
        }

        Vector3 position;
        Vector3 forward;
        if (index < 0 || walked < 10f)
        {
            // Pas assez de chemin connu : juste derriere le bus
            forward = Vector3.ProjectOnPlane(busRigidbody.rotation * Vector3.forward, Vector3.up).normalized;
            position = busRigidbody.position - forward * spawnDistance * 0.5f;
            index = trail.Count;
        }
        else
        {
            position = trail[index];
            forward = trail[Mathf.Min(index + 1, trail.Count - 1)] - position;
            index++;
        }

        GameObject instance = Instantiate(policePrefab);
        instance.SetActive(false);
        ActivePolice = instance.GetComponent<PoliceCarAI>();
        ActivePolice.SetTarget(busRigidbody.transform, busRigidbody);
        ActivePolice.InitializeBody();
        ActivePolice.Spawn(position, forward, trail.GetRange(index, trail.Count - index), busSpeedKmh);
        instance.SetActive(true);
        fastSeconds = 0f;
    }

    private void RemovePolice()
    {
        BlindRemaining = 0f;
        fastSeconds = 0f;
        if (ActivePolice == null)
            return;

        ActivePolice.gameObject.SetActive(false);
        Destroy(ActivePolice.gameObject);
        ActivePolice = null;
    }

    private void OnDisable()
    {
        RemovePolice();
        if (catchZone != null)
            catchZone.AdvanceCapture(false, 0f);
    }
}
