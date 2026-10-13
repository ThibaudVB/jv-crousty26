using UnityEngine;
using UnityEngine.SceneManagement;

public class PoliceGameOverManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMPro.TMP_Text moneyValue;
    [SerializeField] private TMPro.TMP_Text deliveriesValue;
    [SerializeField] private TMPro.TMP_Text totalValue;
    [Header("Score final")]
    [SerializeField, Min(0)] private int pointsPerMoney = 1;
    [SerializeField, Min(0)] private int pointsPerDelivery = 100;

    [Header("Bus")]
    [SerializeField] private BusController busController;
    private bool gameOver;
    public bool IsGameOver => gameOver;

    private void Update()
    {
        if (gameOver) CroustyMenu.EnsureControllerSelection(gameOverPanel);
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void TriggerPoliceGameOver()
        => TriggerGameOver(false);
    public void TriggerTimeoutGameOver()
        => TriggerGameOver(true);
    private void TriggerGameOver(bool timeout)
    {
        if (gameOver)
            return;
        gameOver = true;
        DeliveryManager.Instance?.EndRunOnCapture();
        var delivery = DeliveryManager.Instance;
        int money = delivery != null ? delivery.Score : 0;
        int delivered = delivery != null ? delivery.TotalDelivered : 0;
        long total = (long)money * Mathf.Max(0, pointsPerMoney) + (long)delivered * Mathf.Max(0, pointsPerDelivery);
        if (moneyValue != null) moneyValue.text = money.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        if (deliveriesValue != null) deliveriesValue.text = delivered.ToString();
        if (totalValue != null) totalValue.text = $"Total : {total} Pts";
        if (busController != null)
        {
            var flight = busController.GetComponent<BusFlightController>();
            if (flight != null) flight.enabled = false;
            busController.enabled = false;
        }
        if (gameOverPanel != null)
        {
            CroustyPanelMotion.Show(gameOverPanel);
            CroustySoundDesignManager.PlayUI(CroustySound.Paper);
            CroustySoundDesignManager.PlayUI(CroustySound.Alarm);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        Debug.Log(timeout ? "Game Over : livraison hors delai." : "Game Over : la police a immobilise le bus.", this);
    }

    public void RestartLevel()
    {
        Scene scene = SceneManager.GetActiveScene();
        // Le chemin fonctionne meme si l'ordre des scenes de build change.
        if (string.IsNullOrEmpty(scene.path) || !Application.CanStreamedLevelBeLoaded(scene.path))
        {
            Debug.LogError("Impossible de recommencer : ajouter la scene actuelle aux scenes de build.", this);
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene.path);
    }
}
