using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Controls only pause state. The visual menu and button events live in the scene Canvas.
[DefaultExecutionOrder(-1000)]
public sealed class CroustyPauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private CroustyMenu optionsMenu;

    private bool paused;
    private CursorLockMode previousLockState;
    private bool previousCursorVisibility;
    private static int lastToggleFrame = -1;

    public static bool ToggledThisFrame => lastToggleFrame == Time.frameCount;

    private void Awake()
    {
        if (pausePanel == null)
        {
            Debug.LogError("Assigner le Panel pause dans l'Inspector.", this);
            enabled = false;
            return;
        }
        pausePanel.SetActive(false);
        CroustyPanelMotion.Prepare(pausePanel);
    }

    private void Update()
    {
        if (paused && (optionsMenu == null || !optionsMenu.IsOptionsOpen))
            CroustyMenu.EnsureControllerSelection(pausePanel);
        var pad = Gamepad.current;
        bool toggle = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (pad != null && (pad.startButton.wasPressedThisFrame ||
                (paused && pad.buttonEast.wasPressedThisFrame)));
        if (!toggle) return;
        if (paused && optionsMenu != null && optionsMenu.IsOptionsOpen)
        {
            lastToggleFrame = Time.frameCount;
            optionsMenu.CloseOptions();
        }
        else if (paused) Resume();
        else if (Time.timeScale > 0f) Pause();
    }

    public void Pause()
    {
        if (paused || Time.timeScale <= 0f) return;
        previousLockState = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        paused = true;
        lastToggleFrame = Time.frameCount;
        CroustyPanelMotion.Show(pausePanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        lastToggleFrame = Time.frameCount;
        bool closingOptions = optionsMenu != null && optionsMenu.IsOptionsOpen;
        if (closingOptions) optionsMenu.CloseOptions();
        CroustyPanelMotion.Hide(pausePanel);
        if (!closingOptions) CroustySoundDesignManager.PlayUI(CroustySound.Paper);
        Time.timeScale = 1f;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisibility;
    }

    private void OnDisable()
    {
        if (paused) Resume();
    }

    public void OpenSettings()
    {
        if (!paused || optionsMenu == null) return;
        optionsMenu.OpenOptions();
    }

    public void ReturnToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded("CroustyMainMenu"))
        {
            Debug.LogError("Ajouter CroustyMainMenu aux scènes du build.", this);
            return;
        }
        PlayerPrefs.Save();
        Resume();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("CroustyMainMenu");
    }
}
