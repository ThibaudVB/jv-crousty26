using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class PoliceGameOverManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;
    [Header("Résultats")]
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text deliveriesText;
    [SerializeField] private TMP_Text bonusText;
    [SerializeField] private TMP_Text explosionsText;
    [SerializeField] private TMP_Text totalText;

    [Header("Bus")]
    [SerializeField] private BusController busController;
    private bool gameOver;
    public bool IsGameOver => gameOver;

    private BusPetController pet;
    private BusFlightController flight;
    private BusGPS gps;
    private BusRiceThrower rice;
    private int lastPetCharges, lastFlightCharges, lastGpsCharges, lastRiceCharges;
    private int bonusUsed;

    private void Start()
    {
        if (busController != null)
        {
            pet = busController.GetComponent<BusPetController>();
            flight = busController.GetComponent<BusFlightController>();
            rice = busController.GetComponent<BusRiceThrower>();
        }
        gps = FindAnyObjectByType<BusGPS>();
        lastPetCharges = pet != null ? pet.UsesRemaining : 0;
        lastFlightCharges = flight != null ? flight.UsesRemaining : 0;
        lastGpsCharges = gps != null ? gps.Charges : 0;
        lastRiceCharges = rice != null ? rice.UsesRemaining : 0;
    }

    private void Update()
    {
        if (gameOver) CroustyMenu.EnsureControllerSelection(gameOverPanel);
        else CountBonuses();
    }

    private void CountBonuses()
    {
        if (pet != null) CountBonus(pet.UsesRemaining, ref lastPetCharges);
        if (flight != null) CountBonus(flight.UsesRemaining, ref lastFlightCharges);
        if (gps != null) CountBonus(gps.Charges, ref lastGpsCharges);
        if (rice != null) CountBonus(rice.UsesRemaining, ref lastRiceCharges);
    }

    private void CountBonus(int charges, ref int lastCharges)
    {
        if (charges < lastCharges) bonusUsed += lastCharges - charges;
        lastCharges = charges;
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
        CountBonuses();
        gameOver = true;
        DeliveryManager.Instance?.EndRunOnCapture();
        ShowResults();
        CroustyLeaderboardScores.Record(DeliveryManager.Instance != null ? DeliveryManager.Instance.Score : 0);
        if (busController != null)
        {
            var flight = busController.GetComponent<BusFlightController>();
            if (flight != null) flight.enabled = false;
            busController.enabled = false;
        }
        if (gameOverPanel != null)
        {
            CroustyPanelMotion.Show(gameOverPanel);
            foreach (var video in gameOverPanel.GetComponentsInChildren<VideoPlayer>(true))
            {
                video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                video.Play();
            }
            CroustySoundDesignManager.PlayUI(CroustySound.Paper);
            CroustySoundDesignManager.PlayUI(CroustySound.Alarm);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        Debug.Log(timeout ? "Game Over : livraison hors delai." : "Game Over : la police a immobilise le bus.", this);
    }

    private void ShowResults()
    {
        var delivery = DeliveryManager.Instance;
        var crash = busController != null ? busController.GetComponent<BusCarCrash>() : null;
        float seconds = Time.timeSinceLevelLoad;
        SetText(timeText, $"{Mathf.FloorToInt(seconds / 60f):00}:{Mathf.FloorToInt(seconds % 60f):00}:{Mathf.FloorToInt(seconds * 100f % 100f):00}");
        SetText(deliveriesText, delivery != null ? delivery.TotalDelivered.ToString() : "0");
        SetText(bonusText, bonusUsed.ToString());
        SetText(explosionsText, crash != null ? crash.CarsExploded.ToString() : "0");
        SetText(totalText, delivery != null ? delivery.Score.ToString() : "0");
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
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
