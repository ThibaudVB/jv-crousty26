// Copy into Assets of an isolated full project to validate a real Windows player.
// The release project does not include this harness.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class BuildPlayerChecks : MonoBehaviour
{
    private int failures;
    private string output;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Environment.GetCommandLineArgs().Contains("--crousty-build-checks")) return;
        var root = new GameObject("Build player validation");
        DontDestroyOnLoad(root);
        root.AddComponent<BuildPlayerChecks>();
    }

    private IEnumerator Start()
    {
        output = Path.GetDirectoryName(Application.dataPath);
        Application.logMessageReceived += OnLog;
        yield return new WaitForSecondsRealtime(2);
        Check(SceneManager.GetActiveScene().name == "CroustyMainMenu", "menu is first scene");
        var menu = FindAnyObjectByType<CroustyMenu>();
        var pages = FindAnyObjectByType<CroustyMenuPages>();
        Check(menu != null && pages != null && pages.ticketImage.sprite != null, "menu and ticket included");
        foreach (var path in new[] { "Crousty/ProceduralSurface", "Crousty/ProceduralSignal", "Crousty/Atmosphere", "Crousty/CityGlow", "Crousty/WorldMarker" })
        {
            var material = Resources.Load<Material>(path);
            Check(material != null && material.shader != null && material.shader.isSupported, "shader supported: " + path);
        }
        var sounds = Resources.Load<CroustySoundBank>("Crousty/SoundBank");
        Check(sounds != null && sounds.cues != null && sounds.cues.All(c => c.clip != null), "sound clips included");
        Check(Resources.Load<GameObject>("Crousty/StopSign") != null, "stop model included");
        if (menu == null) { Finish(); yield break; }
        menu.Play();
        yield return new WaitForSecondsRealtime(8);
        Check(SceneManager.GetActiveScene().name == "SceneBase", "Start Game opens BASE");
        var city = FindAnyObjectByType<ProceduralCity>();
        Check(city != null && city.enabled && city.LoadedChunkCount >= 9 && city.BuildingVariantCount > 0, "city generated in player");
        Check(RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear && RenderSettings.fogEndDistance > RenderSettings.fogStartDistance, "runtime linear fog enabled");
        Check(FindAnyObjectByType<CroustyDayNightCycle>() != null, "day/night cycle included");
        Check(FindAnyObjectByType<BusMinimap>()?.radarShader != null, "minimap shader included");
        var smoke = FindAnyObjectByType<SceneBaseDriftSmoke>();
        Check(smoke != null && smoke.smokeMaterial != null && smoke.smokeMaterial.shader.isSupported &&
            smoke.GetComponentsInChildren<ParticleSystem>().Length >= 4, "wheel smoke initialized");
        if (smoke != null) smoke.EmitLandingImpact(smoke.transform.position, Vector3.up, 1f);
        yield return new WaitForSecondsRealtime(.2f);
        Check(smoke != null && smoke.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 0, "smoke emits in player");
        Time.timeScale = 0;
        Capture("build-fog-smoke.png");
        yield return new WaitForSecondsRealtime(1);
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        Capture("build-without-fog.png");
        yield return new WaitForSecondsRealtime(1);
        RenderSettings.fog = fog;
        Finish();
    }

    private void Check(bool success, string description)
    {
        if (!success) failures++;
        Debug.Log((success ? "BUILD_PASS " : "BUILD_FAIL ") + description);
    }

    private void Capture(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24);
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
        RenderTexture.active = previous;
        target.Release();
        Destroy(target);
        Destroy(image);
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failures++;
    }

    private void Finish()
    {
        Debug.Log("BUILD_CHECKS_DONE failures=" + failures);
        Application.logMessageReceived -= OnLog;
        Application.Quit(failures == 0 ? 0 : 1);
    }
}
