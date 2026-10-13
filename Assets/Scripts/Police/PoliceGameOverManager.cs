using UnityEngine;
using UnityEngine.SceneManagement;

public class PoliceGameOverManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Bus")]
    [SerializeField] private BusController busController;
    private bool gameOver;
    public bool IsGameOver => gameOver;

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
        if (busController != null)
        {
            var flight = busController.GetComponent<BusFlightController>();
            if (flight != null) flight.enabled = false;
            busController.enabled = false;
        }
        if (gameOverPanel != null)
        {
            if (timeout)
            {
                foreach (var text in gameOverPanel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (text.GetComponentInParent<UnityEngine.UI.Button>() == null && text.text.ToUpperInvariant().Contains("POLICE")) text.text = "OUT OF TIME!";
                foreach (var text in gameOverPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if (text.GetComponentInParent<UnityEngine.UI.Button>() == null && text.text.ToUpperInvariant().Contains("POLICE")) text.text = "OUT OF TIME!";
            }
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
