using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace V1
{
    // Repères à l'écran (visibles à travers les bâtiments) vers le magasin
    // et les adresses en attente. Une flèche colle au bord quand la cible est hors écran.
    // Ajouté et construit par CroustyHUD.
    public class WaypointHUD : MonoBehaviour
    {
        [SerializeField] private bool showWaypoints = true;
        [SerializeField] private float waypointSize = 44f;
        [SerializeField] private float screenEdgeMargin = 60f;
        [SerializeField] private float heightOffset = 4f;
        [SerializeField] private int labelFontSize = 22;

        private Canvas canvas;
        private DeliveryManager manager;
        private readonly List<Waypoint> waypoints = new List<Waypoint>();

        private class Waypoint
        {
            public RectTransform root;
            public Image icon;
            public RectTransform arrow;
            public Image arrowImage;
            public Text label;

            // Dernier texte affiché, pour ne le reconstruire que s'il change
            public int shownDistance = -1;
            public string shownTitle;
        }

        public void Build(Canvas targetCanvas, DeliveryManager deliveryManager)
        {
            canvas = targetCanvas;
            manager = deliveryManager;
        }

        private void Update()
        {
            if (canvas == null || manager == null)
                return;

            int used = 0;
            Camera cam = Camera.main;

            if (showWaypoints && cam != null && manager.Player != null)
            {
                // Magasin : seulement quand il faut y aller
                CroustyShop shop = manager.Shop;
                if (shop != null && (!manager.HasStarted || manager.AllDelivered))
                    ShowWaypoint(used++, cam, shop, shop.ScreenLabel);

                // Adresses avec une commande en attente
                foreach (DeliveryPoint point in manager.Points)
                {
                    if (point != null && point.IsWaiting)
                        ShowWaypoint(used++, cam, point, null);
                }
            }

            // Cache les repères en trop
            for (int i = used; i < waypoints.Count; i++)
                waypoints[i].root.gameObject.SetActive(false);
        }

        private void ShowWaypoint(int index, Camera cam, BeaconZone target, string title)
        {
            while (waypoints.Count <= index)
                waypoints.Add(CreateWaypoint());

            Waypoint waypoint = waypoints[index];
            waypoint.root.gameObject.SetActive(true);

            Vector3 screen = cam.WorldToScreenPoint(target.transform.position + Vector3.up * heightOffset);
            bool behind = screen.z < 0f;

            float margin = screenEdgeMargin * canvas.scaleFactor;
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 position = new Vector2(screen.x, screen.y);

            // Derrière la caméra, le point est projeté en miroir : on le retourne
            if (behind)
                position = center - (position - center);

            bool onScreen =
                !behind &&
                position.x > margin && position.x < Screen.width - margin &&
                position.y > margin && position.y < Screen.height - margin;

            if (!onScreen)
            {
                // Colle le repère au bord de l'écran, dans la direction de la cible
                Vector2 direction = position - center;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.down;

                Vector2 halfSize = center - Vector2.one * margin;
                float scale = Mathf.Min(
                    halfSize.x / Mathf.Max(Mathf.Abs(direction.x), 0.001f),
                    halfSize.y / Mathf.Max(Mathf.Abs(direction.y), 0.001f)
                );
                position = center + direction * scale;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                waypoint.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                waypoint.arrow.anchoredPosition =
                    (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector3.up) * waypointSize * 0.85f;
            }

            waypoint.arrow.gameObject.SetActive(!onScreen);
            waypoint.root.position = position;

            waypoint.icon.color = target.MarkerColor;
            waypoint.arrowImage.color = target.MarkerColor;

            int distance = Mathf.RoundToInt(target.DistanceTo(manager.Player.position));
            if (distance != waypoint.shownDistance || title != waypoint.shownTitle)
            {
                waypoint.shownDistance = distance;
                waypoint.shownTitle = title;
                waypoint.label.text = string.IsNullOrEmpty(title)
                    ? $"{distance} m"
                    : $"{title}\n{distance} m";
            }
        }

        private Waypoint CreateWaypoint()
        {
            Waypoint waypoint = new Waypoint();

            GameObject root = new GameObject("Repere", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            waypoint.root = root.GetComponent<RectTransform>();
            waypoint.root.sizeDelta = Vector2.one * waypointSize;

            // Rond de couleur
            GameObject icon = new GameObject("Icone", typeof(RectTransform), typeof(Image), typeof(Outline));
            icon.transform.SetParent(root.transform, false);
            waypoint.icon = icon.GetComponent<Image>();
            waypoint.icon.sprite = JojoVisuals.CircleSprite;
            waypoint.icon.raycastTarget = false;
            icon.GetComponent<RectTransform>().sizeDelta = Vector2.one * waypointSize;
            icon.GetComponent<Outline>().effectColor = Color.black;

            // Flèche quand la cible est hors de l'écran
            GameObject arrow = new GameObject("Fleche", typeof(RectTransform), typeof(Image));
            arrow.transform.SetParent(root.transform, false);
            waypoint.arrowImage = arrow.GetComponent<Image>();
            waypoint.arrowImage.sprite = JojoVisuals.ArrowSprite;
            waypoint.arrowImage.raycastTarget = false;
            waypoint.arrow = arrow.GetComponent<RectTransform>();
            waypoint.arrow.sizeDelta = Vector2.one * waypointSize * 0.6f;

            // Distance
            waypoint.label = JojoVisuals.CreateOutlinedText(root.transform, "Distance", JojoVisuals.DefaultFont,
                new Vector2(0.5f, 0f), new Vector2(0f, -8f), TextAnchor.UpperCenter, labelFontSize);
            waypoint.label.rectTransform.pivot = new Vector2(0.5f, 1f);
            waypoint.label.rectTransform.sizeDelta = new Vector2(300f, 80f);

            return waypoint;
        }
    }
}
