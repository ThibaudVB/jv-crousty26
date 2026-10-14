using System;
using System.Collections;
using UnityEngine;

public sealed partial class ProceduralCity
{
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
