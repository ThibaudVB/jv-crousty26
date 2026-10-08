using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class BusCarCrash : MonoBehaviour
{
    [Header("Vitesse minimale pour exploser une voiture")]
    public float explosionSpeedKmh = 60f;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 5f;

    [Header("Après l'impact")]
    [Range(0f, 1f)] public float speedKeptAfterImpact = 0.85f;

    [Header("Debug")]
    [SerializeField] private float lastSpeedKmh;

    private Rigidbody rb;
    private Vector3 velocityBeforeImpact;
    private readonly HashSet<GameObject> destroyedCars = new HashSet<GameObject>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        velocityBeforeImpact = rb.linearVelocity;

        Vector3 flat = velocityBeforeImpact;
        flat.y = 0f;
        lastSpeedKmh = flat.magnitude * 3.6f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TrafficCarNavMesh car = collision.collider.GetComponentInParent<TrafficCarNavMesh>();
        if (car == null) return;

        if (!destroyedCars.Add(car.gameObject)) return;

        if (lastSpeedKmh < explosionSpeedKmh)
        {
            // Si on est trop lent, c'est juste un simple choc, la voiture reste
            destroyedCars.Remove(car.gameObject);
            return;
        }

        ExplodeCar(car);

        // Pour que le bus garde (en partie) son élan
        rb.linearVelocity = velocityBeforeImpact * speedKeptAfterImpact;
    }

    private void ExplodeCar(TrafficCarNavMesh car)
    {
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(
                explosionPrefab,
                car.transform.position,
                Quaternion.identity
            );
            Destroy(fx, explosionLifetime);
        }

        foreach (Collider c in car.GetComponentsInChildren<Collider>())
            c.enabled = false;

        Destroy(car.gameObject);
    }
}