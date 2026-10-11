using System.Collections.Generic;
using UnityEngine;

// Reuses the existing stop and passenger assets, with a SceneBase-only boarding cycle.
public sealed class SceneBaseBusStop : MonoBehaviour
{
    public BusStopZone settings;
    public BusController bus;
    public Transform door;
    public BusPassengers counter;
    public float refillDelay = 15f;
    private Collider zone;
    private Rigidbody busBody;
    private Material zoneMaterial;
    private float stoppedTime;
    private float nextBoardingTime;
    private float refillTime;
    private readonly List<Passenger> passengers = new List<Passenger>();

    private sealed class Passenger
    {
        public BusStopPassenger source;
        public Vector3 position;
        public Quaternion rotation;
        public Collider[] colliders;
        public bool[] colliderEnabled;
        public bool started;
        public bool boarded;
        public float walkingTime;
    }

    private void Awake()
    {
        settings.enabled = false;
        zone = GetComponent<Collider>();
        zone.isTrigger = true;
        busBody = bus.GetComponent<Rigidbody>();
        if (settings.zoneRenderer != null) zoneMaterial = settings.zoneRenderer.material;
        foreach (var source in settings.waitingPassengers)
        {
            if (source == null) continue;
            source.enabled = false;
            var colliders = source.GetComponentsInChildren<Collider>(true);
            var initial = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) initial[i] = colliders[i].enabled;
            passengers.Add(new Passenger {
                source = source, position = source.transform.position, rotation = source.transform.rotation,
                colliders = colliders, colliderEnabled = initial
            });
        }
        SetReady(false);
    }

    private bool Inside()
    {
        Vector3 position = bus.transform.position;
        position.y = zone.bounds.center.y;
        if (settings.requireBusCenterInside) return zone.ClosestPoint(position) == position;
        foreach (var collider in bus.GetComponentsInChildren<Collider>())
            if (!collider.isTrigger && collider.bounds.Intersects(zone.bounds)) return true;
        return false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || bus == null) return;
        bool inside = Inside();
        Vector3 speed = busBody.linearVelocity;
        speed.y = 0;
        bool parked = inside && speed.magnitude * 3.6f <= settings.stopSpeedKmh && bus.enabled;
        stoppedTime = parked ? stoppedTime + Time.deltaTime : 0;
        bool ready = parked && stoppedTime >= settings.stopDuration;
        SetReady(ready);
        bool complete = passengers.Count > 0;
        foreach (var passenger in passengers)
        {
            if (passenger.source == null) continue;
            complete &= passenger.boarded;
            if (passenger.boarded || !ready) continue;
            if (!passenger.started)
            {
                if (Time.time < nextBoardingTime) continue;
                passenger.started = true;
                nextBoardingTime = Time.time + settings.delayBetweenPassengers;
                foreach (var collider in passenger.colliders) if (collider != null) collider.enabled = false;
            }
            passenger.walkingTime += Time.deltaTime;
            var transform = passenger.source.transform;
            Vector3 target = door.position;
            target.y = transform.position.y;
            float walkSpeed = passenger.walkingTime >= 5 ? passenger.source.startRunning : passenger.source.walkSpeed;
            Vector3 direction = target - transform.position;
            if (direction.magnitude <= passenger.source.enterDistance)
            {
                passenger.boarded = true;
                counter.AddPassenger();
                passenger.source.gameObject.SetActive(false);
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction.normalized), passenger.source.turnSpeed * Time.deltaTime);
            }
        }
        refillTime = complete && !inside ? refillTime + Time.deltaTime : 0;
        if (refillTime >= refillDelay) Refill();
    }

    private void Refill()
    {
        foreach (var passenger in passengers)
        {
            if (passenger.source == null) continue;
            passenger.source.transform.SetPositionAndRotation(passenger.position, passenger.rotation);
            passenger.started = passenger.boarded = false;
            passenger.walkingTime = 0;
            for (int i = 0; i < passenger.colliders.Length; i++)
                if (passenger.colliders[i] != null) passenger.colliders[i].enabled = passenger.colliderEnabled[i];
            passenger.source.gameObject.SetActive(true);
        }
        refillTime = 0;
        nextBoardingTime = 0;
    }

    private void SetReady(bool ready)
    {
        if (zoneMaterial != null) zoneMaterial.color = ready ? settings.readyColor : settings.waitingColor;
    }

    private void OnDestroy()
    {
        if (zoneMaterial != null) Destroy(zoneMaterial);
    }
}
