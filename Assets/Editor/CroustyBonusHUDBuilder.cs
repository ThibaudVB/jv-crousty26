using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CroustyBonusHUDBuilder
{
    private const string SpriteFolder = "Assets/UI/Bonuses/";
    private static readonly string[] FileNames = { "BONUS_GPS.png", "BONUS_PET.png", "BONUS_RIZ.png", "BONUS_VOL.png" };
    private static readonly string[] CardNames = { "GPS", "PET", "RICE", "FLIGHT" };
    private static readonly string[] Fields = { "gps", "pet", "rice", "flight" };

    [MenuItem("Tools/Crousty/Placer les atouts dans le HUD")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Sprite[] sprites = new Sprite[FileNames.Length];
        for (int i = 0; i < FileNames.Length; i++) sprites[i] = ImportSprite(SpriteFolder + FileNames[i]);
        AssetDatabase.ImportAsset("Assets/UI/Bonuses/BonusGrayscale.shader");
        const string materialPath = "Assets/UI/Bonuses/BonusGrayscale.mat";
        Material grayMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (grayMaterial == null)
        {
            Shader shader = Shader.Find("UI/CroustyBonusGrayscale");
            if (shader == null) throw new System.InvalidOperationException("Shader gris introuvable.");
            grayMaterial = new Material(shader);
            AssetDatabase.CreateAsset(grayMaterial, materialPath);
        }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            AssetDatabase.GUIDToAssetPath("6a57f33945af343e29f1558f4a2c90f0"));
        foreach (string path in new[] { "Assets/SceneBase.unity", "Assets/Jojo/SceneJojo.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                BusGPS gps = FindInScene<BusGPS>(scene);
                BusPetController pet = FindInScene<BusPetController>(scene);
                BusFlightController flight = FindInScene<BusFlightController>(scene);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (CroustyHUD hud in root.GetComponentsInChildren<CroustyHUD>(true))
                        CreateCards(hud, sprites, font, grayMaterial, gps, pet, flight);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        Debug.Log("[Crousty] Atouts GPS, PET, RICE et FLIGHT placés dans SceneBase et SceneJojo.");
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private static Sprite ImportSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("Image absente : " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void CreateCards(CroustyHUD hud, Sprite[] sprites, TMP_FontAsset font, Material grayMaterial,
        BusGPS gps, BusPetController pet, BusFlightController flight)
    {
        Canvas canvas = hud.GetComponentInChildren<Canvas>(true);
        if (canvas == null) throw new System.InvalidOperationException("Canvas du HUD introuvable : " + hud.name);
        Transform existing = canvas.transform.Find("Atouts");
        if (existing != null)
        {
            var alreadyPlaced = existing.GetComponent<CroustyBonusHUD>();
            if (alreadyPlaced != null)
            {
                var props = new SerializedObject(alreadyPlaced);
                Set(props, "grayMaterial", grayMaterial);
                props.ApplyModifiedPropertiesWithoutUndo();
            }
            return; // Keep later edits made in the Inspector.
        }

        Transform obsolete = canvas.transform.Find("Message");
        if (obsolete != null) Object.DestroyImmediate(obsolete.gameObject);

        RectTransform row = Rect("Atouts", canvas.transform);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.anchoredPosition = new Vector2(0f, 6f);
        row.sizeDelta = new Vector2(1040f, 220f);
        row.localScale = new Vector3(0.6f, 0.6f, 1f);
        CroustyBonusHUD behaviour = row.gameObject.AddComponent<CroustyBonusHUD>();
        SerializedObject serialized = new SerializedObject(behaviour);
        Set(serialized, "grayMaterial", grayMaterial);
        Set(serialized, "gpsController", gps);
        Set(serialized, "petController", pet);
        Set(serialized, "flightController", flight);
        for (int i = 0; i < 4; i++)
        {
            RectTransform card = Rect(CardNames[i], row);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
            card.pivot = new Vector2(0.5f, 0f);
            card.anchoredPosition = new Vector2((i - 1.5f) * 260f, 0f);
            card.sizeDelta = new Vector2(235f, 213f);

            RectTransform imageRect = Rect("Artwork", card);
            Stretch(imageRect);
            Image image = imageRect.gameObject.AddComponent<Image>();
            image.sprite = sprites[i];
            image.preserveAspect = true;
            image.raycastTarget = false;

            var count = AddText(card, "Count", "x3", font, 27, new Vector2(0.16f, 0.39f), new Vector2(70f, 40f));
            var label = AddText(card, "Name", CardNames[i], font, 24, new Vector2(0.53f, 0.095f), new Vector2(184f, 42f));
            SerializedProperty entry = serialized.FindProperty(Fields[i]);
            entry.FindPropertyRelative("artwork").objectReferenceValue = image;
            entry.FindPropertyRelative("countText").objectReferenceValue = count;
            entry.FindPropertyRelative("nameText").objectReferenceValue = label;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(behaviour);
    }

    private static void Set(SerializedObject serialized, string field, Object value) =>
        serialized.FindProperty(field).objectReferenceValue = value;

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text AddText(RectTransform card, string name, string value, TMP_FontAsset font,
        int size, Vector2 anchor, Vector2 dimensions)
    {
        RectTransform rect = Rect(name, card);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = dimensions;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;
        text.outlineColor = new Color32(38, 38, 50, 255);
        text.outlineWidth = 0.2f;
        return text;
    }
}
