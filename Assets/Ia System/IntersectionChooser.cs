using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class IntersectionChooser : MonoBehaviour
{
    [Header("Tags")]
    public string entranceTag = "entree";
    public string carTag = "voitures";

    [Header("Détection des entrées")]
    public float detectionRadius = 15f;

    [Header("Demi-tour")]
    public bool allowUTurn = false;
    [Range(0f, 180f)] public float maxTurnAngle = 120f;

    [Header("Relance")]
    [Tooltip("Délai minimum entre deux relances d'une même voiture")]
    public float retryDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private List<Transform> entrances = new List<Transform>();
    public bool debugLogs = true;

    // Voitures actuellement dans la zone (avec le nombre de colliders à l'intérieur)
    private readonly Dictionary<CarFollowPoints, int> carsInside = new Dictionary<CarFollowPoints, int>();
    private readonly Dictionary<CarFollowPoints, float> lastForceTime = new Dictionary<CarFollowPoints, float>();
    private readonly List<CarFollowPoints> toRemove = new List<CarFollowPoints>();

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public bool useAssignedEntrances;

    private void Start()
    {
        if (!useAssignedEntrances) RefreshEntrances();
    }

    // À rappeler après la génération de la map si elle est faite au runtime
    public void RefreshEntrances()
    {
        entrances.Clear();

        foreach (GameObject go in GameObject.FindGameObjectsWithTag(entranceTag))
        {
            if (Vector3.Distance(go.transform.position, transform.position) <= detectionRadius)
                entrances.Add(go.transform);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CarFollowPoints car = GetCar(other);
        if (car == null) return;

        carsInside.TryGetValue(car, out int count);
        carsInside[car] = count + 1;

        // Premier collider de cette voiture dans la zone : on la force tout de suite
        if (count == 0)
            ForceCar(car);
    }

    private void OnTriggerExit(Collider other)
    {
        CarFollowPoints car = GetCar(other);
        if (car == null) return;

        if (!carsInside.TryGetValue(car, out int count)) return;

        if (count <= 1)
        {
            carsInside.Remove(car);
            lastForceTime.Remove(car);
        }
        else
        {
            carsInside[car] = count - 1;
        }
    }

    private void Update()
    {
        // Toute voiture arrêtée dans la zone est relancée vers une entrée
        toRemove.Clear();

        foreach (var kv in carsInside)
        {
            CarFollowPoints car = kv.Key;

            if (car == null) { toRemove.Add(car); continue; } // voiture détruite

            if (car.IsStopped)
                ForceCar(car);
        }

        foreach (CarFollowPoints c in toRemove)
        {
            carsInside.Remove(c);
            lastForceTime.Remove(c);
        }
    }

    private void ForceCar(CarFollowPoints car)
    {
        // Petit délai pour ne pas relancer à chaque frame si aucune entrée n'est dispo
        if (lastForceTime.TryGetValue(car, out float t) && Time.time - t < retryDelay) return;
        lastForceTime[car] = Time.time;

        Transform chosen = ChooseEntrance(car.transform);

        if (chosen == null)
        {
            if (debugLogs) Debug.LogWarning($"{name} : aucune entrée '{entranceTag}' dans un rayon de {detectionRadius}.", this);
            return;
        }

        if (debugLogs) Debug.Log($"{name} : {car.name} forcée vers {chosen.name}", this);
        car.ForceDestination(chosen);
    }

    public Transform ChooseEntrance(Transform car)
    {
        if (entrances.Count == 0) return null;

        List<Transform> candidates = new List<Transform>();

        foreach (Transform e in entrances)
        {
            if (allowUTurn)
            {
                candidates.Add(e);
                continue;
            }

            Vector3 dir = e.position - car.position;
            dir.y = 0f;
            if (Vector3.Angle(car.forward, dir) <= maxTurnAngle)
                candidates.Add(e);
        }

        // Sécurité : si tout a été filtré, on prend toutes les entrées
        if (candidates.Count == 0)
            candidates = entrances;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private CarFollowPoints GetCar(Collider other)
    {
        GameObject carObj = FindCarWithTag(other.transform);
        if (carObj == null) return null;
        return carObj.GetComponentInParent<CarFollowPoints>();
    }

    private GameObject FindCarWithTag(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag(carTag))
                return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.cyan;
        foreach (Transform e in entrances)
            if (e != null) Gizmos.DrawLine(transform.position, e.position);
    }
}