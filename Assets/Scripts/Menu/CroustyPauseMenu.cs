using UnityEngine;
using UnityEngine.InputSystem;

// Controls only pause state. The visual menu and button events live in the scene Canvas.
[DefaultExecutionOrder(-1000)]
public sealed class CroustyPauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

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
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (paused) Resume();
        else if (Time.timeScale > 0f) Pause();
    }

    public void Pause()
    {
        if (paused || Time.timeScale <= 0f) return;
        previousLockState = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        paused = true;
        lastToggleFrame = Time.frameCount;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        lastToggleFrame = Time.frameCount;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisibility;
    }

    private void OnDisable()
    {
        if (paused) Resume();
    }
}
