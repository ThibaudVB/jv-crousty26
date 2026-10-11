using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the authored panels in CroustyMainMenu; never creates or lays out UI.</summary>
public sealed class CroustyMenu : MonoBehaviour
{
    public const string SensitivityKey = "Crousty.MouseSensitivity";
    private const string VolumeKey = "Crousty.MasterVolume";

    [Header("Panels placés dans la scène")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
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

    private void Awake()
    {
        if (mainPanel == null || optionsPanel == null)
        {
            Debug.LogError("Les panels du menu doivent être assignés dans l'Inspector.", this);
            enabled = false;
            return;
        }
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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
    }

    private void Update()
    {
        if (optionsPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseOptions();
        // Unity applies fullscreen changes at the end of the frame.
        if (requestedFullscreen.HasValue && Screen.fullScreen == requestedFullscreen.Value)
            requestedFullscreen = null;
        if (!requestedFullscreen.HasValue) RefreshFullscreenLabel(Screen.fullScreen);
    }

    public void OpenOptions()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void SelectCategory(int index)
    {
        if (index < 0 || index >= categoryPanels.Length) return;
        for (int i = 0; i < categoryPanels.Length; i++)
        {
            bool selected = i == index;
            categoryPanels[i].SetActive(selected);
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
