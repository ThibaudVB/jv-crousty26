using System.Collections.Generic;
using UnityEngine;

public class TrafficWaypoint : MonoBehaviour
{
    [Tooltip("Points auxquels la voiture peut aller ensuite")]
    public List<TrafficWaypoint> nextWaypoints = new List<TrafficWaypoint>();

    public TrafficWaypoint GetRandomNext(TrafficWaypoint previous = null)
    {
        if (nextWaypoints == null || nextWaypoints.Count == 0)
            return null;

        if (nextWaypoints.Count == 1)
            return nextWaypoints[0];

        List<TrafficWaypoint> choices = new List<TrafficWaypoint>(nextWaypoints);
        choices.Remove(previous);

        if (choices.Count == 0)
            return nextWaypoints[Random.Range(0, nextWaypoints.Count)];

        return choices[Random.Range(0, choices.Count)];
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = nextWaypoints.Count > 1 ? Color.yellow : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.6f);

        Gizmos.color = Color.green;
        foreach (TrafficWaypoint next in nextWaypoints)
        {
            if (next == null) continue;

            Vector3 a = transform.position;
            Vector3 b = next.transform.position;
            Gizmos.DrawLine(a, b);

            // Petite flèche pour voir le sens
            Vector3 dir = (b - a).normalized;
            Vector3 mid = Vector3.Lerp(a, b, 0.5f);
            Vector3 right = Quaternion.Euler(0, 150, 0) * dir;
            Vector3 left = Quaternion.Euler(0, -150, 0) * dir;
            Gizmos.DrawLine(mid, mid + right * 1.2f);
            Gizmos.DrawLine(mid, mid + left * 1.2f);
        }
    }
}