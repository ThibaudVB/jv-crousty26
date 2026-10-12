using System.Collections.Generic;
using UnityEngine;

public sealed partial class ProceduralCity
{
    public enum RoadRule { None, TrafficLight, Yield, Stop }
    public enum LightAspect { Red, Amber, Green }

    private static readonly Vector2Int[] RoadDirections =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private const float StopLineDistance = 18f;
    private const float SignalCycle = 30f;
    private Material signalHousing, signalDark, signalRed, signalAmber, signalGreen, stopRed;
    private GameObject stopSignModel;
    private Bounds stopSignBounds;
    private readonly Dictionary<CarFollowPoints, StopProgress> stopProgress = new Dictionary<CarFollowPoints, StopProgress>();

    private sealed class StopProgress
    {
        public Vector2Int junction;
        public Vector2Int approach;
        public float stoppedAt = -1f;
        public bool released;
    }

    private sealed class SignalDisplay
    {
        public Vector2Int junction;
        public Vector2Int approach;
        public Renderer[] bulbs;
        public MapImpactProp impact;
        public bool broken;
        public LightAspect lastAspect = (LightAspect)(-1);
    }

    private sealed class MarkingBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        public void Rect(float x, float z, float width, float depth)
        {
            float left = x - width * .5f, right = x + width * .5f;
            float low = z - depth * .5f, high = z + depth * .5f;
            int i = vertices.Count;
            vertices.Add(new Vector3(left, .025f, low));
            vertices.Add(new Vector3(left, .025f, high));
            vertices.Add(new Vector3(right, .025f, high));
            vertices.Add(new Vector3(right, .025f, low));
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }
        public void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            int i = vertices.Count;
            vertices.Add(new Vector3(a.x, .026f, a.y));
            vertices.Add(new Vector3(b.x, .026f, b.y));
            vertices.Add(new Vector3(c.x, .026f, c.y));
            // In the XZ plane, clockwise winding points upward.
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            triangles.Add(i);
            triangles.Add(i + (cross < 0 ? 1 : 2));
            triangles.Add(i + (cross < 0 ? 2 : 1));
        }
        public Mesh Finish()
        {
            var mesh = new Mesh { name = "Passages pietons et marquages" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    private void PrepareRoadMaterials()
    {
        signalHousing = Material(new Color(.12f, .15f, .18f));
        signalDark = Material(new Color(.12f, .12f, .12f));
        signalRed = GlowingMaterial(new Color(1f, .08f, .055f));
        signalAmber = GlowingMaterial(new Color(1f, .55f, .025f));
        signalGreen = GlowingMaterial(new Color(.08f, 1f, .22f));
        stopRed = Material(new Color(.82f, .055f, .055f));
        stopSignModel = Resources.Load<GameObject>("Crousty/StopSign");
        if (stopSignModel != null)
        {
            bool found = false;
            foreach (var filter in stopSignModel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Matrix4x4 matrix = stopSignModel.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!found) { stopSignBounds = new Bounds(corner, Vector3.zero); found = true; }
                    else stopSignBounds.Encapsulate(corner);
                }
            }
        }
    }

    private static Material GlowingMaterial(Color color)
    {
        var result = Material(color);
        result.EnableKeyword("_EMISSION");
        result.SetColor("_EmissionColor", color * 3f);
        return result;
    }

    private void DisposeRoadMaterials()
    {
        foreach (var material in new[] { signalHousing, signalDark, signalRed, signalAmber, signalGreen, stopRed })
            if (material != null) Destroy(material);
    }

    private int OpenApproaches(Vector2Int junction)
    {
        int mask = 0;
        for (int i = 0; i < RoadDirections.Length; i++)
            if (IsRoadEdgeOpen(junction, junction + RoadDirections[i])) mask |= 1 << i;
        return mask;
    }

    // The same graph decides the GPS route, the markings, the signs and AI priority.
    public RoadRule RuleAt(Vector2Int junction, Vector2Int approach)
    {
        int mask = OpenApproaches(junction);
        int count = 0;
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
        if (count < 3 || !IsRoadEdgeOpen(junction, junction + approach)) return RoadRule.None;
        uint choice = (uint)CoordinateSeed(junction.x, junction.y, 7204);
        if (count == 4 && choice % 3 == 0) return RoadRule.TrafficLight;
        bool northSouthThrough = (mask & 5) == 5;
        bool eastWestThrough = (mask & 10) == 10;
        bool minor = count == 4 ? approach.x != 0 :
            northSouthThrough ? approach.x != 0 : eastWestThrough && approach.y != 0;
        if (!minor) return RoadRule.None;
        return choice % 5 == 0 ? RoadRule.Stop : RoadRule.Yield;
    }

    public LightAspect LightAt(Vector2Int junction, Vector2Int approach, float time)
    {
        float phase = Mathf.Repeat(time + (uint)CoordinateSeed(junction.x, junction.y, 7205) % 30, SignalCycle);
        bool northSouth = approach.x == 0;
        if (northSouth && phase < 12f || !northSouth && phase >= 15f && phase < 27f) return LightAspect.Green;
        if (northSouth && phase < 14f || !northSouth && phase >= 27f && phase < 29f) return LightAspect.Amber;
        return LightAspect.Red; // one second of all-red between conflicting phases
    }

    private System.Collections.IEnumerator BuildRoadRuleSteps(Chunk chunk, Vector2Int coordinate)
    {
        var paint = new MarkingBuilder();
        int gx = coordinate.x * 2, gz = coordinate.y * 2;
        // Each chunk owns two by two junctions. Their approaches may extend into a
        // neighbour, but only one chunk creates each crossing and signal.
        for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++)
        {
            var junction = new Vector2Int(gx + x, gz + z);
            Vector2 center = new Vector2(x * BlockSize, z * BlockSize);
            for (int d = 0; d < 4; d++)
            {
                Vector2Int outward = RoadDirections[d];
                if (!IsRoadEdgeOpen(junction, junction + outward)) continue;
                AddCrosswalk(paint, center, outward);
                RoadRule rule = RuleAt(junction, outward);
                if (rule != RoadRule.None)
                {
                    AddStopLine(paint, center, outward);
                    if (rule == RoadRule.Yield) AddYieldTeeth(paint, center, outward);
                    if (rule == RoadRule.TrafficLight) AddSignal(chunk, junction, center, outward);
                    else if (rule == RoadRule.Stop) AddStopSign(chunk, center, outward);
                }
            }
            yield return null;
            // Centre lines only on existing edges. Leave their ends clear for zebra crossings.
            if (IsRoadEdgeOpen(junction, junction + Vector2Int.right))
                for (int k = 0; k < 4; k++) paint.Rect(center.x + 21 + k * 7, center.y, 3.1f, .2f);
            if (IsRoadEdgeOpen(junction, junction + Vector2Int.up))
                for (int k = 0; k < 4; k++) paint.Rect(center.x, center.y + 21 + k * 7, .2f, 3.1f);
        }
        chunk.roadMarkings = paint.Finish();
        var objectWithMesh = new GameObject("Passages pietons et lignes", typeof(MeshFilter), typeof(MeshRenderer));
        objectWithMesh.transform.SetParent(chunk.root.transform, false);
        objectWithMesh.GetComponent<MeshFilter>().sharedMesh = chunk.roadMarkings;
        objectWithMesh.GetComponent<MeshRenderer>().sharedMaterial = markings;
    }

    private static Vector2 RightOfIncoming(Vector2Int outward) => new Vector2(-outward.y, outward.x);

    private static void AddCrosswalk(MarkingBuilder paint, Vector2 center, Vector2Int outward)
    {
        // Eight broad zebra bars span the complete 16 m road. The gap to the
        // stop line leaves a safe pedestrian waiting area at each corner.
        for (int i = 0; i < 8; i++)
        {
            float lateral = (i - 3.5f) * 1.75f;
            if (outward.x == 0) paint.Rect(center.x + lateral, center.y + outward.y * 11f, 1.25f, 3.3f);
            else paint.Rect(center.x + outward.x * 11f, center.y + lateral, 3.3f, 1.25f);
        }
    }

    private static void AddStopLine(MarkingBuilder paint, Vector2 center, Vector2Int outward)
    {
        Vector2 position = center + (Vector2)outward * StopLineDistance + RightOfIncoming(outward) * 4f;
        if (outward.x == 0) paint.Rect(position.x, position.y, 7f, .42f);
        else paint.Rect(position.x, position.y, .42f, 7f);
    }

    private static void AddYieldTeeth(MarkingBuilder paint, Vector2 center, Vector2Int outward)
    {
        Vector2 right = RightOfIncoming(outward);
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = center + (Vector2)outward * 20f + right * (1.6f + i * 1.6f);
            paint.Triangle(at - right * .55f, at + right * .55f, at + (Vector2)outward * 1.1f);
        }
    }

    private void AddSignal(Chunk chunk, Vector2Int junction, Vector2 center, Vector2Int outward)
    {
        Vector2 right = RightOfIncoming(outward);
        Vector2 spot = center + (Vector2)outward * 14f + right * 9f;
        var post = new GameObject("Feu tricolore " + junction + " " + outward);
        post.SetActive(false);
        post.transform.SetParent(chunk.root.transform, false);
        post.transform.localPosition = new Vector3(spot.x, 0, spot.y);
        post.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y));
        if (catalog.TryGetValue("TrafficLight_001__Block_r0_c0_StreetProps", out var source))
            Visual(source, post.transform, Vector3.zero, Vector3.one, 0f, false);
        else Box("Mat", post.transform, new Vector3(0, 1.9f, 0), new Vector3(.18f, 3.8f, .18f), signalHousing, null, false);
        Box("Boitier", post.transform, new Vector3(0, 3.15f, .17f), new Vector3(.56f, 1.35f, .30f), signalHousing, null, false);
        var display = new SignalDisplay { junction = junction, approach = outward, bulbs = new Renderer[3] };
        for (int i = 0; i < 3; i++)
        {
            var bulb = Box(i == 0 ? "Rouge" : i == 1 ? "Orange" : "Vert", post.transform,
                new Vector3(0, 3.53f - i * .39f, .36f), new Vector3(.34f, .34f, .08f), signalDark, null, false);
            display.bulbs[i] = bulb.GetComponent<Renderer>();
        }
        chunk.signals.Add(display);
        SetSignalAspect(display, LightAt(junction, outward, Time.time));
        display.impact = ConfigureImpact(post, true);
        post.SetActive(true);
    }

    private void AddStopSign(Chunk chunk, Vector2 center, Vector2Int outward)
    {
        Vector2 spot = center + (Vector2)outward * 21f + RightOfIncoming(outward) * 9f;
        var root = new GameObject("Panneau STOP");
        root.SetActive(false);
        root.transform.SetParent(chunk.root.transform, false);
        root.transform.localPosition = new Vector3(spot.x, 0, spot.y);
        root.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y));
        if (stopSignModel != null)
        {
            var model = Instantiate(stopSignModel, root.transform, false);
            model.name = "STOP - Poly by Google";
            float scale = 3.4f / Mathf.Max(.01f, stopSignBounds.size.y);
            Quaternion orientation = Quaternion.Euler(0, 90f, 0);
            model.transform.localRotation = orientation;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -(orientation * new Vector3(stopSignBounds.center.x, stopSignBounds.min.y, stopSignBounds.center.z)) * scale;
            ConfigureImpact(root, true);
            root.SetActive(true);
            return;
        }
        Box("Mat STOP", root.transform, new Vector3(0, 1.3f, 0), new Vector3(.12f, 2.6f, .12f), signalHousing, null, false);
        var face = new GameObject("Octogone STOP", typeof(MeshFilter), typeof(MeshRenderer));
        face.transform.SetParent(root.transform, false);
        face.transform.localPosition = new Vector3(0, 2.7f, .08f);
        var vertices = new Vector3[8];
        int[] indices = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7 };
        for (int i = 0; i < 8; i++)
        {
            float angle = (22.5f + i * 45f) * Mathf.Deg2Rad;
            vertices[i] = new Vector3(Mathf.Cos(angle) * .68f, Mathf.Sin(angle) * .68f, 0);
        }
        var mesh = new Mesh { name = "Octogone STOP" };
        mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateNormals();
        face.GetComponent<MeshFilter>().sharedMesh = mesh;
        face.GetComponent<MeshRenderer>().sharedMaterial = stopRed;
        var back = new GameObject("Dos du panneau", typeof(MeshFilter), typeof(MeshRenderer));
        back.transform.SetParent(face.transform, false);
        back.transform.localPosition = new Vector3(0, 0, -.04f);
        back.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        back.GetComponent<MeshFilter>().sharedMesh = mesh;
        back.GetComponent<MeshRenderer>().sharedMaterial = signalHousing;
        // The shared octagon mesh is owned by the chunk, like its road-marking mesh.
        chunk.signMeshes.Add(mesh);
        var text = new GameObject("STOP").AddComponent<TextMesh>();
        text.transform.SetParent(face.transform, false);
        text.transform.localPosition = new Vector3(0, 0, .015f);
        text.text = "STOP"; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.fontSize = 64; text.characterSize = .27f; text.color = Color.white;
        ConfigureImpact(root, true);
        root.SetActive(true);
    }

    private void SetSignalAspect(SignalDisplay display, LightAspect aspect)
    {
        display.lastAspect = aspect;
        display.bulbs[0].sharedMaterial = aspect == LightAspect.Red ? signalRed : signalDark;
        display.bulbs[1].sharedMaterial = aspect == LightAspect.Amber ? signalAmber : signalDark;
        display.bulbs[2].sharedMaterial = aspect == LightAspect.Green ? signalGreen : signalDark;
    }

    private void RefreshSignals()
    {
        foreach (var chunk in chunks.Values)
            foreach (var display in chunk.signals)
            {
                if (display.impact != null && display.impact.HasBeenHit)
                {
                    if (!display.broken)
                    {
                        foreach (var bulb in display.bulbs) if (bulb != null) bulb.sharedMaterial = signalDark;
                        display.broken = true;
                    }
                    continue;
                }
                LightAspect aspect = LightAt(display.junction, display.approach, Time.time);
                if (aspect != display.lastAspect) SetSignalAspect(display, aspect);
            }
    }

    // Called only by cars attached to generated routes; their other scene routes are untouched.
    public float AllowedTrafficStep(CarFollowPoints car, Vector3 position, Vector3 target, float requestedStep)
    {
        var junction = new Vector2Int(Mathf.RoundToInt((target.x - Origin.x) / BlockSize),
            Mathf.RoundToInt((target.z - Origin.z) / BlockSize));
        Vector3 node = Origin + new Vector3(junction.x * BlockSize, 0, junction.y * BlockSize);
        if (Mathf.Abs(target.x - node.x) > 9f || Mathf.Abs(target.z - node.z) > 9f) return requestedStep;
        Vector2Int approach = Mathf.Abs(target.x - position.x) > Mathf.Abs(target.z - position.z)
            ? new Vector2Int(position.x < target.x ? -1 : 1, 0)
            : new Vector2Int(0, position.z < target.z ? -1 : 1);
        RoadRule rule = RuleAt(junction, approach);
        if (rule == RoadRule.None) return requestedStep;
        if (!stopProgress.TryGetValue(car, out var state)) stopProgress[car] = state = new StopProgress();
        if (state.junction != junction || state.approach != approach)
        {
            state.junction = junction; state.approach = approach;
            state.stoppedAt = -1f; state.released = false;
        }
        if (state.released) return requestedStep;
        float distance = approach.x != 0 ? Mathf.Abs(position.x - node.x) : Mathf.Abs(position.z - node.z);
        // The car centre waits two metres before the painted line (front bumper).
        const float centreStop = StopLineDistance + 2f;
        // A car already inside the junction must be allowed to clear it even if
        // the signal changes while it turns.
        if (distance < centreStop - 1f) { state.released = true; return requestedStep; }
        if (distance > centreStop + .01f)
            return Mathf.Min(requestedStep, distance - centreStop);
        if (rule == RoadRule.TrafficLight)
        {
            if (LightAt(junction, approach, Time.time) != LightAspect.Green) return 0f;
            state.released = true;
            return requestedStep;
        }
        if (state.stoppedAt < 0f) state.stoppedAt = Time.time;
        if (rule == RoadRule.Stop && Time.time - state.stoppedAt < .8f) return 0f;
        if (PriorityTrafficNear(car, junction, approach)) return 0f;
        state.released = true;
        return requestedStep;
    }

    private bool PriorityTrafficNear(CarFollowPoints car, Vector2Int junction, Vector2Int approach)
    {
        Vector3 node = Origin + new Vector3(junction.x * BlockSize, 0, junction.y * BlockSize);
        foreach (var other in cars)
        {
            if (other == null || other == car || other.proceduralCity != this || other.CurrentPoint == null) continue;
            Vector3 delta = other.transform.position - node;
            if (delta.x * delta.x + delta.z * delta.z > 25f * 25f) continue;
            Vector3 target = other.CurrentPoint.position;
            if (Mathf.Abs(target.x - node.x) > 9f || Mathf.Abs(target.z - node.z) > 9f) continue;
            Vector2Int otherApproach = Mathf.Abs(target.x - other.transform.position.x) > Mathf.Abs(target.z - other.transform.position.z)
                ? new Vector2Int(other.transform.position.x < target.x ? -1 : 1, 0)
                : new Vector2Int(0, other.transform.position.z < target.z ? -1 : 1);
            if (otherApproach.x == approach.x && otherApproach.y == approach.y) continue;
            if (otherApproach.x != approach.x && otherApproach.y != approach.y &&
                RuleAt(junction, otherApproach) == RoadRule.None) return true;
        }
        return false;
    }
}
