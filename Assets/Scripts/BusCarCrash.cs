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
    [Tooltip("Choc avec l'avant du bus nécessaire pour détruire une voiture de police.")]
    public float policeFrontImpactKmh = 15f;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionLifetime = 5f;

    [Header("Debug")]
    [SerializeField] private float lastSpeedKmh;

    public int CarsExploded { get; private set; }

    private Rigidbody rb;
    private Vector3 velocityBeforeImpact;
    private Vector3 angularVelocityBeforeImpact;
    private BoxCollider busCollider;
    private readonly RaycastHit[] impactHits = new RaycastHit[32];
    private readonly HashSet<GameObject> destroyedCars = new HashSet<GameObject>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        busCollider = GetComponent<BoxCollider>();
    }

    private void FixedUpdate()
    {
        velocityBeforeImpact = rb.linearVelocity;
        angularVelocityBeforeImpact = rb.angularVelocity;

        Vector3 flat = velocityBeforeImpact;
        flat.y = 0f;
        lastSpeedKmh = flat.magnitude * 3.6f;
        if (lastSpeedKmh < Mathf.Min(explosionSpeedKmh, policeFrontImpactKmh) || busCollider == null) return;

        // Remove cars just before the physics step reaches them, avoiding the contact impulse.
        Vector3 scale = busCollider.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfSize = Vector3.Scale(busCollider.size * .5f, scale);
        int count = Physics.BoxCastNonAlloc(busCollider.transform.TransformPoint(busCollider.center),
            halfSize, velocityBeforeImpact.normalized, impactHits, busCollider.transform.rotation,
            velocityBeforeImpact.magnitude * Time.fixedDeltaTime + .05f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (impactHits[i].collider.GetComponentInParent<PoliceCarAI>() != null &&
                Mathf.Abs(Vector3.Dot(impactHits[i].normal, transform.forward)) < .5f) continue;
            TryDestroyCar(impactHits[i].collider, impactHits[i].point, lastSpeedKmh);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        var police = collision.collider.GetComponentInParent<PoliceCarAI>();
        GameObject car = police != null ? police.gameObject : FindCarWithTag(collision.collider.transform);
        if (car == null) return;
        if (police != null)
        {
            TryResolvePoliceCollision(collision);
            return;
        }

        // Trop lent : simple choc, la voiture reste
        if (lastSpeedKmh < explosionSpeedKmh) { CroustySoundDesignManager.Play(CroustySound.Horn,car.transform.position); return; }

        if (!TryDestroyCar(collision.collider, collision.GetContact(0).point, lastSpeedKmh)) return;

        // Fallback for contacts missed by the sweep: preserve momentum and steering.
        RestoreMomentum();
    }

    private void RestoreMomentum()
    {
        rb.linearVelocity = velocityBeforeImpact;
        rb.angularVelocity = angularVelocityBeforeImpact;
    }

    // Shared by both collision callbacks so their execution order cannot change the winner.
    public bool TryResolvePoliceCollision(Collision collision)
    {
        if (!isActiveAndEnabled) return false;
        if (collision.collider.GetComponentInParent<PoliceRoadblock>() != null) return false;
        var police = collision.collider.GetComponentInParent<PoliceCarAI>();
        if (police == null) return false;
        if (police.IsDestroyed) return true;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (Mathf.Abs(Vector3.Dot(contact.normal, transform.forward)) < .5f) continue;
            float closingKmh = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)) * 3.6f;
            if (TryDestroyCar(collision.collider, contact.point, closingKmh))
            {
                RestoreMomentum();
                return true;
            }
        }
        return false;
    }

    private bool IsFrontImpact(Vector3 point)
    {
        if (busCollider == null) return false;
        Vector3 local = busCollider.transform.InverseTransformPoint(point) - busCollider.center;
        return local.z >= busCollider.size.z * .5f - .7f &&
            Mathf.Abs(local.x) <= busCollider.size.x * .5f + .5f;
    }

    private bool TryDestroyCar(Collider hit, Vector3 point, float impactKmh)
    {
        if (hit.GetComponentInParent<PoliceRoadblock>() != null) return false;
        var police = hit.GetComponentInParent<PoliceCarAI>();
        GameObject car = police != null ? police.gameObject : FindCarWithTag(hit.transform);
        if (car == null) return false;
        if (police != null)
        {
            if (police.IsDestroyed || !IsFrontImpact(point) || impactKmh < policeFrontImpactKmh) return false;
        }
        else if (lastSpeedKmh < explosionSpeedKmh) return false;
        if (!destroyedCars.Add(car)) return false;
        if (police != null) police.DestroyByBus();
        ExplodeCar(car);
        return true;
    }

    public bool WillDestroy(Collider other)
    {
        if (!isActiveAndEnabled) return false;
        var police = other.GetComponentInParent<PoliceCarAI>();
        if (police != null) return police.IsDestroyed;
        return lastSpeedKmh >= explosionSpeedKmh && FindCarWithTag(other.transform) != null;
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
        CarsExploded++;
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
