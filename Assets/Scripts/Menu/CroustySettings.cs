using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Persisted settings shared by the title screen and pause menu. Only a runtime
// clone of the render pipeline is changed; the authored quality assets stay intact.
public sealed class CroustySettings : MonoBehaviour
{
    public enum Option { Preset, Display, FrameRate, VSync, RenderScale, Shadows, AntiAliasing, PostEffects, ViewDistance, Traffic, Textures, Input }
    static readonly string[][] Labels = {
        new[] { "PERFORMANCE", "BALANCED", "HIGH", "CUSTOM" },
        new[] { "WINDOWED", "FULLSCREEN" },
        new[] { "30 FPS", "60 FPS", "90 FPS", "120 FPS", "UNLIMITED" },
        new[] { "OFF", "ON" }, new[] { "50%", "67%", "80%", "100%" },
        new[] { "OFF", "LOW", "MEDIUM", "HIGH" }, new[] { "OFF", "2x MSAA", "4x MSAA" },
        new[] { "OFF", "ON" }, new[] { "NEAR", "STANDARD", "FAR" },
        new[] { "LIGHT", "NORMAL", "BUSY" }, new[] { "LOW", "MEDIUM", "FULL" },
        new[] { "KEYBOARD / MOUSE", "CONTROLLER" }
    };
    static readonly int[] Defaults = { 1, 1, 1, 0, 3, 2, 1, 1, 1, 1, 2, 0 };
    static readonly int[] FrameRates = { 30, 60, 90, 120, -1 };
    static readonly float[] Scales = { .5f, .67f, .8f, 1f };
    static readonly float[] ShadowDistances = { 0f, 30f, 65f, 110f };
    static readonly int[] TrafficCounts = { 28, 48, 72 };
    static CroustySettings instance;
    UniversalRenderPipelineAsset originalGraphics, originalQuality, runtimePipeline;
    int originalVSync, originalFrameRate, originalTextureLimit;
    float originalLodBias;
    public static event Action Changed;
    public static int Get(Option option) => Mathf.Clamp(PlayerPrefs.GetInt("Crousty.Option." + option, Defaults[(int)option]), 0, Labels[(int)option].Length - 1);
    public static string Label(Option option) => Labels[(int)option][Get(option)];
    public static void Cycle(Option option)
    {
        int count = option == Option.Preset ? 3 : Labels[(int)option].Length;
        Set(option, (Get(option) + 1) % count);
    }
    static void Store(Option option, int value) => PlayerPrefs.SetInt("Crousty.Option." + option, value);
    public static void Set(Option option, int value)
    {
        value = Mathf.Clamp(value, 0, Labels[(int)option].Length - 1);
        Store(option, value);
        if (option == Option.Preset && value < 3)
        {
            Store(Option.RenderScale, value == 0 ? 1 : 3);
            Store(Option.Shadows, value == 0 ? 0 : value + 1);
            Store(Option.AntiAliasing, value);
            Store(Option.PostEffects, value == 0 ? 0 : 1);
            Store(Option.ViewDistance, value == 2 ? 2 : value);
            Store(Option.Traffic, value);
            Store(Option.Textures, value == 0 ? 1 : 2);
        }
        else if (option >= Option.RenderScale && option <= Option.Textures) Store(Option.Preset, 3);
        if (instance != null) instance.Apply(option == Option.Display);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { instance = null; Changed = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() => new GameObject("Crousty Settings").AddComponent<CroustySettings>();
    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        originalVSync = QualitySettings.vSyncCount;
        originalFrameRate = Application.targetFrameRate;
        originalTextureLimit = QualitySettings.globalTextureMipmapLimit;
        originalLodBias = QualitySettings.lodBias;
        originalGraphics = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        originalQuality = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        var source = originalQuality != null ? originalQuality : originalGraphics;
        if (source != null)
        {
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = "Crousty Runtime Quality";
            GraphicsSettings.defaultRenderPipeline = runtimePipeline;
            QualitySettings.renderPipeline = runtimePipeline;
        }
        SceneManager.sceneLoaded += SceneLoaded;
        Apply(true);
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode) => Apply(false);
    void Apply(bool display)
    {
        if (display && !Application.isEditor)
            Screen.fullScreenMode = Get(Option.Display) == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        QualitySettings.vSyncCount = Get(Option.VSync);
        Application.targetFrameRate = FrameRates[Get(Option.FrameRate)];
        QualitySettings.globalTextureMipmapLimit = 2 - Get(Option.Textures);
        QualitySettings.lodBias = new[] { .65f, 1f, 1.5f }[Get(Option.ViewDistance)];
        if (runtimePipeline != null)
        {
            runtimePipeline.renderScale = Scales[Get(Option.RenderScale)];
            runtimePipeline.shadowDistance = ShadowDistances[Get(Option.Shadows)];
            runtimePipeline.msaaSampleCount = 1 << Get(Option.AntiAliasing);
        }
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) data.renderPostProcessing = Get(Option.PostEffects) == 1;
        foreach (var city in FindObjectsByType<ProceduralCity>(FindObjectsSortMode.None)) ApplyCity(city);
    }
    public static void ApplyCity(ProceduralCity city)
    {
        city.viewDistance = Get(Option.ViewDistance) + 1;
        city.trafficCars = TrafficCounts[Get(Option.Traffic)];
    }
    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= SceneLoaded;
        if (GraphicsSettings.defaultRenderPipeline == runtimePipeline) GraphicsSettings.defaultRenderPipeline = originalGraphics;
        if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = originalQuality;
        if (runtimePipeline != null) Destroy(runtimePipeline);
        QualitySettings.vSyncCount = originalVSync;
        Application.targetFrameRate = originalFrameRate;
        QualitySettings.globalTextureMipmapLimit = originalTextureLimit;
        QualitySettings.lodBias = originalLodBias;
        instance = null;
    }
}
