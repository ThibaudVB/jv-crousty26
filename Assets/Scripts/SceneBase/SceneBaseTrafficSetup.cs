using UnityEngine;

// Select a forward waypoint before CarFollowPoints starts.
[DefaultExecutionOrder(-500)]
public sealed class SceneBaseTrafficSetup : MonoBehaviour
{
    public CarFollowPoints[] cars;

    private void Start()
    {
        var routes = FindObjectsByType<CarPointRoute>();
        foreach (var car in cars)
        {
            if (car == null) continue;
            CarPointRoute best = null;
            int index = 0;
            float distance = float.PositiveInfinity;
            foreach (var route in routes)
            for (int i = 0; i < route.transform.childCount; i++)
            {
                Vector3 delta = route.transform.GetChild(i).position - car.transform.position;
                delta.y = 0;
                if (Vector3.Dot(car.transform.forward, delta) < 0) continue;
                if (delta.sqrMagnitude >= distance) continue;
                distance = delta.sqrMagnitude;
                best = route;
                index = i;
            }
            if (best != null) car.FollowRoute(best, index);
            else Debug.LogError($"No forward traffic point for {car.name}.", car);
        }
    }
}
