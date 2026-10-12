using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BusCarCrash : MonoBehaviour
{
    [Header("Détection")]
    public string carTag = "voitures";

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
        GameObject car = FindCarWithTag(collision.collider.transform);
        if (car == null) return;

        // Trop lent : simple choc, la voiture reste
        if (lastSpeedKmh < explosionSpeedKmh) { CroustySoundDesignManager.Play(CroustySound.Horn,car.transform.position); return; }

        // Évite d'exploser deux fois la même voiture
        if (!destroyedCars.Add(car)) return;

        ExplodeCar(car);

        // Le bus garde (en partie) son élan
        rb.linearVelocity = velocityBeforeImpact * speedKeptAfterImpact;
    }

    // Remonte la hiérarchie : le tag peut être sur le collider ou sur un parent
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

    private void ExplodeCar(GameObject car)
    {
        CroustySoundDesignManager.Play(CroustySound.Explosion,car.transform.position);
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, car.transform.position, Quaternion.identity);
            Destroy(fx, explosionLifetime);
        }

        foreach (Collider c in car.GetComponentsInChildren<Collider>())
            c.enabled = false;

        Destroy(car);
    }
}
