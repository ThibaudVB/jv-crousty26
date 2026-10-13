using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

// Radar cartographique reconstruit a partir de la geometrie de la carte active.
[DefaultExecutionOrder(200)]
public sealed class BusMinimap : MonoBehaviour
{
    [Header("References (recherchees si absentes)")]
    public Transform bus;
    public DeliveryManager deliveries;
    public PoliceChaseManager police;
    public Shader radarShader;
    public Transform mapRoot;

    [Header("Affichage")]
    [Min(20f)] public float worldRadius = 100f;
    [Min(60f)] public float cameraHeight = 180f;
    [Range(35f, 90f)] public float cameraTilt = 55f;
    [Tooltip("Resolution minimale ; adaptee automatiquement a la taille du HUD en pixels.")]
    [Range(512, 2048)] public int textureSize = 1024;
    [Range(120f, 360f)] public float sizeOnScreen = 260f;
    [SerializeField] private bool compactHud;

    [SerializeField] private Image deliveryIconTemplate;
    private Camera mapCamera;
    private RenderTexture mapTexture;
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private Image busIcon;
    [SerializeField] private Image shopIcon;
    [SerializeField] private Image policeIcon;
    private readonly List<Image> deliveryIcons = new List<Image>();
    private readonly List<Image> policeIcons = new List<Image>();
    private readonly List<Image> garageIcons = new List<Image>();
    private readonly List<Vector3> nearbyGarages = new List<Vector3>();
    private ProceduralCity city;
    private float nextReferenceSearch;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private RawImage mapImage;
    private BusGPS gps;
    [SerializeField] private TextMeshProUGUI gpsLabel;
    [SerializeField] private MinimapRouteGraphic routeGraphic;
    [SerializeField] private RectTransform routeRoot;
    [SerializeField] private RectTransform northRect;
    private RadarMapRenderer radar;
    private float headingYaw;
    private float nextGeometrySearch;

    private void Awake()
    {
        gps = GetComponent<BusGPS>();
        FindReferences();
        if (mapCanvas == null || deliveryIconTemplate == null)
        {
            Debug.LogError("Assigner les panels de la mini-carte dans l’Inspector.", this);
            enabled = false;
            return;
        }
        BuildCamera();
        if (compactHud && maskRect != null)
            MinimapSpeedBadge.Ensure(maskRect.parent as RectTransform);

        if (radarShader != null) radar = new RadarMapRenderer(radarShader);
        RefreshMap(mapRoot);
    }

#if UNITY_EDITOR
    public void PrepareUI()
    {
        if (mapCanvas != null) return;
        circleSprite = CroustyUISpriteAssets.Save(CreateSmoothSprite(false, 512), "MinimapCercle");
        arrowSprite = CroustyUISpriteAssets.Save(CreateSmoothSprite(true, 128), "MinimapFleche");
        BuildHUD();
        deliveryIconTemplate = CreateIcon("Modèle livraison", circleSprite, new Color(0.3f, 1f, 0.3f), 16f);
        deliveryIconTemplate.gameObject.SetActive(false);
    }
#endif

    private void OnEnable()
    {
        if (mapCanvas != null) mapCanvas.enabled = true;
    }

    private void OnDisable()
    {
        if (mapCanvas != null) mapCanvas.enabled = false;
        if (mapCamera != null) mapCamera.enabled = false;
    }

    private void LateUpdate()
    {
        if ((bus == null || deliveries == null || police == null) &&
            Time.unscaledTime >= nextReferenceSearch)
        {
            FindReferences();
            nextReferenceSearch = Time.unscaledTime + 1f;
        }

        bool visible = bus != null && Time.timeScale > 0f;
        mapCanvas.enabled = visible;
        mapCamera.enabled = false; // Rendu cartographique explicite, sans seconde vue du monde.
        if (!visible) return;

        UpdateTextureResolution();
        if (mapRoot == null && Time.unscaledTime >= nextGeometrySearch)
        {
            RefreshMap(null);
            nextGeometrySearch = Time.unscaledTime + 2f;
        }
        Vector3 busPosition = bus.position;
        busPosition.y = radar != null ? radar.GroundHeight : 0f;
        Vector3 forward = Vector3.ProjectOnPlane(bus.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.001f)
            headingYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        mapCamera.transform.rotation = Quaternion.Euler(cameraTilt, headingYaw, 0f);
        float viewDistance = cameraHeight / Mathf.Sin(cameraTilt * Mathf.Deg2Rad);
        mapCamera.transform.position = busPosition - mapCamera.transform.forward * viewDistance;
        mapCamera.orthographicSize = worldRadius;
        mapCamera.farClipPlane = Mathf.Max(500f, cameraHeight / Mathf.Sin(cameraTilt * Mathf.Deg2Rad) + 300f);

        busIcon.rectTransform.localRotation = Quaternion.identity;
        Vector2 north = ProjectToMap(busPosition + Vector3.forward * 100f);
        float northExtent = Mathf.Max(Mathf.Abs(north.x), Mathf.Abs(north.y));
        northRect.anchoredPosition = northExtent > 0.001f
            ? north * ((Mathf.Min(maskRect.rect.width, maskRect.rect.height) * 0.5f - 12f) / northExtent) : Vector2.zero;
        radar?.Render(mapCamera, mapTexture);

        UpdateGPS();
        UpdateObjectives();
    }

    // A appeler apres generation / ajout de chunks. Aucun asset de carte precalcule requis.
    public void RefreshMap(Transform root)
    {
        if (root == null && gps != null) root = gps.navigationRoot;
        if (root == null)
            foreach (MeshFilter filter in FindObjectsByType<MeshFilter>())
                if (filter.name == "Ground_Asphalt") { root = filter.transform.root; break; }
        mapRoot = root;
        radar?.RefreshGeometry(mapRoot);
    }

    public void AddMapGeometry(Transform chunkRoot) => radar?.AddGeometry(chunkRoot);
    public void RemoveMapGeometry(Transform chunkRoot) => radar?.RemoveGeometry(chunkRoot);

    private void FindReferences()
    {
        if (deliveries == null) deliveries = FindAnyObjectByType<DeliveryManager>();
        if (bus == null && deliveries != null) bus = deliveries.player;
        if (bus == null)
        {
            BusController controller = FindAnyObjectByType<BusController>();
            if (controller != null) bus = controller.transform;
        }
        if (police == null) police = FindAnyObjectByType<PoliceChaseManager>();
        if (city == null) city = FindAnyObjectByType<ProceduralCity>();
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Camera Minimap");
        cameraObject.transform.SetParent(transform, false);
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.enabled = false;
        mapCamera.transform.rotation = Quaternion.Euler(cameraTilt, 0f, 0f);
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = worldRadius;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.1f, 0.15f, 0.17f);
        mapCamera.cullingMask = ~(1 << 5); // Pas les Canvas/UI.
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = Mathf.Max(500f, cameraHeight / Mathf.Sin(cameraTilt * Mathf.Deg2Rad) + 300f);
        mapCamera.allowHDR = false;
        mapCamera.allowMSAA = false;
        mapCamera.useOcclusionCulling = false;

        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;

        UpdateTextureResolution();
    }

    private void UpdateTextureResolution()
    {
        // Sur-echantillonnage 2x, y compris sur ecran haute densite / apres redimensionnement.
        float pixels = (maskRect != null ? Mathf.Max(maskRect.rect.width, maskRect.rect.height) : sizeOnScreen) * (mapCanvas != null ? mapCanvas.scaleFactor : 1f);
        int resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(
            Mathf.CeilToInt(Mathf.Max(textureSize, pixels * 2f))), 512, 2048);
        if (mapTexture != null && mapTexture.width == resolution) return;
        RenderTexture previous = mapTexture;
        mapTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
        mapTexture.name = "Minimap Crousty";
        mapTexture.filterMode = FilterMode.Trilinear;
        mapTexture.wrapMode = TextureWrapMode.Clamp;
        // La texture est reduite dans le HUD : filtrer les lignes routieres fines
        // evite leur scintillement en mouvement. Un biais modere preserve la nettete.
        mapTexture.useMipMap = true;
        // RadarMapRenderer generates the mip chain after drawing the map.
        mapTexture.autoGenerateMips = false;
        mapTexture.mipMapBias = -0.35f;
        mapTexture.Create();
        mapCamera.targetTexture = mapTexture;
        if (mapImage != null) mapImage.texture = mapTexture;
        if (previous != null)
        {
            previous.Release();
            Destroy(previous);
        }
    }

#if UNITY_EDITOR
    private void BuildHUD()
    {
        GameObject canvasObject = new GameObject("Minimap Crousty", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        mapCanvas = canvasObject.GetComponent<Canvas>();
        mapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mapCanvas.sortingOrder = 110;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform frame = CreateRect("Cadre", mapCanvas.transform);
        frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one;
        frame.anchoredPosition = new Vector2(-28f, -44f);
        frame.sizeDelta = Vector2.one * (sizeOnScreen + 10f);
        Image border = frame.gameObject.AddComponent<Image>();
        border.color = new Color(0.04f, 0.09f, 0.13f, 0.94f);
        border.raycastTarget = false;

        maskRect = CreateRect("Vue", frame);
        maskRect.anchorMin = maskRect.anchorMax = maskRect.pivot = new Vector2(0.5f, 0.5f);
        maskRect.anchoredPosition = Vector2.zero;
        maskRect.sizeDelta = Vector2.one * sizeOnScreen;
        maskRect.gameObject.AddComponent<RectMask2D>();

        RectTransform mapImageRect = CreateRect("Carte dynamique", maskRect);
        mapImageRect.anchorMin = Vector2.zero;
        mapImageRect.anchorMax = Vector2.one;
        mapImageRect.offsetMin = mapImageRect.offsetMax = Vector2.zero;
        mapImage = mapImageRect.gameObject.AddComponent<RawImage>();
        mapImage.texture = mapTexture;
        mapImage.raycastTarget = false;

        routeRoot = CreateRect("Itineraire GPS", maskRect);
        routeRoot.sizeDelta = Vector2.one * sizeOnScreen;
        routeGraphic = routeRoot.gameObject.AddComponent<MinimapRouteGraphic>();
        routeGraphic.raycastTarget = false;
        RectTransform labelRect = CreateRect("GPS", frame);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0, -8);
        labelRect.sizeDelta = new Vector2(sizeOnScreen + 10, 55);
        gpsLabel = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        CroustyTypography.Apply(gpsLabel);
        gpsLabel.fontSize = 17;
        gpsLabel.alignment = TextAlignmentOptions.Center;
        gpsLabel.raycastTarget = false;
        gpsLabel.color = new Color(0.3f, 0.9f, 1f);

        shopIcon = CreateIcon("Magasin", circleSprite, new Color(1f, 0.82f, 0.13f), 19f);
        policeIcon = CreateIcon("Police", arrowSprite, new Color(1f, 0.24f, 0.2f), 19f);
        busIcon = CreateIcon("Bus", arrowSprite, new Color(0.1f, 0.9f, 1f), 29f);
        busIcon.rectTransform.anchoredPosition = Vector2.zero;

        northRect = CreateRect("Nord", frame);
        northRect.anchorMin = northRect.anchorMax = new Vector2(0.5f, 0.5f);
        northRect.pivot = new Vector2(0.5f, 0.5f);
        northRect.anchoredPosition = new Vector2(0f, 2f);
        northRect.sizeDelta = new Vector2(30f, 22f);
        TextMeshProUGUI north = northRect.gameObject.AddComponent<TextMeshProUGUI>();
        CroustyTypography.Apply(north);
        north.fontSize = 18;
        north.fontStyle = FontStyles.Bold;
        north.alignment = TextAlignmentOptions.Center;
        north.color = Color.white;
        north.text = "N";
        north.raycastTarget = false;
    }
#endif

    private Vector2 ProjectToMap(Vector3 worldPosition)
    {
        worldPosition.y = radar != null ? radar.GroundHeight : 0f;
        Vector3 viewport = mapCamera.WorldToViewportPoint(worldPosition);
        return Vector2.Scale(new Vector2(viewport.x - 0.5f, viewport.y - 0.5f), maskRect.rect.size);
    }

    private void UpdateGPS()
    {
        gpsLabel.gameObject.SetActive(gps != null && (!compactHud || gps.Target != null));
        if (gps != null) gpsLabel.text = $"{gps.Status}\n{gps.Charges}/3 livraisons";
        routeGraphic.UpdateRoute(mapCamera,
            gps != null && gps.isActiveAndEnabled ? gps.Route : null,
            radar != null ? radar.GroundHeight : 0f);
    }

    private void UpdateObjectives()
    {
        bool showShop = deliveries != null && deliveries.shop != null &&
            (!deliveries.HasStarted || deliveries.AllDelivered);
        shopIcon.gameObject.SetActive(showShop);
        if (showShop)
            PlaceIcon(shopIcon, deliveries.shop.transform.position, 19f);

        int used = 0;
        if (deliveries != null)
        {
            foreach (DeliveryPoint point in deliveries.deliveryPoints)
            {
                if (point == null || !point.IsWaiting) continue;
                while (deliveryIcons.Count <= used)
                    deliveryIcons.Add(Instantiate(deliveryIconTemplate, maskRect));
                Image icon = deliveryIcons[used++];
                icon.gameObject.SetActive(true);
                icon.color = point.markerColor;
                PlaceIcon(icon, point.transform.position, 16f);
            }
        }
        for (int i = used; i < deliveryIcons.Count; i++)
            deliveryIcons[i].gameObject.SetActive(false);

        // Pursuers are always shown (on the border when far); patrols only once on the map.
        int shown = 0;
        if (police != null)
            foreach (PoliceCarAI car in police.Cars)
            {
                if (car == null || !car.gameObject.activeInHierarchy) continue;
                if (!car.InPursuit && !IsOnMap(car.transform.position, 16f)) continue;
                while (policeIcons.Count <= shown)
                    policeIcons.Add(shown == 0 ? policeIcon : Instantiate(policeIcon, maskRect));
                Image icon = policeIcons[shown++];
                icon.gameObject.SetActive(true);
                icon.color = car.InPursuit ? new Color(1f, 0.24f, 0.2f) : new Color(0.35f, 0.55f, 1f);
                PlaceIcon(icon, car.transform.position, car.InPursuit ? 19f : 15f);
            }
        if (shown == 0) policeIcon.gameObject.SetActive(false);
        for (int i = Mathf.Max(1, shown); i < policeIcons.Count; i++)
            policeIcons[i].gameObject.SetActive(false);

        UpdateGarages();
        // Le bus reste au-dessus des autres marqueurs.
        busIcon.transform.SetAsLastSibling();
    }

    private void UpdateGarages()
    {
        nearbyGarages.Clear();
        if (city != null) city.GetNearbyGarages(bus.position, 250f, nearbyGarages);
        int shown = Mathf.Min(3, nearbyGarages.Count);
        while (garageIcons.Count < shown)
        {
            var icon = Instantiate(deliveryIconTemplate, maskRect);
            icon.name = "Garage - reparation";
            icon.color = new Color(.05f, .9f, .85f);
            icon.raycastTarget = false;
            var labelObject = new GameObject("G", typeof(RectTransform));
            labelObject.transform.SetParent(icon.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            CroustyTypography.Apply(label);
            label.text = "G"; label.fontSize = 16; label.color = new Color(.02f, .12f, .15f);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            garageIcons.Add(icon);
        }
        for (int i = 0; i < garageIcons.Count; i++)
        {
            var icon = garageIcons[i];
            icon.gameObject.SetActive(i < shown);
            if (i >= shown) continue;
            PlaceIcon(icon, nearbyGarages[i], 28f);
            // Keep the G upright even when the edge arrow turns towards the garage.
            icon.GetComponentInChildren<TMP_Text>().rectTransform.localRotation = Quaternion.Inverse(icon.rectTransform.localRotation);
        }
    }

    private bool IsOnMap(Vector3 targetPosition, float size)
    {
        float radius = Mathf.Min(maskRect.rect.width, maskRect.rect.height) * 0.5f - size * 0.8f;
        Vector2 position = ProjectToMap(targetPosition);
        return Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y)) <= radius;
    }

    private void PlaceIcon(Image icon, Vector3 targetPosition, float size)
    {
        float radius = Mathf.Min(maskRect.rect.width, maskRect.rect.height) * 0.5f - size * 0.8f;
        Vector2 position = ProjectToMap(targetPosition);
        // Intersection avec le bord carre, en conservant la direction de l'objectif.
        float extent = Mathf.Max(Mathf.Abs(position.x), Mathf.Abs(position.y));
        bool outside = extent > radius;
        if (outside) position *= radius / extent;

        icon.rectTransform.anchoredPosition = position;
        icon.rectTransform.sizeDelta = Vector2.one * (outside ? size : size * 0.8f);
        icon.sprite = outside ? arrowSprite : circleSprite;
        float angle = Mathf.Atan2(position.x, position.y) * Mathf.Rad2Deg;
        icon.rectTransform.localRotation = outside
            ? Quaternion.Euler(0f, 0f, -angle)
            : Quaternion.identity;
    }

#if UNITY_EDITOR
    private Image CreateIcon(string name, Sprite sprite, Color color, float size)
    {
        RectTransform rect = CreateRect(name, maskRect);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * size;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.04f, 0.05f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return image;
    }
#endif

#if UNITY_EDITOR
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject instance = new GameObject(name, typeof(RectTransform));
        instance.transform.SetParent(parent, false);
        return instance.GetComponent<RectTransform>();
    }
#endif

#if UNITY_EDITOR
    private static Sprite CreateSmoothSprite(bool arrow, int resolution)
    {
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);
        texture.name = arrow ? "Minimap fleche HD" : "Minimap cercle HD";
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float u = (x + 0.5f) / resolution * 2f - 1f;
            float v = (y + 0.5f) / resolution * 2f - 1f;
            // Distance signee aux bords : une transition fine evite les escaliers en rotation.
            float distance = arrow
                ? Mathf.Min(v + 0.82f, (0.9f - v - 2f * Mathf.Abs(u)) / Mathf.Sqrt(5f))
                : 0.98f - Mathf.Sqrt(u * u + v * v);
            pixels[y * resolution + x] = new Color(1f, 1f, 1f,
                Mathf.Clamp01(distance * resolution * 0.5f + 0.5f));
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
#endif

    private void OnDestroy()
    {
        radar?.Dispose();
        if (mapCamera != null)
        {
            mapCamera.targetTexture = null;
            Destroy(mapCamera.gameObject);
        }

        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
        }
    }
}
