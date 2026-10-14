using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Input and navigation only: the artwork and animation clips are authored in the scene.
public sealed class CroustyNameEntry : MonoBehaviour
{
    public CroustyMenu menu;
    public TMP_InputField nameInput;
    public Button playButton;
    public TMP_Text hint;
    public CanvasGroup controls;
    public Animator departure;
    public CroustyLoadingScreen loadingScreen;
    [Min(.1f)] public float departureSeconds = .8f;
    [Min(0f)] public float departureHoldSeconds = .65f;
    public bool IsLaunching { get; private set; }
    static readonly int Launch = Animator.StringToHash("Launch");

    private void OnEnable()
    {
        IsLaunching = false;
        controls.interactable = controls.blocksRaycasts = true;
        nameInput.SetTextWithoutNotify(CroustyPlayerProfile.StoredName);
        if (departure != null) { departure.Rebind(); departure.Update(0); }
        Refresh(nameInput.text);
        StartCoroutine(FocusAfterEntrance());
    }

    private IEnumerator FocusAfterEntrance()
    {
        yield return new WaitForSecondsRealtime(.55f);
        if (IsLaunching) yield break;
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    public void Refresh(string value)
    {
        bool valid = CroustyPlayerProfile.Normalize(value).Length > 0;
        playButton.interactable = valid && !IsLaunching;
        hint.text = valid ? "READY TO FIRE UP THE ENGINE?" : "ENTER YOUR NAME TO GET BEHIND THE WHEEL";
    }

    public void Submit(string value) => ConfirmPlay();

    public void ConfirmPlay()
    {
        if (IsLaunching || !isActiveAndEnabled || !menu.IsNameEntryOpen) return;
        string playerName = CroustyPlayerProfile.Normalize(nameInput.text);
        if (playerName.Length == 0)
        {
            Refresh(nameInput.text);
            nameInput.ActivateInputField();
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded("SceneBase"))
        {
            hint.text = "THE GAME LEVEL IS UNAVAILABLE";
            return;
        }
        nameInput.SetTextWithoutNotify(playerName);
        nameInput.DeactivateInputField();
        CroustyPlayerProfile.Save(playerName);
        IsLaunching = true;
        controls.interactable = controls.blocksRaycasts = false;
        playButton.interactable = false;
        hint.text = "LET'S GO!";
        CroustySoundDesignManager.PlayUI(CroustySound.Paper);
        if (departure != null) departure.SetTrigger(Launch);
        StartCoroutine(Depart());
    }

    private IEnumerator Depart()
    {
        // Close the authored wipe before handing over to the persistent loading panel.
        Time.timeScale = 1;
        float elapsed = 0;
        while (elapsed < departureSeconds || departure != null &&
            (!departure.GetCurrentAnimatorStateInfo(0).IsName("Départ") ||
             departure.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        // Leave the completed title readable before the loading screen covers it.
        yield return new WaitForSecondsRealtime(departureHoldSeconds);
        if (loadingScreen != null) Instantiate(loadingScreen).Begin("SceneBase");
        else SceneManager.LoadSceneAsync("SceneBase");
    }

    public void Back()
    {
        if (!IsLaunching) menu.CloseNameEntry();
    }
}

public static class CroustyPlayerProfile
{
    public const string NameKey = "Crousty.PlayerName";
    public const int MaximumNameLength = 20;
    public static string StoredName => Normalize(PlayerPrefs.GetString(NameKey, ""));
    public static string PlayerName => StoredName.Length > 0 ? StoredName : "You";

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var result = new System.Text.StringBuilder();
        bool space = false;
        foreach (char c in value.Trim())
        {
            if (char.IsWhiteSpace(c)) { space = result.Length > 0; continue; }
            if (char.IsControl(c)) continue;
            if (space && result.Length < MaximumNameLength) result.Append(' ');
            space = false;
            if (result.Length >= MaximumNameLength) break;
            result.Append(c);
        }
        // TMP's editable field accepts UTF-16. Never leave a half surrogate on truncation.
        if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1])) result.Length--;
        return result.ToString().TrimEnd();
    }

    public static void Save(string value)
    {
        string name = Normalize(value);
        if (name.Length == 0) return;
        PlayerPrefs.SetString(NameKey, name);
        PlayerPrefs.Save();
    }
}
