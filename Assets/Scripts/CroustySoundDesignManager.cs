using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(900), DisallowMultipleComponent]
public sealed class CroustySoundDesignManager : MonoBehaviour
{
    public static CroustySoundDesignManager Instance { get; private set; }
    public CroustySoundBank bank;
    [Range(0,1)] public float effectsVolume = 1f;
    [Range(0,1)] public float musicVolume = 1f;
    private sealed class Voice { public AudioSource source; public double end; public bool ui; public float volume; }
    private Voice[] voices;
    private AudioSource ambience, drift, reverse, siren;
    private BusController bus;
    private Rigidbody body;
    private BusWheelRig rig;
    private SceneBaseDriftSmoke smoke;
    private BusFlightController flight;
    private PoliceChaseManager police;
    private PoliceGameOverManager gameOver;
    private bool paused, accelerating;
    private float nextAcceleration;
    private int lastPopFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSingleton() => Instance = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("Crousty Sound Design Manager").AddComponent<CroustySoundDesignManager>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (bank == null) bank = Resources.Load<CroustySoundBank>("Crousty/SoundBank");
        if (bank != null && bank.cues != null)
            foreach (var cue in bank.cues) if (cue.clip != null && cue.clip.loadType != AudioClipLoadType.Streaming) cue.clip.LoadAudioData();
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("Crousty.MasterVolume",1));
        effectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Crousty.EffectsVolume", 1));
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Crousty.MusicVolume", 1));
        voices = new Voice[10];
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice { source = Source("Cue " + i), ui = i < 3 };
        ambience = Loop("Bus ambience", CroustySound.Ambience);
        drift = Loop("Drift", CroustySound.Drift);
        reverse = Loop("Reverse", CroustySound.Reverse);
        siren = Loop("Police siren", CroustySound.PoliceSiren);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private AudioSource Source(string name)
    {
        var source = new GameObject(name).AddComponent<AudioSource>();
        source.transform.SetParent(transform,false);
        source.playOnAwake = false;
        source.ignoreListenerPause = true;
        source.volume = 0;
        return source;
    }
    private AudioSource Loop(string name, CroustySound sound)
    {
        var source = Source(name);
        source.loop = true;
        var cue = bank != null ? bank.Find(sound) : null;
        if (cue != null) source.clip = cue.clip;
        return source;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bus = FindAnyObjectByType<BusController>();
        body = bus != null ? bus.GetComponent<Rigidbody>() : null;
        rig = bus != null ? bus.GetComponent<BusWheelRig>() : null;
        smoke = bus != null ? bus.GetComponent<SceneBaseDriftSmoke>() : null;
        flight = bus != null ? bus.GetComponent<BusFlightController>() : null;
        police = FindAnyObjectByType<PoliceChaseManager>();
        gameOver = FindAnyObjectByType<PoliceGameOverManager>();
        accelerating = false;
        paused = false;
        foreach (var voice in voices) if (!voice.ui) voice.source.Stop();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<CroustyButtonSound>() == null) button.gameObject.AddComponent<CroustyButtonSound>();
    }
    public static void Play(CroustySound sound, Vector3? position = null)
    {
        if (Instance != null) Instance.PlayCue(sound, false, position);
    }
    public static void PlayUI(CroustySound sound)
    {
        if (Instance != null) Instance.PlayCue(sound, true, null);
    }
    private void PlayCue(CroustySound sound, bool ui, Vector3? position)
    {
        if (!ui && Time.timeScale <= 0f) return;
        var cue = bank != null ? bank.Find(sound) : null;
        if (cue == null || cue.clip == null) return;
        if (sound == CroustySound.ButtonPop)
        {
            if (lastPopFrame == Time.frameCount) return;
            lastPopFrame = Time.frameCount;
        }
        Voice best = null;
        foreach (var voice in voices)
        {
            if (voice.ui != ui) continue;
            if (!voice.source.isPlaying) { best = voice; break; }
            if (best == null || voice.end < best.end) best = voice;
        }
        if (best == null) return;
        best.source.Stop();
        best.source.clip = cue.clip;
        best.volume = cue.volume * effectsVolume;
        best.source.volume = best.volume;
        best.source.pitch = sound == CroustySound.ButtonPop || sound == CroustySound.DeliveryCoin ? .97f + (Time.frameCount % 7) * .01f : 1;
        best.source.spatialBlend = position.HasValue ? .65f : 0;
        best.source.minDistance = 4;
        best.source.maxDistance = 75;
        best.source.transform.position = position ?? Vector3.zero;
        best.source.time = Mathf.Clamp(cue.startTime,0,Mathf.Max(0,cue.clip.length - .01f));
        float duration = cue.maximumDuration > 0 ? Mathf.Min(cue.maximumDuration,cue.clip.length - best.source.time) : cue.clip.length - best.source.time;
        best.end = AudioSettings.dspTime + duration / best.source.pitch;
        best.source.Play();
    }
    private void Update()
    {
        bool stopped = Time.timeScale <= 0;
        foreach (var voice in voices)
        {
            if (!voice.ui && stopped) voice.end += Time.unscaledDeltaTime;
            if (stopped != paused && !voice.ui) { if (stopped) voice.source.Pause(); else voice.source.UnPause(); }
            if (voice.source.isPlaying)
            {
                if (AudioSettings.dspTime >= voice.end) voice.source.Stop();
                else voice.source.volume = voice.volume * Mathf.Clamp01((float)(voice.end - AudioSettings.dspTime) / .035f);
            }
        }
        paused = stopped;
        bool alive = body != null && (gameOver == null || !gameOver.IsGameOver);
        bool driving = alive && !stopped && bus != null && bus.enabled && (flight == null || !flight.IsAirborne);
        float speed = body != null ? Vector3.Dot(body.linearVelocity,body.rotation * Vector3.forward) : 0;
        float driftAmount = smoke != null ? smoke.DriftIntensity : 0;
        UpdateLoop(ambience,CroustySound.Ambience,alive ? (stopped ? .35f : 1f) : 0,true);
        UpdateLoop(drift,CroustySound.Drift,driving ? driftAmount : 0);
        UpdateLoop(reverse,CroustySound.Reverse,driving && speed < -.5f ? 1 : 0);
        UpdateLoop(siren,CroustySound.PoliceSiren,alive && !stopped && police != null && police.ChaseStarted ? 1 : 0);
        bool acceleration = driving && rig != null && rig.ThrottleAmount > .65f && speed > .5f;
        if (acceleration && !accelerating && Time.time >= nextAcceleration)
        {
            Play(CroustySound.Acceleration);
            nextAcceleration = Time.time + 2.5f;
        }
        accelerating = acceleration;
        if (alive && !stopped && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) Play(CroustySound.Horn);
    }
    private void UpdateLoop(AudioSource source, CroustySound sound, float strength, bool music = false)
    {
        var cue = bank != null ? bank.Find(sound) : null;
        float target = cue != null ? cue.volume * Mathf.Clamp01(strength) * (music ? musicVolume : effectsVolume) : 0;
        source.volume = Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime * .8f);
        if (source.volume > .001f && !source.isPlaying && source.clip != null) source.Play();
        if (source.volume <= .001f && source.isPlaying) source.Stop();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
}
