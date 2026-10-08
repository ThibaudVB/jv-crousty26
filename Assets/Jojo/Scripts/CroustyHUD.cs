using UnityEngine;
using UnityEngine.UI;

namespace Jojo
{
    // Interface du jeu : crée le Canvas, le compteur de crousty et la boîte du gérant.
    // Les repères à l'écran sont gérés par WaypointHUD, ajouté automatiquement.
    // Si aucun Text n'est assigné, ils sont créés automatiquement.
    public class CroustyHUD : MonoBehaviour
    {
        [SerializeField] private DeliveryManager manager;

        [Header("Textes (optionnels)")]
        [SerializeField] private Text counterText;
        [Tooltip("Si vide, les messages passent par la boîte de dialogue du gérant")]
        [SerializeField] private Text messageText;

        [SerializeField] private int fontSize = 32;

        private GerantDialogue gerant;

        private void Start()
        {
            if (manager == null)
                manager = DeliveryManager.Instance != null
                    ? DeliveryManager.Instance
                    : FindAnyObjectByType<DeliveryManager>();

            Canvas canvas = BuildCanvas();

            if (counterText == null)
                counterText = JojoVisuals.CreateOutlinedText(
                    canvas.transform, "Compteur", JojoVisuals.DefaultFont,
                    new Vector2(0f, 1f), new Vector2(30f, -30f),
                    TextAnchor.UpperLeft, fontSize
                );

            if (messageText == null)
            {
                gerant = GetComponent<GerantDialogue>();
                if (gerant == null)
                    gerant = gameObject.AddComponent<GerantDialogue>();

                gerant.Build(canvas.transform);
            }

            WaypointHUD waypoints = GetComponent<WaypointHUD>();
            if (waypoints == null)
                waypoints = gameObject.AddComponent<WaypointHUD>();
            waypoints.Build(canvas, manager);

            if (manager != null)
                manager.Changed += RefreshCounter;
            RefreshCounter();
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.Changed -= RefreshCounter;
        }

        private void Update()
        {
            if (manager == null)
                return;

            string message = GerantLines.For(manager);
            if (gerant != null)
                gerant.SetMessage(message);
            else
                messageText.text = message;
        }

        private void RefreshCounter()
        {
            if (manager == null)
                return;

            counterText.text = manager.HasStarted
                ? $"Crousty : {manager.CroustyLeft} / {manager.OrdersTotal}"
                : "Crousty : 0";
        }

        private Canvas BuildCanvas()
        {
            GameObject canvasObject = new GameObject(
                "Crousty HUD",
                typeof(Canvas),
                typeof(CanvasScaler)
            );
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return canvas;
        }
    }
}
