using System.Collections.Generic;
using UnityEngine;

// One police car. On patrol it is an ordinary CityTrafficCar (lanes, lights, priorities);
// in pursuit it gets behind the bus before attempting a rear-end hit, otherwise follows
// the street graph to where the bus was last reported. PoliceChaseManager decides.
public class PoliceCarAI : MonoBehaviour
{
    public enum Mode { Patrol, Chase, Return, Destroyed, Roadblock }

    public Mode State { get; private set; } = Mode.Patrol;
    public float Speed { get; private set; }
    public float HalfLength { get; private set; }
    public float BlindRemaining => Mathf.Max(0f, blindUntil - Time.time);
    public bool IsBlinded => Time.time < blindUntil;
    public bool InPursuit => State == Mode.Chase;
    public bool IsDestroyed => State == Mode.Destroyed;
    public bool IsRoadblock => State == Mode.Roadblock;
    public bool IsTutorialImpactCar { get; private set; }
    public bool IsRammingBus { get; private set; }
    public float RiceSpeedFactor { get; private set; } = 1f;

    public void SetRiceSpeedFactor(float factor)
    {
        factor = Mathf.Clamp01(factor);
        if (factor < RiceSpeedFactor) Speed *= factor / Mathf.Max(.01f, RiceSpeedFactor);
        RiceSpeedFactor = factor;
        if (patrol != null) patrol.SetSurfaceSpeedFactor(factor);
    }
    public void MarkAsTutorialImpactCar() => IsTutorialImpactCar = true;
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
    private readonly RaycastHit[] obstacleHits = new RaycastHit[32];
    private BoxCollider shape;
    private float blockedFor, reverseUntil, nextRam, ramRetreatUntil;
    private int routeIndex;
    private float nextRoute, blindUntil, yaw, wobble;
    private Vector3 routeGoal, laneTarget;
    private Vector2Int laneFrom, laneTo;
    private bool hasLane;
    private float approachSide;

    public void Init(PoliceChaseManager owner, ProceduralCity streets, float halfLength,
        Renderer[] lights, Material dark, Material red, Material blue)
    {
        manager = owner; city = streets; HalfLength = halfLength;
        approachSide = owner.Cars.Count % 2 == 0 ? -1f : 1f;
        beacons = lights; beaconDark = dark; beaconRed = red; beaconBlue = blue;
        body = GetComponent<Rigidbody>();
        shape = GetComponent<BoxCollider>();
        patrol = GetComponent<CityTrafficCar>();
        patrol.IsPolice = true;
        patrol.Init(city, halfLength);
        patrol.cruiseSpeed = 10f;
    }

    public void StartPatrol(Vector2Int from, Vector2Int to, Vector3 at)
    {
        if (IsDestroyed) return;
        IsRammingBus = false;
        blockedFor = reverseUntil = ramRetreatUntil = 0f;
        SetRiceSpeedFactor(1f);
        State = Mode.Patrol;
        city.Pursuers.Remove(transform);
        AddPatrol();
        patrol.enabled = true;
        patrol.Place(from, to, at);
        SetBeacons(false, 0);
    }

    public void StartChase()
    {
        if (IsDestroyed || IsRoadblock || State == Mode.Chase) return;
        Speed = State == Mode.Patrol ? patrol.Speed : Speed;
        State = Mode.Chase;
        patrol.enabled = false;
        RemovePatrol();
        if (!city.Pursuers.Contains(transform)) city.Pursuers.Add(transform);
        yaw = transform.eulerAngles.y;
        route.Clear(); nextRoute = 0f;
    }

    public void ParkAtRoadblock(Vector3 position, Quaternion rotation)
    {
        State = Mode.Roadblock;
        Speed = 0f;
        IsRammingBus = false;
        patrol.enabled = false;
        RemovePatrol();
        if (!city.Pursuers.Contains(transform)) city.Pursuers.Add(transform);
        body.position = position;
        body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        yaw = rotation.eulerAngles.y;
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
        if (IsDestroyed) return;
        blindUntil = Mathf.Max(blindUntil, Time.time + seconds);
        wobble = Random.value < .5f ? -1f : 1f;
    }

    private void FixedUpdate()
    {
        if (manager == null || IsDestroyed || IsRoadblock || State == Mode.Patrol || Time.timeScale <= 0f) return;
        float dt = Time.fixedDeltaTime;
        Vector3 position = body.position;
        if (!city.IsAreaReady(position)) { Speed = 0f; IsRammingBus = false; return; }
        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        Vector3 toBus = Flat(manager.BusPosition - position);
        Vector3 busForward = Flat(manager.BusBody.rotation * Vector3.forward).normalized;
        Vector3 fromBus = -toBus;
        float behind = -Vector3.Dot(fromBus, busForward);
        float lateral = Mathf.Abs(Vector3.Dot(fromBus, Vector3.Cross(Vector3.up, busForward)));
        float busSpeed = Vector3.Dot(manager.BusVelocity, busForward);
        bool linedUpBehindBus = behind > manager.BusHalfLength && lateral < 2.5f &&
            Vector3.Dot(forward, busForward) > .85f &&
            toBus.sqrMagnitude < 26f * 26f && Vector3.Angle(forward, toBus) < 20f;
        // Line up behind the bus, including when it is stopped. Acceleration belongs
        // to the attack itself; requiring existing closing speed deadlocked parked targets.
        IsRammingBus = InPursuit && SeesBus && !IsBlinded && RiceSpeedFactor > .9f &&
            Time.time >= nextRam && Time.time >= reverseUntil &&
            manager.CanAttemptRearRam(this) && linedUpBehindBus &&
            StaticClearance(position, toBus.normalized, toBus.magnitude) >= toBus.magnitude;
        float top = manager.ChaseSpeed;
        float desired;
        Vector3 goal;

        if (State == Mode.Return)
        {
            if (!hasLane) { manager.Recycle(this); return; }
            Vector3 offset = laneTarget - position; offset.y = 0;
            if (offset.magnitude < 3f) { StartPatrol(laneFrom, laneTo, laneTarget); return; }
            goal = offset.magnitude > 15f || StaticClearance(position, offset.normalized, offset.magnitude) < offset.magnitude
                ? RoutePoint(position, laneTarget, ref top) : laneTarget;
            desired = Mathf.Min(12f, top);
        }
        else if (IsBlinded)
        {
            // Inside the cloud: brake hard and swerve.
            goal = position + Quaternion.Euler(0, 35f * wobble, 0) * forward * 10f;
            desired = 0f;
        }
        else if (IsRammingBus)
        {
            goal = manager.BusPosition - busForward * manager.BusHalfLength;
            desired = Mathf.Min(top, Mathf.Max(0f, busSpeed) + manager.RamClosingSpeed);
        }
        else if (InPursuit && SeesBus && linedUpBehindBus && Mathf.Abs(busSpeed) < 2f)
        {
            // Hold the rear approach during the attack cooldown instead of orbiting
            // a stopped target. A retreat still moves backward below, then waits here.
            goal = manager.BusPosition - busForward * manager.BusHalfLength;
            desired = 0f;
        }
        else if (SeesBus && toBus.sqrMagnitude < 75f * 75f &&
            StaticClearance(position, toBus.normalized, toBus.magnitude) >= toBus.magnitude)
        {
            goal = PursuitPoint(position, busForward);
            Vector3 toGoal = Flat(goal - position);
            if (StaticClearance(position, toGoal.normalized, toGoal.magnitude) < toGoal.magnitude)
                goal = RoutePoint(position, goal, ref top);
            desired = Mathf.Min(top, Mathf.Max(6f, busSpeed + Mathf.Clamp((toBus.magnitude - 10f) * .3f, 0f, 6f)));
        }
        else
        {
            goal = RoutePoint(position, SeesBus ? PursuitPoint(position, busForward) : manager.TrackedPosition, ref top);
            desired = top;
        }

        if (!IsRammingBus) AvoidTraffic(position, forward, ref goal, ref desired);
        foreach (var other in manager.Cars)
        {
            if (other == this || other == null || !other.InPursuit) continue;
            Vector3 separation = Flat(position - other.transform.position);
            float distance = separation.magnitude;
            if (!IsRammingBus && distance > .01f && distance < 5f) goal += separation / distance * (5f - distance);
        }
        KeepOffBus(position, forward, ref desired);

        // Steering: a car cannot turn on the spot, less so at speed.
        Vector3 to = goal - position; to.y = 0;
        if (to.sqrMagnitude > .01f && Time.time >= ramRetreatUntil)
        {
            float angle = Vector3.SignedAngle(forward, to, Vector3.up);
            float rate = Mathf.Lerp(105f, 32f, Mathf.Clamp01(Speed / 32f));
            rate *= Mathf.Clamp01(Speed / 3f);
            yaw += Mathf.Clamp(angle, -rate * dt, rate * dt);
            if (Mathf.Abs(angle) > 25f)
                desired = Mathf.Min(desired, Mathf.Lerp(18f, 7f, Mathf.InverseLerp(25f, 100f, Mathf.Abs(angle))));
        }
        float braking = manager.ChaseAcceleration * (IsBlinded ? 2.5f : 1.6f);
        desired *= RiceSpeedFactor;
        Speed = Mathf.MoveTowards(Speed, desired, (desired > Speed ? manager.ChaseAcceleration : braking) * dt);
        forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        bool reversing = Time.time < reverseUntil && !IsBlinded;
        Vector3 movement = reversing ? -forward : forward;
        float step = (reversing ? 4f * RiceSpeedFactor : Speed) * dt;
        float clearance = StaticClearance(position, movement, step + .2f);
        float allowed = Mathf.Min(step, Mathf.Max(0f, clearance - .1f));
        if (!city.IsAreaReady(position + movement * allowed)) allowed = 0f;
        if (allowed < step * .5f && !IsBlinded && !reversing)
        {
            blockedFor += dt;
            Speed = Mathf.Min(Speed, 2f);
            IsRammingBus = false;
            if (blockedFor > 1.2f) { reverseUntil = Time.time + 1f; nextRoute = 0f; blockedFor = 0f; }
        }
        else blockedFor = 0f;
        if (reversing)
        {
            IsRammingBus = false;
            if (Time.time >= ramRetreatUntil) yaw += (approachSide < 0 ? -1f : 1f) * 45f * dt;
        }
        position += movement * allowed;
        position.y = Ground(position, body.position.y);
        body.MovePosition(position);
        body.MoveRotation(Quaternion.Euler(0, yaw, 0));
    }

    private void Update()
    {
        if (beacons == null) return;
        SetBeacons(!IsDestroyed && State != Mode.Patrol, Mathf.FloorToInt(Time.time * 6f));
    }

    private Vector3 PursuitPoint(Vector3 position, Vector3 busForward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, busForward);
        Vector3 offset = Flat(position - manager.BusPosition);
        float along = Vector3.Dot(offset, busForward);
        float side = Vector3.Dot(offset, right);
        // An officer in front first moves out of the bus's path, then joins the tail.
        if (along > -manager.BusHalfLength && Mathf.Abs(side) < 8f)
        {
            float flank = Mathf.Abs(side) > 1f ? Mathf.Sign(side) : approachSide;
            return manager.BusPosition + busForward * (Mathf.Max(0f, along) + 5f) + right * flank * 12f;
        }
        return manager.BusPosition - busForward * (manager.BusHalfLength + HalfLength + 5f) +
            right * (along > -manager.BusHalfLength ? Mathf.Sign(side) * 8f : approachSide);
    }

    public bool IsRearRamCollision(Collision collision)
    {
        if (!IsRammingBus || !InPursuit || IsDestroyed || IsBlinded || RiceSpeedFactor < .9f || manager == null) return false;
        var busBox = manager.BusBody.GetComponent<BoxCollider>();
        if (busBox == null) return false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            Vector3 local = busBox.transform.InverseTransformPoint(contact.point) - busBox.center;
            if (local.z > -busBox.size.z * .5f + .7f) continue;
            if (Mathf.Abs(Vector3.Dot(contact.normal, busBox.transform.forward)) < .5f) continue;
            if (Mathf.Abs(Vector3.Dot(collision.relativeVelocity, busBox.transform.forward)) < .5f) continue;
            return true;
        }
        return false;
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
            routeIndex = route.Count > 0 && Flat(route[0] - position).magnitude > 4f ? 0 : 1;
            routeGoal = destination;
            nextRoute = Time.time + .8f;
        }
        if (route.Count < 2) { top = 0f; return position; }
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

    // Kinematic pursuit cars must sweep scenery themselves to avoid crossing buildings.
    private float StaticClearance(Vector3 position, Vector3 direction, float distance)
    {
        if (distance <= .001f) return distance;
        Vector3 half = new Vector3(shape.size.x * .48f, .45f, shape.size.z * .48f);
        int count = Physics.BoxCastNonAlloc(position + Vector3.up, half, direction, obstacleHits,
            Quaternion.Euler(0, yaw, 0), distance, ~0, QueryTriggerInteraction.Ignore);
        float clear = count == obstacleHits.Length ? 0f : distance;
        for (int i = 0; i < count; i++)
        {
            var hit = obstacleHits[i];
            if (hit.rigidbody != null || hit.collider.transform.IsChildOf(transform) || hit.normal.y > .6f) continue;
            clear = Mathf.Min(clear, hit.distance);
        }
        return clear;
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
        if (IsRammingBus) return;
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
        if (!IsDestroyed && manager != null && collision.rigidbody != null && collision.rigidbody == manager.BusBody)
        {
            if (IsRearRamCollision(collision))
            {
                manager.RearRamContact();
                // Separate after contact so the next attack can hit a stationary bus again.
                reverseUntil = ramRetreatUntil = Time.time + .8f;
            }
            nextRam = Time.time + manager.RearRamCooldown;
            // Keep IsRammingBus through this callback so BusDamage sees the same impact state.
        }
    }

    public void DestroyByBus()
    {
        if (IsDestroyed) return;
        State = Mode.Destroyed;
        IsRammingBus = false;
        SeesBus = false;
        Speed = 0f;
        patrol.enabled = false;
        RemovePatrol();
        city.Pursuers.Remove(transform);
        SetBeacons(false, 0);
        manager.CarDestroyed(this);
        enabled = false;
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
