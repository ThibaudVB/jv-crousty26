using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class BusAtmosphereChecks : MonoBehaviour
{
    public BusNightLights bus;
    int passed;
    void Check(bool value, string message)
    {
        if (!value) { File.WriteAllText("atmosphere-result.txt", "FAILED " + message); Debug.LogError(message); EditorApplication.Exit(1); throw new Exception(message); }
        passed++;
        Debug.Log("ATMOSPHERE PASS: " + message);
    }
    void RefreshBus() => typeof(BusNightLights).GetMethod("ApplyLighting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bus, null);
    IEnumerator Start()
    {
        yield return null;
        var city = FindAnyObjectByType<ProceduralCity>();
        city.enabled = false;
        var cycle = CroustyDayNightCycle.Instance;
        cycle.enabled = false;
        // This isolated harness renders with the built-in pipeline. The presence
        // of the URP package makes Shader.Find return URP/Lit for generated ground;
        // adapt only those temporary materials so the preview shows actual beams.
        foreach (var renderer in FindObjectsByType<Renderer>())
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || !material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                Color color = material.color;
                material.shader = Shader.Find("Standard");
                material.color = color;
            }
        var beams = bus.GetComponentsInChildren<Light>().Where(l => l.name == "Crousty dipped headlight").ToArray();
        Check(beams.Length == 2, "bus has exactly two headlights");
        Check(beams.All(l => l.type == LightType.Spot && l.shadows == UnityEngine.LightShadows.None && l.range == 55), "headlights are bounded shadowless spotlights");
        Check(beams.All(l => l.transform.parent.name.Contains("Phare")), "lights attach to the real left and right headlamp meshes");
        Check(beams.All(l => Vector3.Dot(l.transform.forward, bus.transform.forward) > .95f), "both headlamps face the road ahead");
        cycle.SetHour(12);
        RefreshBus();
        Check(beams.All(l => !l.enabled && l.intensity == 0), "headlights switch off in daylight");
        cycle.SetHour(22);
        RefreshBus();
        Check(beams.All(l => l.enabled && l.intensity > 5), "headlights switch on automatically in darkness");
        Check(bus.GetComponentsInChildren<Renderer>().Count(r => r.name == "Crousty running light") == 2, "rear red lights are present without extra Light components");
        var parent = beams[0].transform.parent;
        Quaternion before = beams[0].transform.rotation;
        Quaternion oldRotation = parent.localRotation;
        parent.localRotation *= Quaternion.Euler(0,10,3);
        Check(Quaternion.Angle(before, beams[0].transform.rotation) > 5, "lights follow body animation and steering");
        parent.localRotation = oldRotation;
        Check(RenderSettings.skybox != null && RenderSettings.skybox.shader.name == "Skybox/CroustyAtmosphere", "procedural atmosphere is used by the cycle");
        Check(Mathf.Abs(RenderSettings.skybox.GetFloat("_CloudCoverage") - .24f) < .001f, "cloud coverage is intentionally sparse");
        var camera = new GameObject("Atmosphere preview").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 65;
        camera.farClipPlane = 900;
        var rt = new RenderTexture(1600,900,24);
        camera.targetTexture = rt;
        foreach (float hour in new[] { 12f,22f })
        {
            cycle.SetHour(hour);
            RefreshBus();
            camera.transform.position = city.Origin + new Vector3(0,3.5f,-14);
            camera.transform.LookAt(city.Origin + new Vector3(0,10,60));
            Capture(camera,rt,"atmosphere-" + cycle.Phase + ".png");
            camera.transform.position = bus.transform.TransformPoint(new Vector3(7,4,12));
            camera.transform.LookAt(bus.transform.TransformPoint(new Vector3(0,2,0)));
            Capture(camera,rt,"bus-lights-" + cycle.Phase + ".png");
            camera.transform.rotation = Quaternion.Euler(-30,0,0);
            Capture(camera,rt,"sky-" + cycle.Phase + ".png");
        }
        File.WriteAllText("atmosphere-result.txt","PASSED " + passed + " checks");
        EditorApplication.Exit(0);
    }
    void Capture(Camera camera, RenderTexture rt, string name)
    {
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
        image.Apply();
        File.WriteAllBytes(name,image.EncodeToPNG());
        RenderTexture.active = null;
        Destroy(image);
    }
}
