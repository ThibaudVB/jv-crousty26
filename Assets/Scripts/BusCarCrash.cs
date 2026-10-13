using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(100)]
public class BusCarCrash : MonoBehaviour
{
    [Header("Détection")]
    public string carTag = "voitures";

    [Header("Vitesse minimale pour exploser une voiture")]
    public float explosionSpeedKmh = 60f;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 5f;

    [Header("Debug")]
    [SerializeField] private float lastSpeedKmh;

    private Rigidbody rb;
    private Vector3 velocityBeforeImpact;
    private Vector3 angularVelocityBeforeImpact;
    private BoxCollider busCollider;
    private PoliceChaseManager policeManager;
    private readonly RaycastHit[] impactHits = new RaycastHit[32];
    private readonly HashSet<GameObject> destroyedCars = new HashSet<GameObject>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        busCollider = GetComponent<BoxCollider>();
        policeManager = FindAnyObjectByType<PoliceChaseManager>();
    }

    private void FixedUpdate()
    {
        velocityBeforeImpact = rb.linearVelocity;
        angularVelocityBeforeImpact = rb.angularVelocity;

        Vector3 flat = velocityBeforeImpact;
        flat.y = 0f;
        lastSpeedKmh = flat.magnitude * 3.6f;
        if (lastSpeedKmh < explosionSpeedKmh || busCollider == null) return;

        // Remove cars just before the physics step reaches them, avoiding the contact impulse.
        Vector3 scale = busCollider.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfSize = Vector3.Scale(busCollider.size * .5f, scale);
        int count = Physics.BoxCastNonAlloc(busCollider.transform.TransformPoint(busCollider.center),
            halfSize, velocityBeforeImpact.normalized, impactHits, busCollider.transform.rotation,
            velocityBeforeImpact.magnitude * Time.fixedDeltaTime + .05f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            TryDestroyCar(impactHits[i].collider.transform);
    }

    private void OnCollisionEnter(Collision collision)
    {
        var police = collision.collider.GetComponentInParent<PoliceCarAI>();
        GameObject car = police != null ? police.gameObject : FindCarWithTag(collision.collider.transform);
        if (car == null) return;
        if (police != null && (police.IsDestroyed || police.IsRammingBus)) return;

        // Trop lent : simple choc, la voiture reste
        if (lastSpeedKmh < explosionSpeedKmh) { CroustySoundDesignManager.Play(CroustySound.Horn,car.transform.position); return; }

        if (!TryDestroyCar(collision.collider.transform)) return;

        // Fallback for contacts missed by the sweep: preserve momentum and steering.
        rb.linearVelocity = velocityBeforeImpact;
        rb.angularVelocity = angularVelocityBeforeImpact;
    }

    private bool TryDestroyCar(Transform hit)
    {
        var police = hit.GetComponentInParent<PoliceCarAI>();
        GameObject car = police != null ? police.gameObject : FindCarWithTag(hit);
        if (car == null || police != null && (police.IsDestroyed || police.IsRammingBus) || !destroyedCars.Add(car)) return false;
        if (police != null) police.DestroyByBus();
        else if (policeManager != null) policeManager.CivilianCarDestroyed(rb);
        ExplodeCar(car);
        return true;
    }

    public bool WillDestroy(Collider other)
    {
        if (!isActiveAndEnabled || lastSpeedKmh < explosionSpeedKmh) return false;
        var police = other.GetComponentInParent<PoliceCarAI>();
        return police != null ? !police.IsRammingBus : FindCarWithTag(other.transform) != null;
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
