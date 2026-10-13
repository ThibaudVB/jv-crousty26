using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Run in the isolated validation project using StreamingChecksSetup.Run.
public sealed class StreamingChecks : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int passed;
    void Check(bool result, string message)
    {
        if (!result)
        {
            File.WriteAllText("streaming-result.txt", "FAILED: " + message);
            Debug.LogError("STREAMING FAILED: " + message);
            EditorApplication.Exit(1);
            throw new Exception(message);
        }
        passed++;
        Debug.Log("STREAMING PASS: " + message);
    }
    object Field(ProceduralCity city, string name) => typeof(ProceduralCity).GetField(name, Private).GetValue(city);
    object Call(ProceduralCity city, string name, params object[] args) => typeof(ProceduralCity).GetMethod(name, Private).Invoke(city, args);
    void Tick(ProceduralCity city)
    {
        typeof(ProceduralCity).GetField("nextMaintenance", Private).SetValue(city, 0f);
        Call(city, "Update");
    }
    string Fingerprint(Transform root) => string.Join(";", root.GetComponentsInChildren<MeshFilter>()
        .Select(m => m.name + m.transform.position.ToString("F3") + m.transform.rotation.ToString("F3")));

    IEnumerator Start()
    {
        var city = FindAnyObjectByType<ProceduralCity>();
        city.enabled = false;
        city.generationBudgetMs = .5f;
        var manager = city.deliveries;
        Check(city.LoadedChunkCount == 9, "initial neighbourhood is ready before gameplay");
        Check(RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear, "distance fog is enabled");
        string original = Fingerprint(city.WorldRoot.Find("Quartier 0,0"));
        var coordinate = new Vector2Int(7, 7);
        var steps = (IEnumerator)Call(city, "BuildChunkSteps", coordinate);
        steps.MoveNext();
        var chunk = city.WorldRoot.Find("Quartier 7,7");
        Check(chunk.GetComponentsInChildren<MeshFilter>().Length == 1, "first step creates only safe ground");
        steps.MoveNext();
        int before = chunk.GetComponentsInChildren<MeshFilter>().Count(m => m.name.StartsWith("Visuel "));
        steps.MoveNext();
        int after = chunk.GetComponentsInChildren<MeshFilter>().Count(m => m.name.StartsWith("Visuel "));
        Check(before == 0 && after == 1, "imported meshes and colliders are built one at a time");
        while (steps.MoveNext()) { }
        string streamed = Fingerprint(chunk);
        Call(city, "UnloadChunk", coordinate);
        Call(city, "LoadChunk", coordinate);
        Check(Fingerprint(city.WorldRoot.Cast<Transform>().First(t => t.name == "Quartier 7,7" && t.gameObject.activeSelf)) == streamed,
            "incremental and synchronous generation are identical");
        Call(city, "UnloadChunk", coordinate);

        manager.player.position = city.Origin + new Vector3(512, 2, 512);
        Tick(city);
        var abandoned = (Vector2Int)Field(city, "activeCoordinate");
        Check(Field(city, "activeBuild") != null, "small budget leaves construction pending across frames");
        Physics.SyncTransforms();
        Check(Physics.Raycast(manager.player.position, Vector3.down, 4f), "teleport has ground on its first update");
        manager.player.position = city.Origin + new Vector3(2048, 2, 2048);
        Tick(city);
        Check(city.WorldRoot.Cast<Transform>().Where(t => t.name == "Quartier " + abandoned.x + "," + abandoned.y).All(t => !t.gameObject.activeSelf),
            "teleport cancels obsolete partially built district");
        Check(RenderSettings.fogEndDistance <= 40.1f, "fog contracts while nearby geometry is unfinished");
        float maxUpdateMs = 0;
        city.enabled = true;
        for (int i = 0; i < 500; i++)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            Tick(city);
            maxUpdateMs = Mathf.Max(maxUpdateMs, (float)((Time.realtimeSinceStartupAsDouble - started) * 1000));
            yield return new WaitForSecondsRealtime(.01f);
        }
        city.enabled = false;
        Check(manager.shop != null && manager.shop.isActiveAndEnabled, "distant refill shop becomes available asynchronously");
        Check(city.LoadedChunkCount <= 75, "streaming stays bounded after teleport and retirement");
        Check(RenderSettings.fogEndDistance > 100f, "fog gradually opens as surrounding districts finish");
        manager.player.position = city.Origin + Vector3.up * 2;
        city.enabled = true;
        for (int i = 0; i < 400; i++) { Tick(city); yield return new WaitForSecondsRealtime(.01f); }
        city.enabled = false;
        Check(Fingerprint(city.WorldRoot.Cast<Transform>().First(t => t.name == "Quartier 0,0" && t.gameObject.activeSelf)) == original,
            "return trip restores the same deterministic neighbourhood");
        var radar = new RadarMapRenderer(Shader.Find("Hidden/Internal-Colored"));
        radar.RefreshGeometry(city.WorldRoot);
        int count = radar.GeometryCount;
        var origin = city.WorldRoot.Find("Quartier 0,0");
        radar.RemoveGeometry(origin);
        Check(radar.GeometryCount < count, "radar removes one district incrementally");
        radar.AddGeometry(origin);
        Check(radar.GeometryCount == count, "radar incrementally restores the district without duplicates");
        radar.AddGeometry(origin);
        Check(radar.GeometryCount == count, "repeated radar registration is idempotent");
        radar.Dispose();
        var camera = new GameObject("Streaming preview").AddComponent<Camera>();
        camera.transform.position = city.Origin + new Vector3(0, 3.5f, -10);
        camera.transform.LookAt(city.Origin + new Vector3(0, 2, 60));
        camera.fieldOfView = 65;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = city.streamingFogColor;
        camera.farClipPlane = 700;
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        var rt = new RenderTexture(1440, 900, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
        image.Apply();
        File.WriteAllBytes("streaming-fog.png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Destroy(rt);
        Destroy(image);
        File.WriteAllText("streaming-result.txt", "PASSED " + passed + " checks; maximum Update in isolated editor: " + maxUpdateMs.ToString("F2") + " ms");
        Debug.Log("STREAMING CHECKS PASSED: " + passed);
        EditorApplication.Exit(0);
    }
}
