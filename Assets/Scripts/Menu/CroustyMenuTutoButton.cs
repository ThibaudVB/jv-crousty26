using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class CroustyMenuTutoButton : MonoBehaviour
{
    public const string TutorialEnabledKey = "Crousty.TutorialEnabled";

    private static readonly Color InactiveColor = new Color32(255, 255, 255, 255);
    private static readonly Color ActiveColor = new Color32(233, 132, 36, 255);

    private Button button;
    private Graphic targetGraphic;
    public static bool IsTutorialActive { get; private set; }

    private void Awake()
    {
        IsTutorialActive = PlayerPrefs.GetInt(TutorialEnabledKey, 0) == 1;
        button = GetComponent<Button>();
        targetGraphic = button.targetGraphic;
        if (targetGraphic == null)
        {
            Debug.LogError("Le bouton Tutoriel doit avoir une image cible.", this);
            enabled = false;
            return;
        }

        CroustyMenuHover hover = GetComponent<CroustyMenuHover>();
        if (hover != null && hover.ribbonImage == targetGraphic)
        {
            hover.ribbonImage.sprite = hover.idleRibbon;
            hover.ribbonImage = null;
        }

        ApplyColor();
    }

    private void OnEnable()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.AddListener(ToggleColor);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(ToggleColor);
    }

    public static void SaveSelection()
    {
        PlayerPrefs.SetInt(TutorialEnabledKey, IsTutorialActive ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ToggleColor()
    {
        IsTutorialActive = !IsTutorialActive;
        ApplyColor();
    }

    private void ApplyColor()
    {
        targetGraphic.color = IsTutorialActive ? ActiveColor : InactiveColor;
    }
}
