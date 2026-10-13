using UnityEngine;

[DisallowMultipleComponent]
public sealed class CroustyNightFixture : MonoBehaviour
{
    public Color lightColor = new Color(1f,.74f,.38f);
    public float range = 15f;
    public float intensity = 3f;
    public bool Powered { get; private set; } = true;
    public Vector3 LightPosition => transform.TransformPoint(localLightPosition);
    private Vector3 localLightPosition;
    private Renderer glowRenderer;
    private CroustyDayNightCycle owner;

    public void Configure(Renderer renderer, bool streetLamp)
    {
        glowRenderer = renderer;
        Bounds bounds = renderer.bounds;
        Vector3 position = streetLamp ? new Vector3(bounds.center.x, bounds.max.y - .35f, bounds.center.z) :
            new Vector3(bounds.center.x, bounds.min.y + 2.5f, bounds.max.z + .5f);
        localLightPosition = transform.InverseTransformPoint(position);
        if (!streetLamp) { range = 10f; intensity = 1.2f; }
        Register();
    }

    private void Register()
    {
        owner = CroustyDayNightCycle.Instance;
        if (owner != null && glowRenderer != null) owner.Register(this);
    }
    private void OnEnable() => Register();
    private void OnDisable() { if (owner != null) owner.Unregister(this); }
    public void CutPower()
    {
        Powered = false;
        if (glowRenderer == null) return;
        var properties = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_Power", 0);
        glowRenderer.SetPropertyBlock(properties);
    }
}
