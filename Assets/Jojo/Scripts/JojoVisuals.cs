using UnityEngine;
using UnityEngine.Rendering;

// Boîte à outils partagée par les scripts Jojo (ce n'est pas un composant : rien à placer).
// - IsClose : test de distance "à plat"
// - CreateShape / CreateBeacon / SetColor : petits objets visuels créés par code
// - CircleSprite / ArrowSprite : images simples pour le HUD
public static class JojoVisuals
{
    // ---------- DISTANCE ----------

    // Vrai si "position" est à moins de "radius" mètres de "center" (sans compter la hauteur)
    // et que l'écart de hauteur ne dépasse pas "maxHeight".
    public static bool IsClose(Vector3 center, Vector3 position, float radius, float maxHeight)
    {
        if (Mathf.Abs(position.y - center.y) > maxHeight)
            return false;

        center.y = 0f;
        position.y = 0f;
        return Vector3.Distance(center, position) <= radius;
    }

    // ---------- OBJETS 3D ----------

    // Le matériau de base est rangé dans Resources/Crousty/WorldMarker
    private static Material markerMaterial;
    private static bool missingMaterialReported;

    // Change la couleur d'un objet (avec un matériau qui ne réagit pas à la lumière)
    public static void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        Material material = CreateMaterial(color, false);
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
    }

    // Forme simple (cube, sphère...) sans collision ni ombre. Sert aux flammes, à la fumée, au riz.
    public static GameObject CreateShape(PrimitiveType type, Transform parent, Color color)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        RemoveCollider(shape);
        shape.transform.SetParent(parent, false);
        TurnOffShadows(shape);
        SetColor(shape, color);
        return shape;
    }

    // Faisceau lumineux vertical, visible de loin au-dessus des bâtiments
    public static GameObject CreateBeacon(Transform parent, Color color, float height, float width)
    {
        GameObject beacon = new GameObject("Faisceau");
        beacon.transform.SetParent(parent, false);

        // Coeur : fin et très lumineux
        CreateBeamPart(beacon.transform, "Coeur", Color.Lerp(color, Color.white, 0.5f), height, width * 0.35f, 0.9f);

        // Halo : plus large, transparent, et il pulse
        GameObject halo = CreateBeamPart(beacon.transform, "Halo", color, height, width, 0.35f);
        if (halo != null)
            halo.AddComponent<BeaconPulse>();

        // Lumière au sol
        GameObject lightObject = new GameObject("Lumiere");
        lightObject.transform.SetParent(beacon.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.5f;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = 15f;
        light.intensity = 8f;

        return beacon;
    }

    private static GameObject CreateBeamPart(Transform parent, string name, Color color, float height, float width, float alpha)
    {
        color.a = alpha;
        Material material = CreateMaterial(color, true);
        if (material == null)
            return null;

        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        RemoveCollider(part);
        part.transform.SetParent(parent, false);

        // Le cylindre de Unity fait 2 m de haut : on le pose sur le sol et on l'étire
        part.transform.localPosition = Vector3.up * (height * 0.5f);
        part.transform.localScale = new Vector3(width, height * 0.5f, width);

        TurnOffShadows(part);
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        collider.enabled = false; // désactivé tout de suite, Destroy n'agit qu'à la fin de la frame
        Object.Destroy(collider);
    }

    private static void TurnOffShadows(GameObject target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Copie le matériau de base avec une couleur.
    // transparent = vrai : la couleur s'ajoute à ce qu'il y a derrière (effet lumineux du faisceau).
    private static Material CreateMaterial(Color color, bool transparent)
    {
        if (markerMaterial == null)
            markerMaterial = Resources.Load<Material>("Crousty/WorldMarker");

        if (markerMaterial == null || markerMaterial.shader == null || !markerMaterial.shader.isSupported)
        {
            if (!missingMaterialReported)
                Debug.LogError("Matériau Resources/Crousty/WorldMarker absent ou incompatible : les faisceaux ne s'afficheront pas.");
            missingMaterialReported = true;
            return null;
        }

        Material material = new Material(markerMaterial);
        material.SetColor("_BaseColor", color);

        if (transparent)
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
        }
        return material;
    }

    // ---------- IMAGES POUR LE HUD ----------

    private static Sprite circleSprite;
    private static Sprite arrowSprite;

    // Rond blanc (créé une seule fois)
    public static Sprite CircleSprite
    {
        get
        {
            if (circleSprite == null)
                circleSprite = CreateSprite(true);
            return circleSprite;
        }
    }

    // Triangle blanc qui pointe vers le haut (créé une seule fois)
    public static Sprite ArrowSprite
    {
        get
        {
            if (arrowSprite == null)
                arrowSprite = CreateSprite(false);
            return arrowSprite;
        }
    }

    // Dessine l'image pixel par pixel dans une texture 64 x 64
    private static Sprite CreateSprite(bool circle)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Coordonnées du pixel entre -1 et 1 (0 = centre de l'image)
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float alpha = circle ? CircleAlpha(u, v) : ArrowAlpha(u, v);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 1 dans le cercle, 0 dehors, avec un bord adouci
    private static float CircleAlpha(float u, float v)
    {
        float distance = Mathf.Sqrt(u * u + v * v);
        return Mathf.Clamp01((1f - distance) * 16f);
    }

    // 1 dans le triangle, 0 dehors
    private static float ArrowAlpha(float u, float v)
    {
        float halfWidth = (1f - v) * 0.5f;
        bool inside = v > -0.9f && v < 1f && Mathf.Abs(u) < halfWidth;
        return inside ? 1f : 0f;
    }
}
