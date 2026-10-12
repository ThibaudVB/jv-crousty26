using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayNightChecks : MonoBehaviour
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int passed;
    public Canvas previewCanvas;
    public CroustyHUD previewHud;
    void Check(bool value, string message)
    {
        if (!value) { File.WriteAllText("night-result.txt", "FAILED " + message); Debug.LogError(message); EditorApplication.Exit(1); throw new Exception(message); }
        passed++;
        Debug.Log("NIGHT PASS: " + message);
    }
    void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    IEnumerator Start()
    {
        yield return null;
        var city = FindAnyObjectByType<ProceduralCity>();
        city.enabled = false;
        var cycle = CroustyDayNightCycle.Instance;
        cycle.enabled = false;
        Check(cycle != null && cycle.cycleDurationSeconds == 180, "three-minute cycle starts with the procedural city");
        cycle.SetHour(6);
        Check(cycle.Phase == CroustyDayNightCycle.DayPhase.Morning, "6 AM is morning");
        Call(cycle, "AdvanceTime", 60f);
        Check(Mathf.Abs(cycle.Hour - 14) < .001f && cycle.Phase == CroustyDayNightCycle.DayPhase.Day, "60 real seconds advance eight game hours");
        Call(cycle, "AdvanceTime", 30f);
        Check(Mathf.Abs(cycle.Hour - 18) < .001f && cycle.Phase == CroustyDayNightCycle.DayPhase.Sunset, "sunset is reached after 90 seconds");
        Call(cycle, "AdvanceTime", 30f);
        Check(Mathf.Abs(cycle.Hour - 22) < .001f && cycle.Phase == CroustyDayNightCycle.DayPhase.Night, "night is reached after 120 seconds");
        Call(cycle, "AdvanceTime", 60f);
        Check(Mathf.Abs(cycle.Hour - 6) < .001f, "full day returns to its initial hour after exactly 180 seconds");
        cycle.enabled = true;
        Time.timeScale = 0;
        float pausedHour = cycle.Hour;
        yield return new WaitForSecondsRealtime(.05f);
        Check(Mathf.Abs(cycle.Hour - pausedHour) < .001f, "pause freezes the clock and cycle");
        Time.timeScale = 1;
        cycle.enabled = false;
        cycle.SetHour(12);
        Check(cycle.NightAmount < .001f && Shader.GetGlobalFloat("_CroustyCityNight") < .001f, "day turns off window and lamp glow");
        float daySun = cycle.sun.intensity;
        cycle.SetHour(22);
        Check(cycle.NightAmount > .99f && cycle.sun.intensity < daySun, "night dims the sun and turns on warm glow");
        var props = city.WorldRoot.GetComponentsInChildren<MapImpactProp>();
        Check(props.Length > 100, "procedural street furniture receives impact physics");
        var lamp = props.First(p => p.name.Contains("Lamp_"));
        var fixture = lamp.GetComponentInChildren<CroustyNightFixture>();
        Check(lamp.Body.isKinematic && fixture.Powered, "lamp starts upright and powered");
        Check(!lamp.TryHit(Vector3.zero), "standing against a lamp does not break it");
        // A real bus trigger, rather than a direct TryHit call, must activate the prop.
        var bus = new GameObject("Impact test bus");
        bus.transform.position = lamp.transform.position + Vector3.left * 3 + Vector3.up;
        var collider = bus.AddComponent<BoxCollider>();
        collider.size = new Vector3(2, 2, 2);
        var body = bus.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.mass = 2500;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        bus.AddComponent<BusPropImpact>();
        body.linearVelocity = Vector3.right * 12;
        yield return new WaitForSeconds(.65f);
        Check(lamp.HasBeenHit && !lamp.Body.isKinematic, "bus sensor activates a falling lamp without blocking the bus");
        Check(Vector3.Angle(lamp.transform.up, Vector3.up) > 25, "lamp physically falls at its base");
        Check(!fixture.Powered, "impact cuts lamp power");
        Check(!lamp.TryHit(Vector3.right * 12), "the same lamp cannot be broken twice");
        Destroy(bus);
        var sign = props.First(p => p.name == "Panneau STOP");
        Check(sign.TryHit(Vector3.forward * 8) && sign.GetComponent<HingeJoint>() != null, "STOP sign falls as a complete assembly");
        var bench = props.First(p => p.name.Contains("Bench_"));
        Check(bench.TryHit(Vector3.forward * 8) && bench.Body.linearVelocity.magnitude > 0, "small furniture can be knocked loose");
        Check(props.All(p => !p.GetComponentsInChildren<MeshCollider>().Any(c => c.enabled && !c.convex)), "dynamic props contain no illegal non-convex mesh colliders");
        cycle.player.position = city.Origin;
        Call(cycle, "Update");
        var lights = cycle.GetComponentsInChildren<Light>().Where(l => l.type == LightType.Point && l.name.StartsWith("Nearby city light")).ToArray();
        Check(lights.Length == 12 && lights.Count(l => l.enabled) > 0 && lights.Count(l => l.enabled) <= 12,
            "real nearby lights share a twelve-light shadowless pool");
        Check(lights.All(l => l.shadows == LightShadows.None), "local lighting adds no shadow-map rendering cost");
        var windows = city.WorldRoot.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
            .Where(m => m != null && m.name.Contains("Window")).ToArray();
        Check(windows.Length > 0 && windows.All(m => m.shader.name == "Crousty/CityGlow"), "existing window surfaces use the build-retained glow shader");
        var clock = previewCanvas.transform.Find("Time of day").GetComponentInChildren<TMP_Text>();
        cycle.SetHour(0);
        Call(previewHud, "UpdateClock");
        Check(clock.text.Contains("12:00 AM") && clock.text.Contains("NIGHT"), "midnight clock uses English AM/PM and phase");
        cycle.SetHour(12);
        Call(previewHud, "UpdateClock");
        Check(clock.text.Contains("12:00 PM") && clock.text.Contains("DAY"), "noon is formatted correctly");
        Check(previewCanvas.transform.Find("Compteur de vitesse") != null, "original HUD cartouche remains intact");
        var camera = new GameObject("Phase preview").AddComponent<Camera>();
        camera.transform.position = city.Origin + new Vector3(0, 3.5f, -10);
        camera.transform.LookAt(city.Origin + new Vector3(0, 2, 60));
        camera.fieldOfView = 65;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 700;
        previewCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        previewCanvas.worldCamera = camera;
        previewCanvas.planeDistance = 1;
        var rt = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = rt;
        foreach (float hour in new[] { 8f, 12f, 18f, 22f })
        {
            cycle.SetHour(hour);
            Call(cycle, "Update");
            Call(previewHud, "UpdateClock");
            camera.backgroundColor = RenderSettings.fogColor;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes("phase-" + cycle.Phase + ".png", image.EncodeToPNG());
            Destroy(image);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        Destroy(rt);
        File.WriteAllText("night-result.txt", "PASSED " + passed + " checks");
        EditorApplication.Exit(0);
    }
}
