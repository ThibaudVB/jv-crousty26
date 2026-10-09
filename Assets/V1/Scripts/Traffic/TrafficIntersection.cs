using System.Collections.Generic;
using UnityEngine;

namespace V1
{
    /// <summary>
    /// Intersection : les voitures qui y arrivent choisissent une sortie au hasard
    /// parmi les routes de la liste "Exits" (routes dont le début part de l'intersection).
    /// </summary>
    public class TrafficIntersection : MonoBehaviour
    {
        [Tooltip("Routes qui partent de cette intersection")]
        public List<TrafficRoad> exits = new List<TrafficRoad>();

        public float gizmoRadius = 3f;

        private readonly List<TrafficRoad> choices = new List<TrafficRoad>();

        /// <summary>
        /// Choisit une sortie au hasard, sans reprendre la route d'où l'on vient
        /// (pas de demi-tour) s'il existe un autre choix.
        /// </summary>
        public TrafficRoad GetRandomExit(TrafficRoad cameFrom)
        {
            choices.Clear();
            foreach (TrafficRoad road in exits)
                if (road != null)
                    choices.Add(road);

            if (choices.Count > 1)
                choices.Remove(cameFrom);

            return choices.Count == 0 ? null : choices[Random.Range(0, choices.Count)];
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);

            foreach (TrafficRoad r in exits)
            {
                if (r == null || r.PointCount == 0)
                    continue;
                Gizmos.DrawLine(transform.position, r.GetPoint(0));
            }
        }
    }
}
