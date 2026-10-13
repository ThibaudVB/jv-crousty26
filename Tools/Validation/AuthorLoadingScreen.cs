using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class AuthorLoadingScreen
{
    public const string PrefabPath = "Assets/Menu/Loading/Chargement.prefab";
    public static void Run()
    {
        try
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new Exception("Le panel existe déjà : préserver les retouches manuelles.");
            Import("Assets/Menu/Loading/Screen 1.png", false);
            Import("Assets/Menu/Loading/loading bar base.png", true);
            var artwork = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Menu/Loading/Screen 1.png");
            var ribbon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/Loading/loading bar base.png");
            var root = new GameObject("Chargement - Panel UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            var loader = root.AddComponent<CroustyLoadingScreen>(); loader.panel = root.GetComponent<CanvasGroup>();
            var backdrop = Rect(root.transform, "Fond opaque", Vector2.zero, Vector2.one);
            backdrop.gameObject.AddComponent<Image>().color = new Color32(171,151,149,255);
            // UV cropping uses the supplied image directly, without changing its pixels.
            // The baked progress strip is replaced by editable UI Images and TMP labels.
            var picture = Rect(root.transform, "Illustration fournie", new Vector2(0,183f/1080), Vector2.one);
            var raw = picture.gameObject.AddComponent<RawImage>(); raw.texture = artwork; raw.uvRect = new Rect(0,183f/1080,1,897f/1080); raw.raycastTarget = false;
            var bottom = Rect(root.transform, "Bas de illustration", Vector2.zero, new Vector2(1,43f/1080));
            raw = bottom.gameObject.AddComponent<RawImage>(); raw.texture = artwork; raw.uvRect = new Rect(0,0,1,43f/1080); raw.raycastTarget = false;
            var bar = Rect(root.transform, "Barre de chargement", new Vector2(0,43f/1080), new Vector2(1,183f/1080));
            var baseImage = bar.gameObject.AddComponent<Image>(); baseImage.sprite = ribbon; baseImage.raycastTarget = false;
            var fill = Rect(bar, "Remplissage orange", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            fill.sprite = ribbon; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 0;
            fill.color = new Color(233f/239, 132f/233, 36f/225); fill.raycastTarget = false; loader.fill = fill;
            var text = Label(bar, "Étape animée", "LOADING...", new Vector2(.014f,.12f), new Vector2(.87f,.88f), 66);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.rectTransform.pivot = new Vector2(0,.5f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 66; loader.status = text;
            var ink = new Material(text.fontSharedMaterial);
            ink.EnableKeyword("UNDERLAY_ON");
            ink.SetColor("_UnderlayColor",new Color32(123,67,13,255));
            ink.SetFloat("_UnderlayOffsetX",.7f); ink.SetFloat("_UnderlayOffsetY",-.8f);
            ink.SetFloat("_UnderlayDilate",.15f);
            AssetDatabase.CreateAsset(ink,"Assets/Menu/Loading/Texte ombre.mat");
            text.fontSharedMaterial=ink;
            loader.percentage = Label(bar, "Pourcentage animé", "0%", new Vector2(.89f,.2f), new Vector2(.97f,.8f), 42);
            loader.percentage.color = new Color32(38,38,50,255);
            var error = Rect(root.transform, "Retour en cas d'erreur", new Vector2(.37f,.25f),new Vector2(.63f,.34f));
            var button = error.gameObject.AddComponent<Button>(); button.targetGraphic = error.gameObject.AddComponent<Image>(); button.targetGraphic.color = new Color32(233,132,36,255);
            Label(error, "Texte retour", "RETOUR AU MENU", Vector2.zero, Vector2.one, 38);
            UnityEventTools.AddPersistentListener(button.onClick,loader.ReturnToMenu); loader.errorReturn = error.gameObject; error.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("AUTHOR_LOADING_DONE"); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Import(string path, bool sprite)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.spriteImportMode = SpriteImportMode.Single; importer.maxTextureSize = sprite ? 4096 : 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true; importer.SaveAndReimport();
    }
    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; return rect;
    }
    static TMP_Text Label(Transform parent, string name, string value, Vector2 min, Vector2 max, float size)
    {
        var text = Rect(parent,name,min,max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Menu/RetroBlendy SDF.asset");
        text.text=value; text.fontSize=size; text.alignment=TextAlignmentOptions.Center; text.color=new Color32(239,233,225,255); text.raycastTarget=false;
        return text;
    }
}
