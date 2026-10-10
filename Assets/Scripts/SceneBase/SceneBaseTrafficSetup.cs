using UnityEngine;
using UnityEngine.AI;

// Places agents after the baked surface is registered and before the traffic drivers start.
[DefaultExecutionOrder(-500)]
public sealed class SceneBaseTrafficSetup : MonoBehaviour
{
    public TrafficCarNavMesh[] cars;
    public float spawnSearchRadius = 5f;

    private void Start()
    {
        int road = NavMesh.GetAreaFromName("Road");
        int mask = road >= 0 ? 1 << road : NavMesh.AllAreas;
        foreach (var car in cars)
        {
            if (car == null) continue;
            var agent = car.GetComponent<NavMeshAgent>();
            if (NavMesh.SamplePosition(car.transform.position, out var hit, spawnSearchRadius, mask))
            {
                agent.areaMask = mask;
                agent.Warp(hit.position);
            }
            else
            {
                car.enabled = false;
                Debug.LogError($"SceneBase: no road NavMesh at traffic spawn {car.name}.", car);
            }
        }
    }
}
