using UnityEngine;
using System.Collections.Generic;

// Marks every solid part of a roadblock, including its parked police cars.
// BusDamage applies contact damage; BusCarCrash must never clear these obstacles.
[DisallowMultipleComponent]
public sealed class PoliceRoadblock : MonoBehaviour
{
    public float CreatedAt { get; private set; }
    private void Awake() => CreatedAt = Time.time;
    private static readonly HashSet<PoliceRoadblock> active = new HashSet<PoliceRoadblock>();
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    // Includes parked cars and clearance for the bus. Intersection checks are planar.
    public static bool BlocksSegment(Vector3 from, Vector3 to)
    {
        foreach (var block in active)
        {
            if (block == null || !block.isActiveAndEnabled) continue;
            Vector3 a = block.transform.InverseTransformPoint(from);
            Vector3 b = block.transform.InverseTransformPoint(to);
            a.y = b.y = 0f;
            var footprint = new Bounds(new Vector3(0f, 0f, 6f), new Vector3(22f, 2f, 24f));
            if (footprint.Contains(a) || footprint.Contains(b)) return true;
            Vector3 segment = b - a;
            if (segment.sqrMagnitude > .0001f && footprint.IntersectRay(new Ray(a, segment.normalized), out float distance)
                && distance <= segment.magnitude) return true;
        }
        return false;
    }
}
