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
    public CroustyNameEntry nameEntry;
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
    [Tooltip("Glissement (px) d'une case au survol de la souris")]
    public float tabHoverOffset = 10f;

    private bool launching;
    private bool? requestedFullscreen;
    private bool animationReady;
    private int selectedCategory = -1;
    private TMP_Text optionsTitle;
    private Image controllerConfirmPrompt;
    private string defaultOptionsTitle;
    public bool IsOptionsOpen { get; private set; }
    public bool IsLeaderboardOpen { get; private set; }
    public bool IsNameEntryOpen { get; private set; }

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
        if (sensitivitySlider != null)
        {
            float sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.75f), sensitivitySlider.minValue, sensitivitySlider.maxValue);
            sensitivitySlider.SetValueWithoutNotify(sensitivity);
            if (sensitivityLabel != null) sensitivityLabel.text = sensitivity.ToString("0.00");
        }
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        volumeSlider.SetValueWithoutNotify(volume);
        AudioListener.volume = volume;
        volumeLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
        RefreshFullscreenLabel(Screen.fullScreen);
        SetupTabHover();
        if (pauseMenu != null) RestyleSectionLabels();
        foreach (TMP_Text text in optionsPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name != "OPTIONS texte") continue;
            optionsTitle = text;
            defaultOptionsTitle = text.text;
            break;
        }
        if (optionsTitle == null)
            Debug.LogError("Le titre OPTIONS texte est introuvable dans le panneau des réglages.", this);
        SelectCategory(0);
        CloseOptions();
        SetSettingsNavigationVisible(true);
        SetSettingsCreditsTabVisible(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (nameEntry != null) nameEntry.gameObject.SetActive(false);
        if (pauseMenu != null) mainPanel.SetActive(false);
        CroustyPanelMotion.Prepare(mainPanel);
        CroustyPanelMotion.Prepare(optionsPanel);
        foreach (var category in categoryPanels) CroustyPanelMotion.Prepare(category);
        animationReady = true;
    }

    private void Update()
    {
        CroustyInput.UpdateMenuDevice();
        if (pauseMenu == null || IsOptionsOpen)
            EnsureControllerSelection(IsOptionsOpen ? optionsPanel : IsLeaderboardOpen ? leaderboardPanel :
                IsNameEntryOpen ? nameEntry.gameObject : mainPanel);
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (pauseMenu == null && back)
        {
            if (IsNameEntryOpen) nameEntry.Back();
            else if (IsOptionsOpen) CloseOptions();
            else if (IsLeaderboardOpen) CloseLeaderboard();
            else if (homePages != null && homePages.IsExpanded) homePages.SetExpanded(false);
        }
        // Unity applies fullscreen changes at the end of the frame.
        if (requestedFullscreen.HasValue && Screen.fullScreen == requestedFullscreen.Value)
            requestedFullscreen = null;
        if (!requestedFullscreen.HasValue) RefreshFullscreenLabel(Screen.fullScreen);
    }

    private void LateUpdate()
    {
        if (pauseMenu != null) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var button = selected != null ? selected.GetComponent<Button>() : null;
        bool navigationTicket = button != null && homePages != null &&
            button.transform == homePages.navigationTab;
        bool visible = CroustyInput.MenuUsesController && button != null && button.IsInteractable() &&
            button.isActiveAndEnabled && (nameEntry == null || !nameEntry.IsLaunching) &&
            (!navigationTicket || !homePages.IsAnimating);
        if (!visible)
        {
            if (controllerConfirmPrompt != null) controllerConfirmPrompt.gameObject.SetActive(false);
            return;
        }
        if (controllerConfirmPrompt == null)
        {
            var prompt = new GameObject("Controller confirm A", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas));
            controllerConfirmPrompt = prompt.GetComponent<Image>();
            controllerConfirmPrompt.sprite = Resources.Load<Sprite>("Crousty/ControllerConfirmA");
            controllerConfirmPrompt.raycastTarget = false;
            controllerConfirmPrompt.preserveAspect = true;
        }
        var rect = controllerConfirmPrompt.rectTransform;
        rect.SetParent(button.transform, false);
        // Keep the prompt inside the ribbon, before its label, away from the bus artwork.
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(24, 0);
        if (navigationTicket)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = homePages.ControllerPromptPosition;
        }
        rect.sizeDelta = new Vector2(28, 28);
        rect.SetAsLastSibling();
        var promptCanvas = controllerConfirmPrompt.GetComponent<Canvas>();
        var menuCanvas = button.GetComponentInParent<Canvas>();
        promptCanvas.overrideSorting = true;
        if (menuCanvas != null)
        {
            promptCanvas.sortingLayerID = menuCanvas.rootCanvas.sortingLayerID;
            promptCanvas.sortingOrder = menuCanvas.rootCanvas.sortingOrder + 1;
        }
        controllerConfirmPrompt.gameObject.SetActive(true);
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
        SetCreditsPageMode(false);
        SetSettingsNavigationVisible(true);
        SetSettingsCreditsTabVisible(false);
        IsOptionsOpen = true;
        CroustyPanelMotion.Hide(mainPanel);
        CroustyPanelMotion.Show(optionsPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    public void ToggleMore() => homePages?.Toggle();

    public void OpenSettings()
    {
        SetCreditsPageMode(false);
        SetSettingsNavigationVisible(true);
        if (categoryPanels != null && categoryPanels.Length > 3 && categoryPanels[3] != null)
            categoryPanels[3].SetActive(false);
        SelectCategory(0);
        OpenOptions();
    }

    public void OpenCredits()
    {
        if (categoryPanels == null || categoryPanels.Length <= 3)
        {
            Debug.LogError("Le panneau de crédits doit être assigné comme quatrième catégorie.", this);
            return;
        }
        OpenOptions();
        SetSettingsNavigationVisible(false);
        SetSettingsCreditsTabVisible(false);
        SetCreditsPageMode(true);
        SelectCategory(3);
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
        SetCreditsPageMode(false);
        SetSettingsNavigationVisible(true);
        SetSettingsCreditsTabVisible(false);
        IsOptionsOpen = false;
        if (!animationReady) { optionsPanel.SetActive(false); mainPanel.SetActive(true); return; }
        CroustyPanelMotion.Hide(optionsPanel);
        CroustyPanelMotion.Show(mainPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    // Au survol, le texte glisse et le bandeau s'allonge en restant collé au bord de l'écran,
    // même pour la case sélectionnée. Pareil pour le bouton Retour.
    private void SetupTabHover()
    {
        if (tabs != null)
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var background = tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null
                    ? tabBackgrounds[i].rectTransform : null;
                var shadow = tabShadows != null && i < tabShadows.Length && tabShadows[i] != null
                    ? tabShadows[i].rectTransform : null;
                tabs[i].followers = new[] { background, shadow };
                tabs[i].stretchWidth = background != null ? background.rect.width : 312f;
                tabs[i].hoverOffset = tabHoverOffset;
            }

        var back = FindDeep(optionsPanel.transform, "RETOUR");
        var settingsBack = back != null ? back.GetComponent<CroustyMenuHover>() : null;

        // Le Retour du classement reprend la mise en page et le survol de celui des réglages.
        if (leaderboardPanel != null)
            foreach (var hover in leaderboardPanel.GetComponentsInChildren<CroustyMenuHover>(true))
                if (hover.name == "Retour classement")
                {
                    CopyBackLayout(settingsBack, hover);
                    SetupBackHover(hover);
                    CroustyPinLeft.Pin(hover.transform);
                }
        // Pareil pour le Retour de l'écran « nom du joueur » (avant de lancer la partie).
        if (nameEntry != null)
            foreach (var hover in nameEntry.GetComponentsInChildren<CroustyMenuHover>(true))
                if (hover.name == "Retour")
                {
                    CopyBackLayout(settingsBack, hover);
                    SetupBackHover(hover);
                    CroustyPinLeft.Pin(hover.transform);
                }
        SetupBackHover(settingsBack);

        // Les bandeaux de gauche (Retour + onglets) restent collés au bord de l'écran,
        // même quand le cadre 848 x 480 est centré sur un écran plus large.
        var pinned = new System.Collections.Generic.List<Transform>();
        if (settingsBack != null) pinned.Add(settingsBack.transform);
        if (tabs != null)
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                if (tabShadows != null && i < tabShadows.Length && tabShadows[i] != null) pinned.Add(tabShadows[i].transform);
                if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null) pinned.Add(tabBackgrounds[i].transform);
                pinned.Add(tabs[i].transform);
            }
        CroustyPinLeft.Pin(pinned.ToArray());
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == childName) return child;
        return null;
    }

    // Contour de l'étiquette « OPTIONS », ramené à un rectangle 0..1.
    private static readonly Vector2[] SectionTagShape =
    {
        new Vector2(0f, 0.604167f), new Vector2(0.184397f, 0.979167f), new Vector2(0.58156f, 1f),
        new Vector2(0.673759f, 0.895833f), new Vector2(0.964539f, 0.708333f), new Vector2(1f, 0.395833f),
        new Vector2(0.808511f, 0f), new Vector2(0f, 0.0625f)
    };

    // Menu pause : les petits titres (SOURIS, AUDIO, VIDÉO...) prennent la même étiquette
    // et le même centrage que dans le menu principal.
    private void RestyleSectionLabels()
    {
        if (categoryPanels == null) return;
        foreach (var panel in categoryPanels)
        {
            if (panel == null) continue;
            foreach (var tag in panel.GetComponentsInChildren<CroustyVectorGraphic>(true))
            {
                const string suffix = " étiquette";
                if (!tag.name.EndsWith(suffix)) continue;
                string title = tag.name.Substring(0, tag.name.Length - suffix.Length);
                var label = tag.transform.parent.Find(title + " texte")?.GetComponent<TMP_Text>();
                if (label == null) continue;

                // Ancienne mise en page : texte à (x + 7, y + 3) d'une étiquette de 28 px de haut.
                Vector2 textPosition = label.rectTransform.anchoredPosition;
                var tagRect = tag.rectTransform;
                tagRect.anchorMin = tagRect.anchorMax = tagRect.pivot = new Vector2(0f, 1f);
                tagRect.anchoredPosition = textPosition + new Vector2(-7f, 6f);
                tagRect.sizeDelta = new Vector2(116f, 26f);
                tag.points = SectionTagShape;
                tag.SetVerticesDirty();

                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0f, 1f);
                labelRect.anchoredPosition = tagRect.anchoredPosition + new Vector2(2f, -0.4f);
                labelRect.sizeDelta = new Vector2(106f, 24f);
                label.fontSize = 13f;
                label.enableAutoSizing = false;
                label.horizontalAlignment = HorizontalAlignmentOptions.Center;
                label.verticalAlignment = VerticalAlignmentOptions.Geometry;
            }
        }
    }

    // Même place, même bandeau, même texte que le bouton Retour des réglages.
    private static void CopyBackLayout(CroustyMenuHover source, CroustyMenuHover target)
    {
        if (source == null || source.colorTarget == null || source.textTarget == null) return;
        Image ribbon = target.colorTarget != null ? target.colorTarget : target.ribbonImage;
        if (ribbon == null || target.textTarget == null) return;

        TMP_Text label = target.textTarget, sourceLabel = source.textTarget;
        label.rectTransform.SetParent(target.transform, false);
        ribbon.rectTransform.SetParent(target.transform, false);
        ribbon.transform.SetAsFirstSibling();
        CopyRect((RectTransform)source.transform, (RectTransform)target.transform);
        CopyRect(source.colorTarget.rectTransform, ribbon.rectTransform);
        CopyRect(sourceLabel.rectTransform, label.rectTransform);

        ribbon.sprite = source.colorTarget.sprite;
        ribbon.type = source.colorTarget.type;
        ribbon.preserveAspect = source.colorTarget.preserveAspect;
        if (target.ribbonImage != null) target.idleRibbon = ribbon.sprite;

        label.font = sourceLabel.font;
        label.fontSize = sourceLabel.fontSize;
        label.enableAutoSizing = sourceLabel.enableAutoSizing;
        label.fontStyle = sourceLabel.fontStyle;
        label.alignment = sourceLabel.alignment;
        label.characterSpacing = sourceLabel.characterSpacing;
        label.margin = sourceLabel.margin;
        label.color = sourceLabel.color;
    }

    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta;
        to.anchoredPosition = from.anchoredPosition;
        to.localScale = from.localScale;
        to.localRotation = from.localRotation;
    }

    // Bouton Retour : le texte glisse, le bandeau crème s'allonge depuis le bord et devient orange.
    private void SetupBackHover(CroustyMenuHover hover)
    {
        if (hover == null) return;
        Image ribbon = hover.colorTarget != null ? hover.colorTarget : hover.ribbonImage;
        if (ribbon == null || hover.textTarget == null) return;

        if (hover.ribbonImage != null)
        {
            // Ancien survol par changement d'image : on garde l'image crème et on la teinte.
            if (hover.idleRibbon != null) ribbon.sprite = hover.idleRibbon;
            hover.ribbonImage = null;
            hover.colorTarget = ribbon;
            hover.activeImageColor = selectedColor;
            hover.activeTextColor = selectedTextColor;
            hover.hoverScale = 1.035f;
        }

        // Le texte ne doit pas s'étirer avec le bandeau.
        RectTransform label = hover.textTarget.rectTransform;
        if (label.IsChildOf(ribbon.transform)) label.SetParent(hover.transform, true);

        // Pivot à gauche (sans bouger le bandeau) pour qu'il s'allonge vers la droite.
        RectTransform band = ribbon.rectTransform;
        float width = band.rect.width;
        band.anchoredPosition += new Vector2(-band.pivot.x * width * band.localScale.x, 0f);
        band.pivot = new Vector2(0f, band.pivot.y);

        hover.SetTarget(label);
        hover.followers = new[] { band };
        hover.stretchWidth = width;
        hover.hoverOffset = tabHoverOffset;
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
            if (tabs == null || i >= tabs.Length) continue;
            tabs[i].highlighted = selected;
            if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
            {
                if (tabBackgrounds[i] is Image image && orangeRibbon != null && creamRibbon != null)
                    image.sprite = selected ? orangeRibbon : creamRibbon;
                else tabBackgrounds[i].color = selected ? selectedColor : normalColor;
            }
            if (tabShadows != null && i < tabShadows.Length && tabShadows[i] != null)
                tabShadows[i].color = selected ? inkColor : Color.clear;
            if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                tabLabels[i].color = selected ? selectedTextColor : inkColor;
        }
    }

    private void SetCreditsPageMode(bool enabled)
    {
        if (optionsTitle != null)
            optionsTitle.text = enabled ? "CREDITS" : defaultOptionsTitle;
    }

    private void SetSettingsNavigationVisible(bool visible)
    {
        if (tabs == null) return;
        int count = Mathf.Min(3, tabs.Length);
        for (int i = 0; i < count; i++)
        {
            if (tabs[i] != null) tabs[i].gameObject.SetActive(visible);
            if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
                tabBackgrounds[i].gameObject.SetActive(visible);
            if (tabShadows != null && i < tabShadows.Length && tabShadows[i] != null)
                tabShadows[i].gameObject.SetActive(visible);
        }
    }

    private void SetSettingsCreditsTabVisible(bool visible)
    {
        const int creditsTabIndex = 3;
        if (tabs != null && creditsTabIndex < tabs.Length && tabs[creditsTabIndex] != null)
            tabs[creditsTabIndex].gameObject.SetActive(visible);
        if (tabBackgrounds != null && creditsTabIndex < tabBackgrounds.Length &&
            tabBackgrounds[creditsTabIndex] != null)
            tabBackgrounds[creditsTabIndex].gameObject.SetActive(visible);
        if (tabShadows != null && creditsTabIndex < tabShadows.Length &&
            tabShadows[creditsTabIndex] != null)
            tabShadows[creditsTabIndex].gameObject.SetActive(visible);
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

    private void RefreshFullscreenLabel(bool fullscreen)
    {
        if (fullscreenLabel != null) fullscreenLabel.text = fullscreen ? "ON" : "OFF";
    }

    private void OnDisable() => PlayerPrefs.Save();

    public void Play()
    {
        if (launching) return;
        if (nameEntry != null)
        {
            if (IsNameEntryOpen) return;
            IsNameEntryOpen = true;
            CroustyPanelMotion.Hide(mainPanel);
            CroustyPanelMotion.Show(nameEntry.gameObject);
            CroustySoundDesignManager.PlayUI(CroustySound.Paper);
            return;
        }
        LaunchGame();
    }

    public void CloseNameEntry()
    {
        if (!IsNameEntryOpen || nameEntry.IsLaunching) return;
        IsNameEntryOpen = false;
        CroustyPanelMotion.Hide(nameEntry.gameObject);
        CroustyPanelMotion.Show(mainPanel);
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
    }

    private void LaunchGame()
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
