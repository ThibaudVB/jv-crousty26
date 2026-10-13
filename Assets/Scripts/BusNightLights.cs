using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BusNightLights : MonoBehaviour
{
    [Min(5f)] public float headlightRange = 55f;
    [Min(0f)] public float headlightIntensity = 6f;
    [Range(20f, 100f)] public float beamAngle = 65f;
    private readonly List<Light> headlights = new List<Light>(2);
    private readonly List<GameObject> additions = new List<GameObject>();
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Lens> lenses = new List<Lens>();
    private MaterialPropertyBlock properties;
    private struct Lens { public Renderer renderer; public Material[] original; }

    private void Awake() => properties = new MaterialPropertyBlock();

    private void Start()
    {
        var template = Resources.Load<Material>("Crousty/CityGlow");
        Bounds bounds = VisualBounds();
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.name.IndexOf("Phare", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (headlights.Count >= 2) break;
            Vector3 position = renderer.bounds.center + transform.forward * .06f;
            CreateBeam(renderer.transform, position);
            if (template != null) PrepareLens(renderer, template, new Color(2.2f,1.95f,1.4f));
        }
        var pivot = transform.Find("Carrosserie - Animation arcade") ?? transform;
        if (headlights.Count == 0)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = transform.TransformPoint(new Vector3(bounds.center.x + side * bounds.extents.x * .72f,
                    bounds.min.y + bounds.size.y * .23f, bounds.max.z + .06f));
                CreateBeam(pivot, position);
                if (template != null) CreateLens(pivot, position, transform.forward, template, new Color(2.2f,1.95f,1.4f));
            }
        }
        if (template != null)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = transform.TransformPoint(new Vector3(bounds.center.x + side * bounds.extents.x * .72f,
                    bounds.min.y + bounds.size.y * .23f, bounds.min.z - .06f));
                CreateLens(pivot, position, -transform.forward, template, new Color(1.8f,.06f,.025f));
            }
        ApplyLighting();
    }

    private Bounds VisualBounds()
    {
        Bounds result = new Bounds(new Vector3(0,2,-.6f), new Vector3(3.2f,4,5.6f));
        bool found = false;
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found) { result = new Bounds(corner, Vector3.zero); found = true; }
                else result.Encapsulate(corner);
            }
        }
        return result;
    }

    private void CreateBeam(Transform parent, Vector3 position)
    {
        var light = new GameObject("Crousty dipped headlight").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = position;
        light.transform.rotation = Quaternion.LookRotation(transform.forward - transform.up * .1f, transform.up);
        light.type = LightType.Spot;
        light.range = headlightRange;
        light.spotAngle = beamAngle;
        light.innerSpotAngle = beamAngle * .65f;
        light.color = new Color(1,.92f,.77f);
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        light.enabled = false;
        headlights.Add(light);
        additions.Add(light.gameObject);
    }

    private void PrepareLens(Renderer renderer, Material template, Color color)
    {
        Material[] original = renderer.sharedMaterials;
        var glowing = new Material[original.Length];
        for (int i = 0; i < glowing.Length; i++)
        {
            glowing[i] = new Material(template) { name = "Crousty bus lamp" };
            glowing[i].SetColor("_BaseColor", original[i] != null ? original[i].color : Color.white);
            if (color.g < .1f) glowing[i].SetColor("_BaseColor", new Color(.28f,.02f,.012f));
            glowing[i].SetColor("_GlowColor", color);
            materials.Add(glowing[i]);
        }
        lenses.Add(new Lens { renderer = renderer, original = original });
        renderer.sharedMaterials = glowing;
    }

    private void CreateLens(Transform parent, Vector3 position, Vector3 forward, Material template, Color color)
    {
        var lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
        lens.name = "Crousty running light";
        lens.transform.SetParent(parent, false);
        lens.transform.position = position;
        // The built-in quad's visible normal points toward local -Z.
        lens.transform.rotation = Quaternion.LookRotation(-forward, transform.up);
        lens.transform.localScale = new Vector3(.18f,.13f,1);
        var collider = lens.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = lens.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        PrepareLens(renderer, template, color);
        additions.Add(lens);
    }

    private void Update() => ApplyLighting();
    private void ApplyLighting()
    {
        var cycle = CroustyDayNightCycle.Instance;
        float night = cycle != null ? cycle.NightAmount : 0;
        float power = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.8f,night));
        foreach (var light in headlights)
        {
            light.enabled = power > .005f;
            light.intensity = headlightIntensity * power;
        }
        foreach (var lens in lenses)
        {
            if (lens.renderer == null) continue;
            lens.renderer.GetPropertyBlock(properties);
            properties.SetFloat("_Power", power);
            lens.renderer.SetPropertyBlock(properties);
        }
    }

    private void OnDisable() { foreach (var light in headlights) if (light != null) light.enabled = false; }
    private void OnDestroy()
    {
        foreach (var lens in lenses) if (lens.renderer != null) lens.renderer.sharedMaterials = lens.original;
        foreach (var addition in additions) if (addition != null) Destroy(addition);
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
