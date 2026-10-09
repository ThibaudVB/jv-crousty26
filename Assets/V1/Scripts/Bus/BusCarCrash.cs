using System;
using System.Collections.Generic;
using UnityEngine;

namespace V1
{
    // Quand le bus percute une voiture du trafic assez vite, la voiture explose
    // et le bus garde une partie de son élan. La caméra tremble au choc.
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

        [Header("Caméra")]
        [Range(0f, 1f)] public float crashShake = 0.6f;
        [Range(0f, 1f)] public float bumpShake = 0.2f;

        [Header("Debug")]
        [SerializeField] private float lastSpeedKmh;

        // Déclenché à chaque voiture détruite
        public event Action<TrafficCarNavMesh> CarDestroyed;

        public int CarsDestroyed { get; private set; }

        private Rigidbody rb;
        private BusCameraFollow cameraFollow;
        private Vector3 velocityBeforeImpact;
        private readonly HashSet<GameObject> destroyedCars = new HashSet<GameObject>();

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            cameraFollow = FindAnyObjectByType<BusCameraFollow>();
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
            if (car == null || destroyedCars.Contains(car.gameObject))
                return;

            // Trop lent : simple accrochage, la voiture reste
            if (lastSpeedKmh < explosionSpeedKmh)
            {
                if (cameraFollow != null && lastSpeedKmh > 15f)
                    cameraFollow.Shake(bumpShake);
                return;
            }

            destroyedCars.Add(car.gameObject);
            ExplodeCar(car);

            // Le bus garde (en partie) son élan
            rb.linearVelocity = velocityBeforeImpact * speedKeptAfterImpact;

            if (cameraFollow != null)
                cameraFollow.Shake(crashShake);
        }

        private void ExplodeCar(TrafficCarNavMesh car)
        {
            if (explosionPrefab != null)
            {
                GameObject fx = Instantiate(explosionPrefab, car.transform.position, Quaternion.identity);
                Destroy(fx, explosionLifetime);
            }

            foreach (Collider c in car.GetComponentsInChildren<Collider>())
                c.enabled = false;

            CarsDestroyed++;
            CarDestroyed?.Invoke(car);

            Destroy(car.gameObject);
        }
    }
}
