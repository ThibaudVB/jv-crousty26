using System.Collections.Generic;
using UnityEngine;

// One police car. On patrol it is an ordinary CityTrafficCar (lanes, lights, priorities);
// in pursuit it drives itself: straight at the bus while it is in sight, otherwise along
// the street graph to where the bus was last reported. PoliceChaseManager decides.
public class PoliceCarAI : MonoBehaviour
{
    public enum Mode { Patrol, Chase, Return }

    public Mode State { get; private set; } = Mode.Patrol;
    public float Speed { get; private set; }
    public float HalfLength { get; private set; }
    public float BlindRemaining => Mathf.Max(0f, blindUntil - Time.time);
    public bool IsBlinded => Time.time < blindUntil;
    public bool InPursuit => State == Mode.Chase;
    // Cached by the manager at a few hertz.
    public bool SeesBus { get; set; }

    private PoliceChaseManager manager;
    private ProceduralCity city;
    private CityTrafficCar patrol;
    private Rigidbody body;
    private Renderer[] beacons;
    private Material beaconDark, beaconRed, beaconBlue;
    private readonly List<Vector3> route = new List<Vector3>(32);
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private int routeIndex;
    private float nextRoute, blindUntil, yaw, wobble;
    private Vector3 routeGoal, laneTarget;
    private Vector2Int laneFrom, laneTo;
    private bool hasLane;

    public void Init(PoliceChaseManager owner, ProceduralCity streets, float halfLength,
        Renderer[] lights, Material dark, Material red, Material blue)
    {
        manager = owner; city = streets; HalfLength = halfLength;
        beacons = lights; beaconDark = dark; beaconRed = red; beaconBlue = blue;
        body = GetComponent<Rigidbody>();
        patrol = GetComponent<CityTrafficCar>();
        patrol.IsPolice = true;
        patrol.Init(city, halfLength);
        patrol.cruiseSpeed = 10f;
    }

    public void StartPatrol(Vector2Int from, Vector2Int to, Vector3 at)
    {
        State = Mode.Patrol;
        city.Pursuers.Remove(transform);
        AddPatrol();
        patrol.enabled = true;
        patrol.Place(from, to, at);
        SetBeacons(false, 0);
    }

    public void StartChase()
    {
        if (State == Mode.Chase) return;
        Speed = State == Mode.Patrol ? patrol.Speed : Speed;
        State = Mode.Chase;
        patrol.enabled = false;
        RemovePatrol();
        if (!city.Pursuers.Contains(transform)) city.Pursuers.Add(transform);
        yaw = transform.eulerAngles.y;
        route.Clear(); nextRoute = 0f;
    }

    // Lost the bus: drive back into the nearest right-hand lane, then patrol again.
    public void ReturnToPatrol()
    {
        if (State != Mode.Chase) return;
        State = Mode.Return;
        hasLane = city.NearestLane(transform.position, transform.forward, out laneFrom, out laneTo, out laneTarget);
        route.Clear(); nextRoute = 0f;
    }

    public void Blind(float seconds)
    {
        blindUntil = Mathf.Max(blindUntil, Time.time + seconds);
        wobble = Random.value < .5f ? -1f : 1f;
    }

    private void FixedUpdate()
    {
        if (manager == null || State == Mode.Patrol || Time.timeScale <= 0f) return;
        float dt = Time.fixedDeltaTime;
        Vector3 position = body.position;
        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        float top = manager.ChaseSpeed;
        float desired;
        Vector3 goal;

        if (State == Mode.Return)
        {
            if (!hasLane) { manager.Recycle(this); return; }
            Vector3 offset = laneTarget - position; offset.y = 0;
            if (offset.magnitude < 3f) { StartPatrol(laneFrom, laneTo, laneTarget); return; }
            goal = offset.magnitude > 25f ? RoutePoint(position, laneTarget, ref top) : laneTarget;
            desired = Mathf.Min(12f, top);
        }
        else if (IsBlinded)
        {
            // Inside the cloud: brake hard and swerve.
            goal = position + Quaternion.Euler(0, 35f * wobble, 0) * forward * 10f;
            desired = 0f;
        }
        else if (SeesBus)
        {
            // Aim slightly ahead of the bus, more when it is far.
            Vector3 bus = manager.BusPosition;
            float lead = Mathf.Clamp((bus - position).magnitude / Mathf.Max(8f, Speed), 0f, 1.2f);
            goal = bus + manager.BusVelocity * lead;
            desired = top;
        }
        else
        {
            goal = RoutePoint(position, manager.TrackedPosition, ref top);
            desired = top;
        }

        AvoidTraffic(position, forward, ref goal, ref desired);
        KeepOffBus(position, forward, ref desired);

        // Steering: a car cannot turn on the spot, less so at speed.
        Vector3 to = goal - position; to.y = 0;
        if (to.sqrMagnitude > .01f)
        {
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            float rate = Mathf.Lerp(160f, 80f, Speed / Mathf.Max(1f, manager.ChaseSpeed));
            yaw += Mathf.Clamp(angle, -rate * dt, rate * dt);
            if (Mathf.Abs(angle) > 55f) desired = Mathf.Min(desired, 9f);
        }
        float braking = manager.ChaseAcceleration * (IsBlinded ? 2.5f : 1.6f);
        Speed = Mathf.MoveTowards(Speed, desired, (desired > Speed ? manager.ChaseAcceleration : braking) * dt);
        forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        position += forward * Speed * dt;
        position.y = Ground(position, body.position.y);
        body.MovePosition(position);
        body.MoveRotation(Quaternion.Euler(0, yaw, 0));
    }

    private void Update()
    {
        if (beacons == null) return;
        SetBeacons(State != Mode.Patrol, Mathf.FloorToInt(Time.time * 6f));
    }

    private void SetBeacons(bool on, int phase)
    {
        if (beacons == null || beacons.Length < 2) return;
        beacons[0].sharedMaterial = on && phase % 2 == 0 ? beaconRed : beaconDark;
        beacons[1].sharedMaterial = on && phase % 2 == 1 ? beaconBlue : beaconDark;
    }

    // Follows the street graph (the GPS search) and slows down before corners.
    private Vector3 RoutePoint(Vector3 position, Vector3 destination, ref float top)
    {
        if (Time.time >= nextRoute || (routeGoal - destination).sqrMagnitude > 64f)
        {
            if (!city.TryBuildRoute(position, destination, route)) route.Clear();
            routeIndex = 1; routeGoal = destination;
            nextRoute = Time.time + .8f;
        }
        if (route.Count < 2) return destination;
        while (routeIndex < route.Count - 1 && Flat(route[routeIndex] - position).magnitude < 7f) routeIndex++;
        routeIndex = Mathf.Min(routeIndex, route.Count - 1);
        Vector3 corner = route[routeIndex];
        if (routeIndex < route.Count - 1)
        {
            Vector3 incoming = Flat(corner - position), outgoing = Flat(route[routeIndex + 1] - corner);
            if (Vector3.Angle(incoming, outgoing) > 30f)
                top = Mathf.Min(top, Mathf.Sqrt(11f * 11f + 2f * 9f * incoming.magnitude));
        }
        return corner;
    }

    // Overtakes slower cars on the left instead of driving through them.
    private void AvoidTraffic(Vector3 position, Vector3 forward, ref Vector3 goal, ref float desired)
    {
        Vector3 left = new Vector3(-forward.z, 0, forward.x);
        foreach (var car in city.Traffic)
        {
            if (car == null || car.transform == transform) continue;
            Vector3 offset = Flat(car.transform.position - position);
            float ahead = Vector3.Dot(offset, forward);
            if (ahead <= 0f || ahead > 16f || Mathf.Abs(Vector3.Dot(offset, left)) > 2.8f) continue;
            goal += left * 4.5f;
            desired = Mathf.Min(desired, ahead < HalfLength + car.HalfLength + 2f ? car.Speed : 17f);
        }
    }

    // Presses the bus without shoving it through the scenery.
    private void KeepOffBus(Vector3 position, Vector3 forward, ref float desired)
    {
        Vector3 offset = Flat(manager.BusPosition - position);
        float ahead = Vector3.Dot(offset, forward);
        if (ahead <= 0f || Mathf.Abs(Vector3.Dot(offset, new Vector3(forward.z, 0, -forward.x))) > 3.2f) return;
        float room = ahead - HalfLength - manager.BusHalfLength - 1f;
        float busAlong = Mathf.Max(0f, Vector3.Dot(manager.BusVelocity, forward));
        if (room < 6f) desired = Mathf.Min(desired, busAlong + Mathf.Max(0f, room));
        if (room < .3f) desired = Mathf.Min(desired, busAlong * .9f);
    }

    private float Ground(Vector3 position, float current)
    {
        int count = Physics.RaycastNonAlloc(position + Vector3.up * 3f, Vector3.down, groundHits, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            // Ramps are climbed, walls and roofs above the car are not.
            if (hit.rigidbody != null || hit.normal.y < .6f || hit.point.y > current + 1.2f) continue;
            if (hit.point.y > best) best = hit.point.y;
        }
        return float.IsNegativeInfinity(best) ? city.Origin.y + .02f : best + .02f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (manager != null && collision.rigidbody != null && collision.rigidbody == manager.BusBody) manager.Provoked(this);
    }

    private void AddPatrol() => city.AddPolicePatrol(patrol);
    private void RemovePatrol() => city.RemovePolicePatrol(patrol);
    private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    private void OnDestroy()
    {
        if (city == null) return;
        city.Pursuers.Remove(transform);
        if (patrol != null) RemovePatrol();
    }
}
