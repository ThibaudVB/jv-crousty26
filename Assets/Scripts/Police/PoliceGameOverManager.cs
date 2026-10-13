using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Écran de fin de partie ("Results") : s'affiche quand la police attrape le bus ou quand le chrono est fini.
// Il montre la durée de la partie, les livraisons, les bonus utilisés, les voitures explosées et le score.
// Tous les éléments (panel, vidéo de fond, textes, bouton) sont placés dans le Canvas de la scène.
public class PoliceGameOverManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Textes de l'écran de fin")]
    [Tooltip("Durée de la partie (minutes:secondes:centièmes)")]
    [SerializeField] private TMP_Text timeText;
    [Tooltip("Crousty livrés pendant la partie")]
    [SerializeField] private TMP_Text deliveriesText;
    [Tooltip("Nombre de bonus utilisés (pet, vol, GPS, riz)")]
    [SerializeField] private TMP_Text bonusText;
    [Tooltip("Nombre de voitures explosées")]
    [SerializeField] private TMP_Text explosionsText;
    [Tooltip("Score de la partie")]
    [SerializeField] private TMP_Text totalText;

    [Header("Bus")]
    [SerializeField] private BusController busController;
    private bool gameOver;
    public bool IsGameOver => gameOver;

    // Comptage des bonus utilisés : on regarde les charges de chaque bonus,
    // et chaque charge en moins compte pour un bonus utilisé.
    private BusPetController pet;
    private BusFlightController flight;
    private BusGPS gps;
    private BusRiceThrower rice;
    private int lastPetCharges, lastFlightCharges, lastGpsCharges, lastRiceCharges;
    private int bonusUsed;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        pet = FindAnyObjectByType<BusPetController>();
        flight = FindAnyObjectByType<BusFlightController>();
        gps = FindAnyObjectByType<BusGPS>();
        rice = FindAnyObjectByType<BusRiceThrower>();

        lastPetCharges = pet != null ? pet.UsesRemaining : 0;
        lastFlightCharges = flight != null ? flight.UsesRemaining : 0;
        lastGpsCharges = gps != null ? gps.Charges : 0;
        lastRiceCharges = rice != null ? rice.UsesRemaining : 0;
    }

    private void Update()
    {
        if (gameOver)
            return;

        if (pet != null) CountBonus(pet.UsesRemaining, ref lastPetCharges);
        if (flight != null) CountBonus(flight.UsesRemaining, ref lastFlightCharges);
        if (gps != null) CountBonus(gps.Charges, ref lastGpsCharges);
        if (rice != null) CountBonus(rice.UsesRemaining, ref lastRiceCharges);
    }

    // Si les charges ont baissé depuis la dernière image, des bonus ont été utilisés
    private void CountBonus(int charges, ref int lastCharges)
    {
        if (charges < lastCharges)
            bonusUsed += lastCharges - charges;
        lastCharges = charges;
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
            ShowResults();
            CroustyPanelMotion.Show(gameOverPanel);
            CroustySoundDesignManager.PlayUI(CroustySound.Paper);
            CroustySoundDesignManager.PlayUI(CroustySound.Alarm);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        Debug.Log(timeout ? "Game Over : livraison hors delai." : "Game Over : la police a immobilise le bus.", this);
    }

    // Écrit les résultats de la partie dans les textes de l'écran de fin
    private void ShowResults()
    {
        DeliveryManager manager = DeliveryManager.Instance;
        BusCarCrash crash = busController != null ? busController.GetComponent<BusCarCrash>() : null;

        // Durée de la partie (ne compte pas le temps passé en pause)
        float seconds = Time.timeSinceLevelLoad;
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int wholeSeconds = Mathf.FloorToInt(seconds % 60f);
        int hundredths = Mathf.FloorToInt(seconds * 100f % 100f);
        SetText(timeText, $"{minutes:00}:{wholeSeconds:00}:{hundredths:00}");

        SetText(deliveriesText, manager != null ? manager.TotalDelivered.ToString() : "0");
        SetText(bonusText, bonusUsed.ToString());
        SetText(explosionsText, crash != null ? crash.CarsExploded.ToString() : "0");
        SetText(totalText, manager != null ? manager.Score.ToString() : "0");
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
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
