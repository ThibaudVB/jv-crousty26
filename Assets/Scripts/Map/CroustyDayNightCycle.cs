using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-1100), DisallowMultipleComponent]
public sealed class CroustyDayNightCycle : MonoBehaviour
{
    public enum DayPhase { Morning, Day, Sunset, Night }
    public static CroustyDayNightCycle Instance { get; private set; }
    [Min(10f)] public float cycleDurationSeconds = 180f;
    [Range(0f, 24f)] public float startingHour = 6f;
    [Range(0, 24)] public int maximumLocalLights = 12;
    public Transform player;
    public Light sun;
    public float Hour { get; private set; }
    public float NightAmount { get; private set; }
    public DayPhase Phase => PhaseAt(Hour);
    private double elapsed;
    private float nextLightSelection;
    private readonly List<CroustyNightFixture> fixtures = new List<CroustyNightFixture>();
    private readonly Dictionary<Material, Material> luminousMaterials = new Dictionary<Material, Material>();
    private Light[] localLights;
    private CroustyNightFixture[] selected;
    private float[] distances;
    private Material luminousTemplate, originalSkybox, runtimeSkybox;
    private Light originalSun;
    private Color originalSunColor, originalAmbientSky, originalAmbientEquator, originalAmbientGround, originalFogColor;
    private float originalSunIntensity, originalReflection;
    private Quaternion originalSunRotation;
    private AmbientMode originalAmbientMode;
    private bool createdSun;
    private static readonly float[] Hours = { 0, 5, 6, 9, 12, 16, 18, 20, 24 };
    private static readonly Color[] Sky = {
        new Color(.025f,.04f,.09f), new Color(.055f,.065f,.12f), new Color(.57f,.39f,.34f),
        new Color(.63f,.77f,.92f), new Color(.69f,.79f,.9f), new Color(.64f,.76f,.9f),
        new Color(.72f,.36f,.26f), new Color(.055f,.065f,.14f), new Color(.025f,.04f,.09f)
    };
    private static readonly Color[] SunColors = {
        new Color(.45f,.57f,.9f), new Color(.45f,.57f,.9f), new Color(1,.55f,.3f),
        new Color(1,.88f,.7f), new Color(1,.96f,.86f), new Color(1,.9f,.73f),
        new Color(1,.4f,.16f), new Color(.45f,.57f,.9f), new Color(.45f,.57f,.9f)
    };
    private static readonly float[] Intensities = { .15f,.12f,.55f,1.15f,1.4f,1.1f,.65f,.12f,.15f };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSingleton() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        originalSun = RenderSettings.sun;
        if (sun == null) sun = originalSun;
        if (sun == null)
            foreach (var light in FindObjectsByType<Light>())
                if (light.type == LightType.Directional && light.isActiveAndEnabled) { sun = light; break; }
        if (sun == null)
        {
            sun = new GameObject("Crousty sun and moon").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            createdSun = true;
        }
        originalSunColor = sun.color;
        originalSunIntensity = sun.intensity;
        originalSunRotation = sun.transform.rotation;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSky = RenderSettings.ambientSkyColor;
        originalAmbientEquator = RenderSettings.ambientEquatorColor;
        originalAmbientGround = RenderSettings.ambientGroundColor;
        originalReflection = RenderSettings.reflectionIntensity;
        originalFogColor = RenderSettings.fogColor;
        originalSkybox = RenderSettings.skybox;
        var atmosphere = Resources.Load<Material>("Crousty/Atmosphere");
        if (atmosphere != null || originalSkybox != null)
        {
            runtimeSkybox = new Material(atmosphere != null ? atmosphere : originalSkybox);
            RenderSettings.skybox = runtimeSkybox;
        }
        RenderSettings.sun = sun;
        luminousTemplate = Resources.Load<Material>("Crousty/CityGlow");
        int count = Mathf.Clamp(maximumLocalLights, 0, 24);
        localLights = new Light[count];
        selected = new CroustyNightFixture[count];
        distances = new float[count];
        for (int i = 0; i < count; i++)
        {
            var light = new GameObject("Nearby city light " + i).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = false;
            localLights[i] = light;
        }
        SetHour(startingHour);
    }

    private void Update()
    {
        AdvanceTime(Time.deltaTime);
        if (Time.timeScale <= 0) return;
        if (Time.time >= nextLightSelection)
        {
            nextLightSelection = Time.time + .2f;
            SelectNearbyLights();
        }
        // Follow a falling fixture even between selection passes.
        for (int i = 0; i < localLights.Length; i++)
        {
            var fixture = selected[i];
            bool powered = fixture != null && fixture.isActiveAndEnabled && fixture.Powered && NightAmount > .01f;
            localLights[i].enabled = powered;
            if (!powered) continue;
            localLights[i].transform.position = fixture.LightPosition;
            localLights[i].color = fixture.lightColor;
            localLights[i].range = fixture.range;
            localLights[i].intensity = fixture.intensity * NightAmount;
        }
    }

    private void AdvanceTime(float seconds)
    {
        elapsed += Mathf.Max(0, seconds);
        float duration = Mathf.Max(10f, cycleDurationSeconds);
        ApplyHour(Mathf.Repeat(startingHour + (float)(elapsed % duration) * 24f / duration, 24f));
    }

    public static DayPhase PhaseAt(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        return hour < 6 || hour >= 20 ? DayPhase.Night : hour < 10 ? DayPhase.Morning : hour < 17 ? DayPhase.Day : DayPhase.Sunset;
    }

    public void SetHour(float hour)
    {
        startingHour = Mathf.Repeat(hour, 24f);
        elapsed = 0;
        ApplyHour(startingHour);
        nextLightSelection = 0;
    }

    private void ApplyHour(float hour)
    {
        Hour = hour;
        int k = 0;
        while (k < Hours.Length - 2 && hour >= Hours[k + 1]) k++;
        float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Hours[k], Hours[k + 1], hour));
        Color sky = Color.Lerp(Sky[k], Sky[k + 1], t);
        float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5.5f, 8f, hour)) *
            (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17f, 20f, hour)));
        NightAmount = 1f - daylight;
        // Keep neutral ambient lighting on the city while giving the visible
        // daytime sky a vivid cyan blue. Dawn, sunset and night retain their hues.
        Color visibleSky = Color.Lerp(sky, new Color(.035f, .64f, .94f), daylight);
        Color visibleHorizon = Color.Lerp(sky, new Color(.055f, .72f, .96f), daylight);
        sun.color = Color.Lerp(SunColors[k], SunColors[k + 1], t);
        sun.intensity = Mathf.Lerp(Intensities[k], Intensities[k + 1], t);
        float elevation = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f);
        // At night the same directional light becomes a cool, subdued moon.
        sun.transform.rotation = Quaternion.Euler(Mathf.Max(5f, Mathf.Abs(elevation) * 75f), hour * 15f - 90f, 0);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(new Color(.11f,.14f,.24f), sky, daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.065f,.08f,.14f), sky * .7f, daylight);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(.035f,.04f,.07f), new Color(.28f,.26f,.24f), daylight);
        RenderSettings.reflectionIntensity = Mathf.Lerp(.2f, .8f, daylight);
        RenderSettings.fogColor = visibleHorizon;
        Shader.SetGlobalFloat("_CroustyCityNight", NightAmount);
        Shader.SetGlobalFloat("_CroustyCityDaylight", Mathf.Lerp(.25f, 1f, daylight));
        Shader.SetGlobalFloat("_CroustySkyTime", (float)(elapsed % 10000));
        if (runtimeSkybox != null)
        {
            if (runtimeSkybox.HasProperty("_SkyColor")) runtimeSkybox.SetColor("_SkyColor", visibleSky);
            if (runtimeSkybox.HasProperty("_HorizonColor")) runtimeSkybox.SetColor("_HorizonColor", visibleHorizon);
            if (runtimeSkybox.HasProperty("_SunColor")) runtimeSkybox.SetColor("_SunColor", sun.color);
            if (runtimeSkybox.HasProperty("_SunDirection")) runtimeSkybox.SetVector("_SunDirection", -sun.transform.forward);
            if (runtimeSkybox.HasProperty("_SkyTint")) runtimeSkybox.SetColor("_SkyTint", visibleSky);
            if (runtimeSkybox.HasProperty("_Tint")) runtimeSkybox.SetColor("_Tint", visibleSky);
            if (runtimeSkybox.HasProperty("_GroundColor")) runtimeSkybox.SetColor("_GroundColor", RenderSettings.ambientGroundColor);
            if (runtimeSkybox.HasProperty("_Exposure")) runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(.25f, 1.15f, daylight));
        }
    }

    public Material[] PrepareLuminousMaterials(Material[] source)
    {
        Material[] result = null;
        for (int i = 0; i < source.Length; i++)
        {
            var original = source[i];
            if (original == null || luminousTemplate == null) continue;
            string name = original.name;
            if (!name.Contains("Window") && !name.Contains("Glass") && !name.Contains("Lamp_Glow")) continue;
            if (result == null) result = (Material[])source.Clone();
            if (!luminousMaterials.TryGetValue(original, out var material))
            {
                material = new Material(luminousTemplate) { name = name + " - cycle", enableInstancing = true };
                material.SetColor("_BaseColor", original.color);
                material.SetColor("_GlowColor", name.Contains("Lamp_Glow") ? new Color(2.4f,1.7f,.75f) : new Color(1.2f,.72f,.27f));
                luminousMaterials.Add(original, material);
            }
            result[i] = material;
        }
        return result ?? source;
    }

    internal void Register(CroustyNightFixture fixture) { if (!fixtures.Contains(fixture)) fixtures.Add(fixture); }
    internal void Unregister(CroustyNightFixture fixture) => fixtures.Remove(fixture);

    private void SelectNearbyLights()
    {
        for (int i = 0; i < selected.Length; i++) { selected[i] = null; distances[i] = float.PositiveInfinity; }
        if (player == null || NightAmount <= .01f) return;
        foreach (var fixture in fixtures)
        {
            if (fixture == null || !fixture.Powered || !fixture.isActiveAndEnabled) continue;
            float distance = (fixture.LightPosition - player.position).sqrMagnitude;
            if (distance > 70f * 70f) continue;
            for (int i = 0; i < selected.Length; i++)
            {
                if (distance >= distances[i]) continue;
                for (int j = selected.Length - 1; j > i; j--) { selected[j] = selected[j - 1]; distances[j] = distances[j - 1]; }
                selected[i] = fixture;
                distances[i] = distance;
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        if (sun != null && !createdSun) { sun.color = originalSunColor; sun.intensity = originalSunIntensity; sun.transform.rotation = originalSunRotation; }
        RenderSettings.sun = originalSun;
        RenderSettings.skybox = originalSkybox;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalAmbientSky;
        RenderSettings.ambientEquatorColor = originalAmbientEquator;
        RenderSettings.ambientGroundColor = originalAmbientGround;
        RenderSettings.reflectionIntensity = originalReflection;
        RenderSettings.fogColor = originalFogColor;
        Shader.SetGlobalFloat("_CroustyCityNight", 0);
        Shader.SetGlobalFloat("_CroustyCityDaylight", 1);
        Shader.SetGlobalFloat("_CroustySkyTime", 0);
        if (localLights != null)
            foreach (var light in localLights) if (light != null) Destroy(light.gameObject);
        if (createdSun && sun != null) Destroy(sun.gameObject);
        if (runtimeSkybox != null) Destroy(runtimeSkybox);
        foreach (var material in luminousMaterials.Values) Destroy(material);
    }
}
