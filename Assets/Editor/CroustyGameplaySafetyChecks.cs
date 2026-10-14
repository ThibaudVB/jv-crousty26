using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Explicit Play-mode checks. Temporary delivery data is restored before returning to gameplay.
public static class CroustyGameplaySafetyChecks
{
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Tools/Crousty/Verifier les correctifs en Play")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        { Debug.LogWarning("Lancer SceneBase en Play avant les controles de gameplay."); return; }
        var city = UnityEngine.Object.FindAnyObjectByType<ProceduralCity>();
        var deliveries = DeliveryManager.Instance;
        if (city == null || city.WorldRoot == null || deliveries == null || deliveries.player == null)
            throw new InvalidOperationException("Ville et bus actifs requis.");
        int buildings = 0;
        foreach (var mesh in city.WorldRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (!mesh.name.StartsWith("Visuel Building_") && !mesh.name.EndsWith("_Shop")) continue;
            var core = mesh.GetComponent<BoxCollider>();
            Check(core != null && core.enabled && !core.isTrigger && core.bounds.Contains(mesh.GetComponent<Renderer>().bounds.center),
                "Collision de secours du batiment " + mesh.name);
            foreach (var material in mesh.GetComponent<Renderer>().sharedMaterials)
                if (material != null && material.HasProperty("_Surface")) Check(material.GetFloat("_Surface") == 0f, "Facade opaque");
            buildings++;
        }
        Check(buildings > 0, "Batiments proceduraux presents");
        foreach (var part in city.WorldRoot.GetComponentsInChildren<Transform>())
            if (part.name.StartsWith("Feu tricolore ") || part.name == "Panneau STOP")
                Check(part.GetComponent<MapImpactProp>() == null && part.GetComponent<BoxCollider>() != null,
                    "STOP et feux solides et indestructibles");

        var garages = new List<Vector2Int>();
        var regions = new HashSet<Vector2Int>();
        for (int x = -24; x <= 24; x++) for (int z = -24; z <= 24; z++)
        {
            if (!city.IsGarageBlock(x, z)) continue;
            var region = new Vector2Int(Mathf.FloorToInt(x / 8f), Mathf.FloorToInt(z / 8f));
            Check(regions.Add(region), "Au plus un garage par region de 512 m");
            foreach (var previous in garages) Check(Vector2Int.Distance(previous, new Vector2Int(x, z)) >= 5f, "Garages espaces d'au moins 320 m");
            garages.Add(new Vector2Int(x, z));
        }
        Check(garages.Count > 0, "Garages accessibles dans la ville");
        var follow = Camera.main != null ? Camera.main.GetComponent<BusCameraFollow>() : null;
        Check(follow != null && follow.cameraLocked && Camera.main.fieldOfView >= Mathf.Min(follow.baseFOV, follow.maxFOV) - .1f && Camera.main.fieldOfView <= Mathf.Max(follow.baseFOV, follow.maxFOV) + .1f, "Camera verrouillee avec effet de vitesse borne");
        var lights = deliveries.player.GetComponent<BusNightLights>();
        Check(lights != null && lights.headlightRange >= 100f && lights.headlightIntensity >= 80f, "Phares renforces");
        Check(float.IsPositiveInfinity(deliveries.RunTimeLimit), "Tournees sans limite de temps");
        if (!deliveries.HasFailed)
        {
            float elapsed = deliveries.RunElapsed; bool active = deliveries.RunActive;
            try
            {
                typeof(DeliveryManager).GetField("<RunElapsed>k__BackingField", Fields).SetValue(deliveries, 1000000f);
                typeof(DeliveryManager).GetField("<RunActive>k__BackingField", Fields).SetValue(deliveries, true);
                typeof(DeliveryManager).GetMethod("UpdateTimer", Fields).Invoke(deliveries, null);
                Check(!deliveries.HasFailed, "Une tournee ne peut plus echouer avec le temps");
            }
            finally
            {
                typeof(DeliveryManager).GetField("<RunElapsed>k__BackingField", Fields).SetValue(deliveries, elapsed);
                typeof(DeliveryManager).GetField("<RunActive>k__BackingField", Fields).SetValue(deliveries, active);
            }
        }
        CheckDestinations(deliveries);
        Debug.Log("GAMEPLAY_SAFETY_CHECKS_PASSED: collisions, garages, camera, phares, chrono et destinations.");
    }

    private static void CheckDestinations(DeliveryManager manager)
    {
        var original = manager.deliveryPoints;
        var visible = (List<DeliveryPoint>)manager.VisibleDestinations;
        var originalVisible = new List<DeliveryPoint>(visible);
        var root = new GameObject("Temporary delivery checks");
        root.SetActive(false);
        try
        {
            manager.deliveryPoints = new List<DeliveryPoint>();
            visible.Clear();
            for (int i = 0; i < 40; i++)
            {
                var address = new GameObject("Test address " + i);
                address.transform.SetParent(root.transform, false);
                address.transform.position = manager.player.position + new Vector3(1000 + (i % 8) * 200, 0, 1000 + (i / 8) * 200);
                var point = address.AddComponent<DeliveryPoint>();
                point.marker = new GameObject("Test marker");
                point.marker.transform.SetParent(address.transform, false);
                point.SetOrderActive(true);
                manager.deliveryPoints.Add(point);
            }
            root.SetActive(true);
            var candidates = (List<DeliveryPoint>)typeof(DeliveryManager).GetMethod("FindNearbyAddresses", Fields).Invoke(manager, new object[] { 40 });
            Check(candidates.Count == 40, "40 adresses dispersees restent disponibles");
            Refresh(manager);
            Check(visible.Count == 10, "40 commandes affichent seulement dix destinations");
            var removed = visible[0];
            removed.MarkPending();
            Refresh(manager);
            Check(visible.Count == 10 && !visible.Contains(removed), "Une destination est remplacee des qu'un colis part");
            int markers = 0;
            foreach (var point in manager.deliveryPoints) if (point.marker.activeSelf) markers++;
            Check(markers == 10, "Dix balises maximum dans le monde");
        }
        finally
        {
            manager.deliveryPoints = original;
            visible.Clear(); visible.AddRange(originalVisible);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Refresh(DeliveryManager manager) => typeof(DeliveryManager).GetMethod("RefreshVisibleDestinations", Fields).Invoke(manager, null);
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("GAMEPLAY_SAFETY_CHECK_FAILED: " + message); }
}
