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
    [SerializeField] private TMP_Text moneyText;
    public int FinalScore { get; private set; }

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
        var delivery = DeliveryManager.Instance;
        if (delivery != null) delivery.RecordFinalScore(FinalScore);
        else CroustyLeaderboardScores.Record(FinalScore, CroustyPlayerProfile.PlayerName);
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
        int seconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        int delivered = delivery != null ? delivery.TotalDelivered : 0;
        int explosions = crash != null ? crash.CarsExploded : 0;
        int earned = delivery != null ? delivery.MoneyEarned : 0;
        FinalScore = CalculateFinalScore(seconds, delivered, bonusUsed, explosions, earned);
        SetText(timeText, $"{seconds / 60:00}:{seconds % 60:00}  +{seconds * 20}");
        SetText(deliveriesText, $"{delivered}  +{delivered * 100}");
        SetText(bonusText, $"{bonusUsed}  +{bonusUsed * 5}");
        SetText(explosionsText, $"{explosions}  +{explosions * 30}");
        SetText(moneyText, $"${earned}  +{earned * 60}");
        SetText(totalText, FinalScore.ToString());
    }

    public static int CalculateFinalScore(int seconds, int delivered, int bonuses, int explosions, int earned)
        => Mathf.Max(0, seconds) * 20 + Mathf.Max(0, delivered) * 100 + Mathf.Max(0, bonuses) * 5
            + Mathf.Max(0, explosions) * 30 + Mathf.Max(0, earned) * 60;

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("CroustyMainMenu");
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
