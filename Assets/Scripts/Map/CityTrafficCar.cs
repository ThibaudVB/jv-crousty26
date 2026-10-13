using System.Collections.Generic;
using UnityEngine;

// Ambient driver of the procedural street grid. It keeps to the right-hand lane,
// picks a random exit at each junction, obeys ProceduralCity's road rules and
// keeps a safe gap to whatever is ahead (other cars or the player's bus).
[RequireComponent(typeof(Rigidbody))]
public sealed class CityTrafficCar : MonoBehaviour
{
    public enum Turn { Straight, Right, Left, Back }

    public float cruiseSpeed = 11f;
    public float acceleration = 4f;
    public float braking = 9f;

    public Vector2Int From { get; private set; }
    public Vector2Int To { get; private set; }
    public Vector2Int Heading => To - From;
    // Junction reached after To, on the chosen exit.
    public Vector2Int Next => next;
    public Turn NextTurn { get; private set; }
    public float Speed { get; private set; }
    public float HalfLength { get; private set; }
    // Patrolling police car: obeys the same rules, managed by PoliceChaseManager.
    public bool IsPolice { get; set; }
    private float surfaceSpeedFactor = 1f;
    public void SetSurfaceSpeedFactor(float factor)
    {
        factor = Mathf.Clamp01(factor);
        if (factor < surfaceSpeedFactor) Speed *= factor / Mathf.Max(.01f, surfaceSpeedFactor);
        surfaceSpeedFactor = factor;
    }
    // Past the entry of the junction ahead: the car is committed to clear it.
    public bool InJunction => index > 0;

    // Road-rule progress at the junction ahead, owned by ProceduralCity.
    internal bool released;
    internal float stoppedAt = -1f;

    private const float ComfortBraking = 5f;
    private const float MinimumGap = 2.5f;
    private ProceduralCity city;
    private Rigidbody body;
    private Vector2Int next;
    private readonly List<Vector3> points = new List<Vector3>(12);
    private int index;
    private float blockedSince = -1f, ignoreTrafficUntil, nextHorn;

    public void Init(ProceduralCity owner, float halfLength)
    {
        city = owner;
        HalfLength = halfLength;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // Puts the car in the lane of the street from -> to, at the given world position.
    public void Place(Vector2Int from, Vector2Int to, Vector3 position)
    {
        From = from; To = to;
        next = IsPolice ? city.ChoosePolicePatrolExit(From, To) : city.ChooseExit(From, To);
        BuildLeg();
        Speed = cruiseSpeed * .7f;
        released = false; stoppedAt = -1f;
        blockedSince = -1f;
        var rotation = Quaternion.LookRotation(ProceduralCity.Flat(Heading));
        transform.SetPositionAndRotation(position, rotation);
        body.position = position; body.rotation = rotation;
    }

    private void FixedUpdate()
    {
        if (city == null) return;
        float dt = Time.fixedDeltaTime;
        Vector3 position = body.position;
        if (IsPolice && !city.IsAreaReady(position)) return;
        Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();

        float ruleRoom = city.ClearanceAhead(this, position);
        float trafficRoom = RoomBehindTraffic(position, forward, out bool bus, out CityTrafficCar blocker);
        float room = Mathf.Min(ruleRoom, trafficRoom);

        float desired = cruiseSpeed;
        float turnSpeed = NextTurn == Turn.Right ? 5f : NextTurn == Turn.Left ? 7f : NextTurn == Turn.Back ? 4f : cruiseSpeed;
        if (InJunction) desired = Mathf.Min(desired, turnSpeed);
        else desired = Mathf.Min(desired, Mathf.Sqrt(turnSpeed * turnSpeed + 2f * ComfortBraking * Vector3.Distance(position, points[0])));
        desired = Mathf.Min(desired, Mathf.Sqrt(2f * ComfortBraking * Mathf.Max(0f, room)));
        desired *= surfaceSpeedFactor;
        Speed = Mathf.MoveTowards(Speed, desired, (desired > Speed ? acceleration : braking) * dt);
        float step = Mathf.Min(Speed * dt, Mathf.Max(0f, room));
        if (IsPolice && !city.IsAreaReady(position + forward * (step + HalfLength))) step = 0f;
        if (room <= .01f) Speed = 0f;

        // Two cars waiting on each other inside a junction would wait forever: one of
        // them squeezes through. Queues on the street and the bus are never ignored.
        if (trafficRoom < ruleRoom && Speed < .2f)
        {
            if (blockedSince < 0f) blockedSince = Time.time;
            if (bus && Time.time - blockedSince > 2.5f && Time.time >= nextHorn)
            {
                CroustySoundDesignManager.Play(CroustySound.Horn, position);
                nextHorn = Time.time + 5f;
            }
            if (!bus && (InJunction || blocker != null && blocker.InJunction) && Time.time - blockedSince > 4f)
                ignoreTrafficUntil = Time.time + 2f;
        }
        else blockedSince = -1f;

        Vector3 start = position;
        for (int guard = 0; step > 0f && guard < 16; guard++)
        {
            Vector3 target = points[index]; target.y = position.y;
            float distance = Vector3.Distance(position, target);
            if (distance > step) { position += (target - position) / distance * step; break; }
            position = target; step -= distance;
            if (++index == points.Count) NextLeg();
        }
        body.MovePosition(position);
        Vector3 moved = position - start; moved.y = 0;
        if (moved.sqrMagnitude > 1e-6f)
            body.MoveRotation(Quaternion.Slerp(body.rotation, Quaternion.LookRotation(moved), 12f * dt));
    }

    private void NextLeg()
    {
        From = To; To = next;
        next = IsPolice ? city.ChoosePolicePatrolExit(From, To) : city.ChooseExit(From, To);
        released = false; stoppedAt = -1f;
        BuildLeg();
    }

    // One leg: the end of the straight approach, then a Bezier curve through the
    // junction ahead into the right-hand lane of the chosen exit.
    private void BuildLeg()
    {
        points.Clear(); index = 0;
        Vector2Int heading = Heading, exit = next - To;
        NextTurn = exit == heading ? Turn.Straight : exit == -heading ? Turn.Back :
            exit == new Vector2Int(heading.y, -heading.x) ? Turn.Right : Turn.Left;
        Vector3 entry = city.LanePoint(To, heading, -ProceduralCity.JunctionEntry);
        Vector3 leave = city.LanePoint(To, exit, ProceduralCity.JunctionEntry);
        points.Add(entry);
        if (NextTurn != Turn.Straight)
        {
            // Left turns cut inside the centre so that opposite left turns pass in front of each other.
            float handle = NextTurn == Turn.Right ? 2.8f : NextTurn == Turn.Left ? 3f : 8f;
            Vector3 a = entry + ProceduralCity.Flat(heading) * handle, b = leave - ProceduralCity.Flat(exit) * handle;
            for (int i = 1; i < 6; i++)
            {
                float t = i / 6f, u = 1f - t;
                points.Add(u * u * u * entry + 3f * u * u * t * a + 3f * u * t * t * b + t * t * t * leave);
            }
        }
        points.Add(leave);
    }

    // Free distance in front of the bumper, short of a safety gap.
    private float RoomBehindTraffic(Vector3 position, Vector3 forward, out bool bus, out CityTrafficCar blocker)
    {
        bus = false; blocker = null;
        float room = float.PositiveInfinity;
        Vector3 right = new Vector3(forward.z, 0, -forward.x);
        if (Time.time >= ignoreTrafficUntil)
            foreach (var other in city.Traffic)
            {
                if (other == null || other == this) continue;
                float gap = GapTo(other.transform.position, other.HalfLength, position, forward, right);
                if (gap < room) { room = gap; blocker = other; }
            }
        foreach (var pursuer in city.Pursuers)
        {
            if (pursuer == null || pursuer == transform) continue;
            float gap = GapTo(pursuer.position, 2.5f, position, forward, right);
            if (gap < room) { room = gap; blocker = null; }
        }
        if (city.player != null)
        {
            float gap = GapTo(city.player.position, 5f, position, forward, right);
            if (gap < room) { room = gap; bus = true; blocker = null; }
        }
        return room;
    }

    private float GapTo(Vector3 other, float otherHalfLength, Vector3 position, Vector3 forward, Vector3 right)
    {
        Vector3 offset = other - position; offset.y = 0;
        float ahead = Vector3.Dot(offset, forward);
        if (ahead <= 0f || ahead > 45f || Mathf.Abs(Vector3.Dot(offset, right)) > 2.8f) return float.PositiveInfinity;
        return ahead - HalfLength - otherHalfLength - MinimumGap;
    }
}
