using UnityEngine;

// Petits outils pour créer des marqueurs visibles sans préparer de matériau.
public static class JojoVisuals
{
    private static Material markerMaterial;
    private static bool reportedMissingMaterial;
    public static void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        var material = CreateUnlitMaterial(color, false);
        if (material != null) renderer.sharedMaterial = material;
    }

    // Faisceau lumineux vertical, visible au-dessus des bâtiments (sans collision)
    public static GameObject CreateBeacon(Transform parent, Color color, float height, float width)
    {
        GameObject beacon = new GameObject("Faisceau");
        beacon.SetActive(false);
        beacon.transform.SetParent(parent, false);

        // Coeur du faisceau : fin et très lumineux
        CreateBeamPart(beacon.transform, "Coeur", Color.Lerp(color, Color.white, 0.5f), height, width * 0.35f, 0.9f);

        // Halo : plus large et transparent, il pulse
        GameObject halo = CreateBeamPart(beacon.transform, "Halo", color, height, width, 0.35f);
        if (halo != null) halo.AddComponent<BeaconPulse>();

        // Lumière au sol
        GameObject lightObject = new GameObject("Lumiere");
        lightObject.transform.SetParent(beacon.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.5f;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 15f;
        light.intensity = 8f;

        beacon.SetActive(true);
        return beacon;
    }

    private static GameObject CreateBeamPart(Transform parent, string name, Color color, float height, float width, float alpha)
    {
        color.a = alpha;
        var material = CreateUnlitMaterial(color, true);
        if (material == null) return null;
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);

        part.transform.SetParent(parent, false);
        part.transform.localPosition = Vector3.up * (height * 0.5f);
        // Le cylindre de base fait 2 de haut
        part.transform.localScale = new Vector3(width, height * 0.5f, width);

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        renderer.sharedMaterial = material;

        return part;
    }

    private static Material CreateUnlitMaterial(Color color, bool additive)
    {
        // The Resources material references its shader, so player builds retain it.
        // No runtime-only Shader.Find or transparent keyword variant is needed.
        if (markerMaterial == null) markerMaterial = Resources.Load<Material>("Crousty/WorldMarker");
        if (markerMaterial == null || markerMaterial.shader == null || !markerMaterial.shader.isSupported)
        {
            if (!reportedMissingMaterial)
                Debug.LogError("Matériau de repère Crousty/WorldMarker absent ou non compatible. Les livraisons restent disponibles.");
            reportedMissingMaterial = true;
            return null;
        }
        Material material = new Material(markerMaterial);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.Zero);
        material.SetFloat("_ZWrite", additive ? 0f : 1f);
        material.SetOverrideTag("RenderType", additive ? "Transparent" : "Opaque");
        material.renderQueue = additive ? (int)UnityEngine.Rendering.RenderQueue.Transparent : (int)UnityEngine.Rendering.RenderQueue.Geometry;
        return material;
    }

    // Petits sprites générés pour le HUD
    private static Sprite circleSprite;
    private static Sprite arrowSprite;

    public static Sprite CircleSprite =>
        circleSprite != null ? circleSprite : circleSprite = CreateSprite(IsInCircle);

    public static Sprite ArrowSprite =>
        arrowSprite != null ? arrowSprite : arrowSprite = CreateSprite(IsInArrow);

    private static Sprite CreateSprite(System.Func<float, float, float> shape)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Coordonnées entre -1 et 1
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, shape(u, v)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static float IsInCircle(float u, float v)
    {
        float distance = Mathf.Sqrt(u * u + v * v);
        return Mathf.Clamp01((1f - distance) * 16f);
    }

    // Triangle qui pointe vers le haut
    private static float IsInArrow(float u, float v)
    {
        float halfWidth = (1f - v) * 0.5f;
        bool inside = v > -0.9f && v < 1f && Mathf.Abs(u) < halfWidth;
        return inside ? 1f : 0f;
    }
}
