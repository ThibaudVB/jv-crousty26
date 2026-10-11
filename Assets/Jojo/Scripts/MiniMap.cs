using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Mini map ronde en bas à droite de l'écran.
// Une caméra vue du dessus suit le bus et filme dans une texture,
// affichée dans le HUD. Le bus est la flèche au centre.
// Les adresses à livrer (et le magasin quand il faut y retourner) ont une pastille,
// collée au bord de la carte quand elles sont trop loin.
public class MiniMap : MonoBehaviour
{
    [Tooltip("Objet suivi. Si vide, le bus.")]
    public Transform target;

    [Header("Vue")]
    [Tooltip("Distance visible autour du bus, en mètres (zoom)")]
    public float viewRadius = 60f;
    [Tooltip("La carte tourne avec le bus (le haut = devant le bus). Décoché : le nord reste en haut.")]
    public bool rotateWithBus = true;
    public float cameraHeight = 300f;
    public Color backgroundColor = new Color32(0x3A, 0x3A, 0x45, 0xFF);

    [Header("Affichage (depuis le coin bas-droit)")]
    public float size = 280f;
    public Vector2 screenOffset = new Vector2(-30f, 30f);
    public float borderWidth = 6f;
    public Color borderColor = new Color32(0x26, 0x26, 0x31, 0xFF);
    public Color playerColor = new Color(1f, 0.6f, 0.1f);
    public float iconSize = 22f;

    private DeliveryManager manager;
    private Camera mapCamera;
    private RenderTexture texture;
    private RectTransform playerArrow;
    private Transform iconLayer;
    private readonly List<Image> icons = new List<Image>();

    public void Build(Transform canvas)
    {
        if (target == null)
        {
            BusController bus = FindAnyObjectByType<BusController>();
            if (bus != null)
                target = bus.transform;
            else
                Debug.LogWarning("MiniMap : aucun bus (BusController) trouvé.");
        }

        manager = DeliveryManager.Instance;

        CreateCamera();

        // Contour rond
        GameObject root = new GameObject("Mini Map", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = screenOffset;
        rect.sizeDelta = Vector2.one * (size + borderWidth * 2f);
        SetupImage(root.GetComponent<Image>(), JojoVisuals.CircleSprite, borderColor);

        // Masque rond : ce qui dépasse du cercle est caché
        GameObject mask = new GameObject("Masque", typeof(RectTransform), typeof(Image), typeof(Mask));
        mask.transform.SetParent(root.transform, false);
        mask.GetComponent<RectTransform>().sizeDelta = Vector2.one * size;
        SetupImage(mask.GetComponent<Image>(), JojoVisuals.CircleSprite, Color.white);
        mask.GetComponent<Mask>().showMaskGraphic = false;

        // Image filmée par la caméra
        GameObject view = new GameObject("Vue", typeof(RectTransform), typeof(RawImage));
        view.transform.SetParent(mask.transform, false);
        view.GetComponent<RectTransform>().sizeDelta = Vector2.one * size;
        RawImage viewImage = view.GetComponent<RawImage>();
        viewImage.texture = texture;
        viewImage.raycastTarget = false;

        // Pastilles des objectifs, sous la flèche du bus
        iconLayer = new GameObject("Icones", typeof(RectTransform)).transform;
        iconLayer.SetParent(root.transform, false);

        // Le bus, toujours au centre
        GameObject arrow = new GameObject("Bus", typeof(RectTransform), typeof(Image), typeof(Outline));
        arrow.transform.SetParent(root.transform, false);
        playerArrow = arrow.GetComponent<RectTransform>();
        playerArrow.sizeDelta = Vector2.one * size * 0.1f;
        SetupImage(arrow.GetComponent<Image>(), JojoVisuals.ArrowSprite, playerColor);
        arrow.GetComponent<Outline>().effectColor = Color.black;
    }

    private void CreateCamera()
    {
        texture = new RenderTexture(512, 512, 16);

        GameObject cameraObject = new GameObject("Camera Mini Map");
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.nearClipPlane = 1f;
        mapCamera.farClipPlane = cameraHeight * 2f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = backgroundColor;
        mapCamera.targetTexture = texture;

        // Pas d'ombres sur la carte : moins coûteux
        mapCamera.GetUniversalAdditionalCameraData().renderShadows = false;
    }

    private void LateUpdate()
    {
        if (target == null || mapCamera == null)
            return;

        float yaw = target.eulerAngles.y;

        // Au-dessus du bus, regarde vers le bas
        mapCamera.transform.SetPositionAndRotation(
            target.position + Vector3.up * cameraHeight,
            Quaternion.Euler(90f, rotateWithBus ? yaw : 0f, 0f)
        );
        mapCamera.orthographicSize = viewRadius;

        // Carte fixe : c'est la flèche qui tourne
        playerArrow.localRotation = Quaternion.Euler(0f, 0f, rotateWithBus ? 0f : -yaw);

        UpdateIcons(yaw);
    }

    private void UpdateIcons(float yaw)
    {
        int used = 0;

        if (manager != null)
        {
            // Magasin : seulement quand il faut y aller
            CroustyShop shop = manager.shop;
            if (shop != null && (!manager.HasStarted || manager.AllDelivered))
                ShowIcon(used++, shop.transform.position, shop.markerColor, yaw);

            // Adresses avec une commande en attente
            foreach (DeliveryPoint point in manager.deliveryPoints)
            {
                if (point.IsWaiting)
                    ShowIcon(used++, point.transform.position, point.markerColor, yaw);
            }
        }

        // Cache les pastilles en trop
        for (int i = used; i < icons.Count; i++)
            icons[i].gameObject.SetActive(false);
    }

    private void ShowIcon(int index, Vector3 worldPosition, Color color, float yaw)
    {
        while (icons.Count <= index)
            icons.Add(CreateIcon());

        Image icon = icons[index];
        icon.gameObject.SetActive(true);
        icon.color = color;

        // Position par rapport au bus, vue de dessus
        Vector3 offset = worldPosition - target.position;
        Vector2 position = new Vector2(offset.x, offset.z);

        // Si la carte tourne, on tourne la position pareil (devant le bus = en haut)
        if (rotateWithBus)
            position = Quaternion.Euler(0f, 0f, yaw) * position;

        // Mètres -> pixels, puis colle au bord si c'est hors de la carte
        position *= size * 0.5f / viewRadius;
        position = Vector2.ClampMagnitude(position, (size - iconSize) * 0.5f);

        icon.rectTransform.anchoredPosition = position;
    }

    private Image CreateIcon()
    {
        GameObject iconObject = new GameObject("Pastille", typeof(RectTransform), typeof(Image), typeof(Outline));
        iconObject.transform.SetParent(iconLayer, false);
        iconObject.GetComponent<RectTransform>().sizeDelta = Vector2.one * iconSize;
        iconObject.GetComponent<Outline>().effectColor = Color.black;

        Image image = iconObject.GetComponent<Image>();
        SetupImage(image, JojoVisuals.CircleSprite, Color.white);
        return image;
    }

    private void OnDestroy()
    {
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    private static void SetupImage(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
    }
}
