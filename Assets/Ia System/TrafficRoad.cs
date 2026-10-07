using UnityEngine;

/// <summary>
/// Route simple À SENS UNIQUE. Ses enfants (dans l'ordre de la hiérarchie)
/// sont les points à suivre, dans le sens de circulation.
/// À la fin : soit une intersection (choix de sortie), soit une route suivante.
/// Route à double sens = deux TrafficRoad, une par sens.
/// </summary>
public class TrafficRoad : MonoBehaviour
{
    [Header("Fin de la route")]
    [Header("Intersection à la fin de cette route (prioritaire)")]
    public TrafficIntersection endIntersection;
    [Header("Route qui continue directement, s'il n'y a pas d'intersection")]
    public TrafficRoad nextRoad;

    public int PointCount => transform.childCount;

    public Vector3 GetPoint(int index)
    {
        return transform.GetChild(index).position;
    }

    /// <summary>Route sur laquelle la voiture continue après le dernier point.</summary>
    public TrafficRoad GetNextRoad()
    {
        if (endIntersection != null)
            return endIntersection.GetRandomExit(this);

        return nextRoad;
    }

    private void OnDrawGizmos()
    {
        int count = PointCount;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = GetPoint(i);

            Gizmos.color = i == 0 ? Color.green : (i == count - 1 ? Color.red : Color.cyan);
            Gizmos.DrawSphere(p, 0.5f);

            if (i < count - 1)
            {
                Vector3 next = GetPoint(i + 1);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(p, next);

                // Flèche de sens
                Vector3 dir = (next - p).normalized;
                Vector3 mid = Vector3.Lerp(p, next, 0.5f);
                Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, 150, 0) * dir * 1.5f);
                Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, -150, 0) * dir * 1.5f);
            }
        }

        // Lien vers la suite
        Gizmos.color = Color.yellow;
        Vector3 last = GetPoint(count - 1);
        if (endIntersection != null)
            Gizmos.DrawLine(last, endIntersection.transform.position);
        else if (nextRoad != null && nextRoad.PointCount > 0)
            Gizmos.DrawLine(last, nextRoad.GetPoint(0));
    }
}