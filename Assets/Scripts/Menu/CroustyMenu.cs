using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Controls the authored panels in CroustyMainMenu; never creates or lays out UI.</summary>
public sealed class CroustyMenu : MonoBehaviour
{
    public const string SensitivityKey = "Crousty.MouseSensitivity";
    private const string VolumeKey = "Crousty.MasterVolume";

    [Header("Panels placés dans la scène")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject leaderboardPanel;
    public CroustyMenuPages homePages;
    [Tooltip("Assigné uniquement pour les options du menu pause.")]
    public CroustyPauseMenu pauseMenu;
    [Tooltip("Commandes, Audio, Vidéo, Crédits")]
    public GameObject[] categoryPanels;

    [Header("Réglages")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityLabel;
    public Slider volumeSlider;
    public TMP_Text volumeLabel;
    public TMP_Text fullscreenLabel;

    [Header("Onglets")]
    public CroustyMenuHover[] tabs;
    public Graphic[] tabBackgrounds;
    public Graphic[] tabShadows;
    public TMP_Text[] tabLabels;
    public Sprite orangeRibbon;
    public Sprite creamRibbon;
    public Color selectedColor = new Color32(233, 132, 36, 255);
    public Color normalColor = new Color32(239, 233, 225, 255);
    public Color inkColor = new Color32(38, 38, 50, 255);
    public Color selectedTextColor = new Color32(255, 253, 247, 255);

    private bool launching;
    private bool? requestedFullscreen;
    private bool animationReady;
    private int selectedCategory = -1;
    public bool IsOptionsOpen { get; private set; }
    public bool IsLeaderboardOpen { get; private set; }

    private void Awake()
    {
        if (mainPanel == null || optionsPanel == null)
        {
            Debug.LogError("Les panels du menu doivent être assignés dans l'Inspector.", this);
            enabled = false;
            return;
        }
        if (pauseMenu == null)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        float sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.75f), sensitivitySlider.minValue, sensitivitySlider.maxValue);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        sensitivityLabel.text = sensitivity.ToString("0.00");
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        volumeSlider.SetValueWithoutNotify(volume);
        AudioListener.volume = volume;
        volumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
        RefreshFullscreenLabel(Screen.fullScreen);
        SelectCategory(0);
        CloseOptions();
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (pauseMenu != null) mainPanel.SetActive(false);
        CroustyPanelMotion.Prepare(mainPanel);
        CroustyPanelMotion.Prepare(optionsPanel);
        foreach (var category in categoryPanels) CroustyPanelMotion.Prepare(category);
        animationReady = true;
    }

    private void Update()
    {
        if (pauseMenu == null || IsOptionsOpen)
            EnsureControllerSelection(IsOptionsOpen ? optionsPanel : IsLeaderboardOpen ? leaderboardPanel : mainPanel);
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (pauseMenu == null && back)
        {
            if (IsOptionsOpen) CloseOptions();
            else if (IsLeaderboardOpen) CloseLeaderboard();
            else if (homePages != null && homePages.IsExpanded) homePages.SetExpanded(false);
        }
        // Unity applies fullscreen changes at the end of the frame.
        if (requestedFullscreen.HasValue && Screen.fullScreen == requestedFullscreen.Value)
            requestedFullscreen = null;
        if (!requestedFullscreen.HasValue) RefreshFullscreenLabel(Screen.fullScreen);
    }

    // Select authored controls after transitions, page changes or controller connection.
    // Submission and directional navigation remain owned by InputSystemUIInputModule.
    public static void EnsureControllerSelection(GameObject panel)
    {
        var events = EventSystem.current;
        if (Gamepad.current == null || events == null || panel == null || !panel.activeInHierarchy) return;
        var current = events.currentSelectedGameObject;
        if (current != null && current.transform.IsChildOf(panel.transform))
        {
            var selected = current.GetComponent<Selectable>();
            if (selected != null && selected.isActiveAndEnabled && selected.IsInteractable()) return;
        }
        foreach (var control in panel.GetComponentsInChildren<Selectable>())
        {
            if (!control.isActiveAndEnabled || !control.IsInteractable() ||
                control.navigation.mode == Navigation.Mode.None) continue;
            events.SetSelectedGameObject(control.gameObject);
            return;
        }
        events.SetSelectedGameObject(null);
    }

    public void OpenOptions()
    {
        if (IsOptionsOpen) return;
        IsOptionsOpen = true;
        CroustyPanelMotion.Hide(mainPanel);
        CroustyPanelMotion.Show(optionsPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    public void ToggleMore() => homePages?.Toggle();

    public void OpenSettings()
    {
        SelectCategory(0);
        OpenOptions();
    }

    public void OpenCredits()
    {
        SelectCategory(3);
        OpenOptions();
    }

    public void OpenLeaderboard()
    {
        if (leaderboardPanel == null || IsLeaderboardOpen) return;
        IsLeaderboardOpen = true;
        CroustyPanelMotion.Hide(mainPanel);
        CroustyPanelMotion.Show(leaderboardPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    public void CloseLeaderboard()
    {
        if (!IsLeaderboardOpen) return;
        IsLeaderboardOpen = false;
        CroustyPanelMotion.Hide(leaderboardPanel);
        CroustyPanelMotion.Show(mainPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    public void CloseOptions()
    {
        if (animationReady && !IsOptionsOpen) return;
        IsOptionsOpen = false;
        if (!animationReady) { optionsPanel.SetActive(false); mainPanel.SetActive(true); return; }
        CroustyPanelMotion.Hide(optionsPanel);
        CroustyPanelMotion.Show(mainPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    public void SelectCategory(int index)
    {
        if (index < 0 || index >= categoryPanels.Length) return;
        bool changed = selectedCategory != index;
        selectedCategory = index;
        for (int i = 0; i < categoryPanels.Length; i++)
        {
            bool selected = i == index;
            if (animationReady && changed)
            {
                if (selected) CroustyPanelMotion.Show(categoryPanels[i]);
                else CroustyPanelMotion.Hide(categoryPanels[i]);
            }
            else if (!animationReady) categoryPanels[i].SetActive(selected);
            tabs[i].highlighted = selected;
            if (tabBackgrounds[i] is Image image && orangeRibbon != null && creamRibbon != null)
                image.sprite = selected ? orangeRibbon : creamRibbon;
            else tabBackgrounds[i].color = selected ? selectedColor : normalColor;
            if (tabShadows[i] != null) tabShadows[i].color = selected ? inkColor : Color.clear;
            tabLabels[i].color = selected ? selectedTextColor : inkColor;
        }
    }

    public void SetSensitivity(float value)
    {
        sensitivityLabel.text = value.ToString("0.00");
        PlayerPrefs.SetFloat(SensitivityKey, value);
        if (pauseMenu != null)
            foreach (var camera in Object.FindObjectsByType<BusCameraFollow>())
                camera.mouseSensitivity = Mathf.Clamp(value, 0.2f, 1.5f) * 0.16f;
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    public void ToggleFullscreen()
    {
        bool target = !(requestedFullscreen ?? Screen.fullScreen);
        requestedFullscreen = target;
        Screen.fullScreen = target;
        RefreshFullscreenLabel(target);
    }

    private void RefreshFullscreenLabel(bool fullscreen) =>
        fullscreenLabel.text = fullscreen ? "ACTIVÉ" : "DÉSACTIVÉ";

    private void OnDisable() => PlayerPrefs.Save();

    public void Play()
    {
        if (launching) return;
        if (!Application.CanStreamedLevelBeLoaded("SceneBase"))
        {
            Debug.LogError("Ajouter SceneBase aux scènes du build.", this);
            return;
        }
        launching = true;
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene("SceneBase");
    }

    public void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
