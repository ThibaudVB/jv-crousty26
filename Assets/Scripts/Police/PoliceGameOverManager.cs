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
        => TriggerGameOver(null);
    public void TriggerTimeoutGameOver()
        => TriggerGameOver("OUT OF TIME!");
    public void TriggerWreckGameOver()
        => TriggerGameOver("WRECKED!");
    // title : remplace le titre "POLICE" du panneau (null = garde le titre de la police)
    private void TriggerGameOver(string title)
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
            if (title != null)
            {
                foreach (var text in gameOverPanel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (text.GetComponentInParent<UnityEngine.UI.Button>() == null && text.text.ToUpperInvariant().Contains("POLICE")) text.text = title;
                foreach (var text in gameOverPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    if (text.GetComponentInParent<UnityEngine.UI.Button>() == null && text.text.ToUpperInvariant().Contains("POLICE")) text.text = title;
            }
            CroustyPanelMotion.Show(gameOverPanel);
            CroustySoundDesignManager.PlayUI(CroustySound.Paper);
            CroustySoundDesignManager.PlayUI(CroustySound.Alarm);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        Debug.Log(title != null ? "Game Over : " + title : "Game Over : la police a immobilise le bus.", this);
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
