using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The prefab owns all artwork and layout. This component only drives loading and motion.
public sealed class CroustyLoadingScreen : MonoBehaviour
{
    public CanvasGroup panel;
    public Image fill;
    public TMP_Text status;
    public TMP_Text percentage;
    public GameObject errorReturn;
    [Min(0)] public float minimumSeconds = 5;
    [Min(.1f)] public float fadeSeconds = .8f;
    [Range(1, 4)] public int prewarmRadius = 2;
    [Range(1, 10)] public float generationBudgetMs = 6;
    public string[] messages = { "LOADING...", "CUTTING THE TENDERS...", "SPREADING THE SAUCE...", "PLACING THE CRISPY ONIONS...", "C'EST CHAUD, ON Y VA !" };
    public static bool IsLoading { get; private set; }
    public float Progress { get; private set; }
    public bool Ready { get; private set; }
    float target, began, changed;
    int phase = -1;
    bool ownsLoad, failed;
    Vector3 labelScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => IsLoading = false;

    public void Begin(string scene)
    {
        if (IsLoading) { Destroy(gameObject); return; }
        ownsLoad = IsLoading = true;
        DontDestroyOnLoad(gameObject);
        began = Time.realtimeSinceStartup;
        labelScale = status.transform.localScale;
        panel.alpha = 1;
        panel.blocksRaycasts = true;
        errorReturn.SetActive(false);
        fill.fillAmount = 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Time.timeScale = 0;
        StartCoroutine(Load(scene));
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Time.timeScale = 0;

    IEnumerator Load(string scene)
    {
        // Give the overlay an actual rendered frame before Unity starts loading assets.
        yield return null;
        yield return null;
        AsyncOperation operation = null;
        try { operation = SceneManager.LoadSceneAsync(scene); }
        catch (System.Exception e) { Fail(e.Message); }
        if (operation == null) { Fail("Niveau indisponible"); yield break; }
        while (!operation.isDone)
        { target = .35f * Mathf.Clamp01(operation.progress / .9f); yield return null; }
        Time.timeScale = 0;
        yield return null; // let the scene's Start methods bind their references
        var city = FindAnyObjectByType<ProceduralCity>();
        if (city == null || !city.enabled || city.WorldRoot == null)
        { Fail("La ville n'a pas pu être préparée"); yield break; }
        var preparation = city.Prewarm(prewarmRadius, generationBudgetMs);
        while (true)
        {
            bool next = false;
            try { next = preparation.MoveNext(); }
            catch (System.Exception e) { Fail(e.Message); }
            if (failed || !next) break;
            target = .35f + .6f * city.PrewarmProgress;
            yield return preparation.Current;
        }
        (preparation as System.IDisposable)?.Dispose();
        if (failed) yield break;
        Ready = true;
        target = 1;
        while (Progress < .999f) yield return null;
        Progress = fill.fillAmount = 1;
        yield return new WaitForSecondsRealtime(.25f);
        for (float t = 0; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            panel.alpha = 1 - Mathf.SmoothStep(0, 1, t / fadeSeconds);
            yield return null;
        }
        panel.alpha = 0;
        Release();
        Destroy(gameObject);
    }

    void Update()
    {
        if (!ownsLoad || failed) return;
        float clock = minimumSeconds <= 0 ? 1 : Mathf.Clamp01((Time.realtimeSinceStartup - began) / minimumSeconds);
        float allowed = Mathf.Min(target, clock, Ready ? 1 : .97f);
        Progress = Mathf.MoveTowards(Progress, Mathf.Max(Progress, allowed), Time.unscaledDeltaTime * .45f);
        fill.fillAmount = Progress;
        percentage.text = Mathf.FloorToInt(Progress * 100) + "%";
        int next = Progress >= .999f ? 4 : Progress >= .72f ? 3 : Progress >= .48f ? 2 : Progress >= .25f ? 1 : 0;
        if (phase != next)
        { phase = next; status.text = messages[Mathf.Min(phase, messages.Length - 1)]; changed = Time.unscaledTime; }
        float age = Time.unscaledTime - changed;
        float bounce = Mathf.Sin(age * 17) * Mathf.Exp(-age * 6) * .075f;
        status.transform.localScale = Vector3.Scale(labelScale, new Vector3(1 + bounce, 1 - bounce, 1));
        percentage.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 3.5f) * 3);
    }

    void Fail(string reason)
    {
        failed = true;
        status.text = "OUPS ! RETOUR AU MENU";
        errorReturn.SetActive(true);
        Debug.LogError("Chargement interrompu : " + reason, this);
    }

    public void ReturnToMenu()
    {
        Release();
        SceneManager.LoadSceneAsync("CroustyMainMenu");
        Destroy(gameObject);
    }

    void Release()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (!ownsLoad) return;
        ownsLoad = IsLoading = false;
        Time.timeScale = 1;
    }
    void OnDestroy() => Release();
}
