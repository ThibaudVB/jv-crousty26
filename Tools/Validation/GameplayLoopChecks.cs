using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class GameplayLoopChecks : MonoBehaviour
{
    public CroustyHUD hud;
    public Canvas canvas;
    public PoliceGameOverManager defeat;
    public GameObject defeatPanel;
    public CroustyMenu menu;
    int passed, originalBest, authoredHudObjectCount;
    string originalLeaderboard;
    bool hadLeaderboard;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void Awake() { authoredHudObjectCount = canvas.GetComponentsInChildren<Transform>(true).Length; }
    void Check(bool value,string message)
    {
        if (!value) { File.WriteAllText("gameplay-result.txt","FAILED "+message); Debug.LogError(message); RestoreBest(); EditorApplication.Exit(1); throw new Exception(message); }
        passed++;
        Debug.Log("GAMEPLAY PASS: " + message);
    }
    void Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
    double PaperEnd()
    {
        var paper = CroustySoundDesignManager.Instance.bank.Find(CroustySound.Paper).clip;
        var voices = (Array)typeof(CroustySoundDesignManager).GetField("voices",Private).GetValue(CroustySoundDesignManager.Instance);
        double end = 0;
        foreach (var voice in voices)
        {
            var type = voice.GetType();
            if (((AudioSource)type.GetField("source").GetValue(voice)).clip == paper)
                end = Math.Max(end,(double)type.GetField("end").GetValue(voice));
        }
        return end;
    }
    void RestoreBest()
    {
        PlayerPrefs.SetInt("Crousty.BestScore",originalBest);
        if(hadLeaderboard)PlayerPrefs.SetString(CroustyLeaderboardScores.StorageKey,originalLeaderboard);
        else PlayerPrefs.DeleteKey(CroustyLeaderboardScores.StorageKey);
        PlayerPrefs.Save();
    }
    IEnumerator Start()
    {
        originalBest = PlayerPrefs.GetInt("Crousty.BestScore",0);
        hadLeaderboard=PlayerPrefs.HasKey(CroustyLeaderboardScores.StorageKey);
        originalLeaderboard=PlayerPrefs.GetString(CroustyLeaderboardScores.StorageKey);
        yield return null;
        Check(canvas.GetComponentsInChildren<Transform>(true).Length == authoredHudObjectCount,"starting the HUD does not generate UI objects");
        var hudFields = new SerializedObject(hud);
        foreach (string field in new[] { "timeValue", "timerValue", "scoreValue", "scoreGain" })
        {
            var text = hudFields.FindProperty(field).objectReferenceValue as TMP_Text;
            Check(text != null && text.transform.IsChildOf(canvas.transform),field + " references an authored Canvas text");
        }
        var city = FindAnyObjectByType<ProceduralCity>();
        city.enabled = false;
        var manager = city.deliveries;
        manager.enabled = false;
        var bank = Resources.Load<CroustySoundBank>("Crousty/SoundBank");
        Check(bank != null && bank.cues.Length == 14 && bank.cues.All(c => c.clip != null),"all fourteen supplied clips are assigned in the bank");
        var soundAnalysis = new System.Text.StringBuilder();
        float audioDeadline = Time.realtimeSinceStartup + 2;
        while (bank.cues.Any(c => c.clip.loadType == AudioClipLoadType.DecompressOnLoad && c.clip.loadState != AudioDataLoadState.Loaded) && Time.realtimeSinceStartup < audioDeadline) yield return null;
        foreach (var cue in bank.cues)
        {
            if (cue.clip.loadType != AudioClipLoadType.DecompressOnLoad) continue;
            var samples = new float[Mathf.Min(cue.clip.samples,cue.clip.frequency * 3) * cue.clip.channels];
            if (!cue.clip.GetData(samples,0)) continue;
            int first = System.Array.FindIndex(samples,v => Mathf.Abs(v) > .015f);
            soundAnalysis.AppendLine(cue.sound + " length=" + cue.clip.length + " onset=" + (float)Mathf.Max(0,first) / cue.clip.frequency / cue.clip.channels);
        }
        File.WriteAllText("sound-analysis.txt",soundAnalysis.ToString());
        Check(CroustySoundDesignManager.Instance != null,"persistent sound manager starts automatically");
        Check(CroustySoundDesignManager.Instance.GetComponentsInChildren<AudioSource>().Length == 14,"sound sources use a fixed pool");
        var stops = city.WorldRoot.GetComponentsInChildren<MapImpactProp>().Where(p => p.name == "Panneau STOP").ToArray();
        Check(stops.Length > 0 && stops.All(s => s.transform.Find("STOP - Poly by Google") != null),"every STOP uses the supplied GLB");
        var stopRenderer = stops[0].GetComponentInChildren<MeshRenderer>();
        Check(Mathf.Abs(stopRenderer.bounds.size.y - 3.4f) < .05f,"imported STOP has a consistent real-world height");
        Check(stops[0].Body.isKinematic,"imported STOP preserves impact physics");
        double paperEnd = PaperEnd();
        menu.OpenOptions();
        Check(PaperEnd() > paperEnd,"opening options plays the paper transition");
        Check(menu.IsOptionsOpen,"options navigation changes logical state immediately");
        yield return new WaitForSecondsRealtime(.4f);
        Check(menu.optionsPanel.activeSelf && !menu.mainPanel.activeSelf,"panel transition completes without leaving the previous menu active");
        Vector3 authored = menu.optionsPanel.transform.localScale;
        Time.timeScale = 0;
        paperEnd = PaperEnd();
        menu.SelectCategory(1);
        Check(PaperEnd() == paperEnd,"changing settings category does not play paper");
        yield return new WaitForSecondsRealtime(.4f);
        Check(menu.categoryPanels[1].activeSelf && !menu.categoryPanels[0].activeSelf,"settings tabs animate while game time is paused");
        menu.CloseOptions();
        Check(PaperEnd() > paperEnd,"returning from options plays the paper transition");
        yield return new WaitForSecondsRealtime(.4f);
        Check(!menu.IsOptionsOpen && menu.mainPanel.activeSelf && !menu.optionsPanel.activeSelf,"back navigation completes in pause");
        Check(menu.optionsPanel.transform.localScale == authored,"menu restores its authored scale");
        paperEnd = PaperEnd();
        menu.CloseOptions();
        Check(PaperEnd() == paperEnd,"closing already closed options does not play paper");
        Time.timeScale = 1;
        menu.mainPanel.SetActive(false);
        var far = new GameObject("Distant test address").AddComponent<DeliveryPoint>();
        far.transform.position = city.Origin + new Vector3(9000,0,9000);
        manager.RegisterPoint(far);
        Call(manager,"LoadOrders");
        Check(manager.RunActive && manager.OrdersTotal == 6 && manager.CroustyLeft == 6,"pickup starts a six-order ninety-second sprint");
        Check(Mathf.Abs(manager.RunRemaining - 90) < .001f,"deadline starts at pickup");
        Check(!far.IsWaiting,"retained distant addresses are excluded from the local sprint");
        Time.timeScale = 0;
        manager.enabled = true;
        yield return new WaitForSecondsRealtime(.08f);
        Check(manager.RunElapsed == 0,"pause preserves the delivery deadline");
        manager.enabled = false;
        Time.timeScale = 1;
        Call(manager,"AdvanceRun",30f);
        var orders = manager.deliveryPoints.Where(p => p.IsWaiting).ToArray();
        manager.throwDuration = .04f;
        for (int i = 0; i < orders.Length; i++)
        {
            Call(manager,"ThrowCrousty",orders[i]);
            float deadline = Time.realtimeSinceStartup + 3;
            while (manager.Delivered <= i && Time.realtimeSinceStartup < deadline) yield return null;
            Check(manager.Delivered == i + 1,"projectile arrival credits delivery " + (i + 1));
        }
        Check(manager.Score == 1700 && manager.LastRunScore == 1700,"six deliveries plus completion and time bonus give the expected score");
        Check(manager.AllDelivered && !manager.RunActive,"sixth arrival stops the deadline and completes the round");
        int score = manager.Score;
        Call(manager,"AdvanceRun",200f);
        Check(!manager.HasFailed && manager.Score == score,"completed round cannot subsequently time out or award twice");
        yield return new WaitForSecondsRealtime(1.5f);
        Check(canvas.transform.Find("Delivery countdown").GetComponentInChildren<TMP_Text>().text.Contains("RUN COMPLETE"),"completed run label is in English");
        Check(canvas.transform.Find("Carte score") != null && canvas.transform.Find("Delivery countdown") != null,"score and countdown integrate with the existing HUD");
        var scoreLabel = canvas.transform.Find("Carte score").GetComponentsInChildren<TMP_Text>().First(t => t.name == "Valeur score" || t.name == "Progression livraisons");
        Check(scoreLabel.text == "1700","animated score settles on the exact total");
        Call(manager,"LoadOrders");
        Check(manager.RunActive && manager.Score == score && manager.LastRunScore == 0,"next pickup starts a new round while keeping the session score");
        Call(manager,"AdvanceRun",30f);
        yield return null;
        CapturePreview(city,stops[0]);
        Check(canvas.transform.Find("Delivery countdown").GetComponentInChildren<TMP_Text>().text.Contains("TIME LEFT"),"active countdown label is in English");
        Check(canvas.transform.Find("Time of day").GetComponentInChildren<TMP_Text>().text.Contains("DAY"),"clock phase is in English");
        Call(manager,"AdvanceRun",59.98f);
        var late = manager.deliveryPoints.First(p => p.IsWaiting);
        manager.throwDuration = .3f;
        Call(manager,"ThrowCrousty",late);
        Call(manager,"AdvanceRun",.05f);
        Check(manager.HasFailed && defeat.IsGameOver && Time.timeScale == 0,"missed deadline uses the existing defeat flow");
        Check(defeatPanel.activeSelf && defeatPanel.GetComponentInChildren<TMP_Text>().text == "OUT OF TIME!","timeout displays its English reason on the existing death panel");
        yield return new WaitForSecondsRealtime(.4f);
        Check(defeatPanel.GetComponent<CanvasGroup>().alpha == 1,"death panel finishes its animation while time is frozen");
        Time.timeScale = 1;
        yield return new WaitForSecondsRealtime(.4f);
        Check(manager.Delivered == 0 && manager.Score == score,"late airborne parcel cannot award points after defeat");
        Call(manager,"LoadOrders");
        Check(manager.HasFailed && !manager.RunActive,"failed round cannot silently restart");
        RestoreBest();
        File.WriteAllText("gameplay-result.txt","PASSED " + passed + " checks");
        EditorApplication.Exit(0);
    }
    void CapturePreview(ProceduralCity city,MapImpactProp stop)
    {
        var stopRenderer=stop.GetComponentInChildren<MeshRenderer>();
        foreach (var renderer in FindObjectsByType<Renderer>()) foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || !material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
            Color color=material.color;material.shader=Shader.Find("Standard");material.color=color;
        }
        var cycle=CroustyDayNightCycle.Instance;cycle.enabled=false;cycle.SetHour(12);
        Call(hud,"UpdateClock");Call(hud,"UpdateRunHUD");
        var camera = new GameObject("Loop preview").AddComponent<Camera>();
        camera.transform.position = city.Origin + new Vector3(0,3.5f,-10);
        camera.transform.LookAt(city.Origin + new Vector3(0,2,60));
        camera.clearFlags = CameraClearFlags.Skybox;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var rt = new RenderTexture(1920,1080,24);
        camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        File.WriteAllBytes("loop-hud.png",image.EncodeToPNG());
        camera.transform.position = stopRenderer.bounds.center + stop.transform.forward * 4 + Vector3.up;
        camera.transform.LookAt(stopRenderer.bounds.center);
        canvas.enabled = false;
        camera.Render(); image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        File.WriteAllBytes("stop-poly.png",image.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;Destroy(rt);Destroy(image);canvas.enabled=true;Destroy(camera.gameObject);
    }
}
