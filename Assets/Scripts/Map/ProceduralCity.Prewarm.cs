using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ProceduralCity
{
    private readonly Queue<Vector2Int> deliveryChunkRequests = new Queue<Vector2Int>();
    private readonly HashSet<Vector2Int> deliveryPreparationChunks = new HashSet<Vector2Int>();

    public IEnumerator PrepareDeliveryAddresses(int wanted)
    {
        var center = ChunkAt(player.position);
        var random = new System.Random(CoordinateSeed(center.x, center.y, 9011));
        int desired = Mathf.Max(32, wanted * 3), prepared = 0;
        int preparationBudget = Mathf.Clamp(wanted * 4, 48, 192);
        for (int ring = 2; ring <= 8; ring++)
        {
            var candidates = new List<Vector2Int>();
            for (int x = -ring; x <= ring; x++) for (int z = -ring; z <= ring; z++)
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == ring)
                    candidates.Add(center + new Vector2Int(x, z));
            for (int i = candidates.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp; }
            foreach (var coordinate in candidates)
            {
                int available = 0;
                foreach (var point in deliveries.deliveryPoints)
                    if (point != null && Vector3.Distance(point.transform.position, player.position) >= 160f) available++;
                if ((available >= desired && deliveries.HasEnoughDispersedAddresses(wanted)) ||
                    prepared >= preparationBudget || deliveries.HasFailed) yield break;
                if (FeatureChunk(coordinate) != null) continue;
                deliveryPreparationChunks.Add(coordinate);
                Vector3 location = Origin + new Vector3(coordinate.x * ChunkSize + 1, 0, coordinate.y * ChunkSize + 1);
                if (!IsAreaReady(location)) deliveryChunkRequests.Enqueue(coordinate);
                while (!IsAreaReady(location))
                {
                    if (deliveries.HasFailed) yield break;
                    yield return null; // The regular streaming budget builds this chunk.
                }
                prepared++;
            }
        }
    }

    public void FinishDeliveryPreparation()
    {
        deliveryPreparationChunks.Clear();
        deliveryChunkRequests.Clear();
    }
    public bool IsPrewarming { get; private set; }
    public float PrewarmProgress { get; private set; }

    // Uses the same generation steps and seed as streaming, including collisions,
    // delivery registration and minimap indexing. Works while gameplay is paused.
    public IEnumerator Prewarm(int radius = 2, float budgetMs = 6f)
    {
        if (IsPrewarming || WorldRoot == null || player == null)
            throw new InvalidOperationException("La ville ne peut pas être préparée.");
        IsPrewarming = true;
        PrewarmProgress = 0;
        try
        {
            var center = ChunkAt(player.position);
            radius = Mathf.Clamp(radius, 1, 4);
            int total = (radius * 2 + 1) * (radius * 2 + 1), completed = 0;
            double deadline = Time.realtimeSinceStartupAsDouble;
            // Finish a possible streaming operation before reusing its import queue.
            while (activeBuild != null)
            {
                if (!activeBuild.MoveNext())
                { (activeBuild as IDisposable)?.Dispose(); activeBuild = null; }
                yield return null;
            }
            for (int ring = 0; ring <= radius; ring++)
                for (int x = -ring; x <= ring; x++)
                    for (int z = -ring; z <= ring; z++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                        var coordinate = center + new Vector2Int(x, z);
                        if (!chunks.TryGetValue(coordinate, out var chunk) || !chunk.complete)
                        {
                            var steps = BuildChunkSteps(coordinate);
                            try
                            {
                                while (steps.MoveNext())
                                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                                    {
                                        yield return null;
                                        deadline = Time.realtimeSinceStartupAsDouble + Mathf.Clamp(budgetMs, 1, 10) / 1000.0;
                                    }
                            }
                            finally { (steps as IDisposable)?.Dispose(); }
                        }
                        PrewarmProgress = ++completed / (float)total;
                    }
            UpdateShop();
            UpdateTraffic(true);
            Physics.SyncTransforms();
        }
        finally { IsPrewarming = false; }
    }
}
