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
    private AudioSource ambience, drift, reverse, engine;
    private AudioSource chaseMusic, chaseIntro;
    private AudioSource menuMusic;
    private AudioSource wings;
    private bool inMainMenu;
    private bool wasChasing, chaseAudioPaused;
    private float chaseIntroRemaining, chaseEnvelope;
    private bool chaseMusicStarted;
    private float GameplayMix => Mathf.Lerp(1f, .65f, chaseEnvelope);
    private AudioLowPassFilter engineFilter;
    private AudioListener listener;
    private PoliceCarAI sirenLeader;
    private readonly System.Collections.Generic.List<PoliceSirenEmitter> sirenEmitters = new System.Collections.Generic.List<PoliceSirenEmitter>();
    private float engineLoad, engineRevs;
    private int engineGear;
    private BusController bus;
    private Rigidbody body;
    private BusWheelRig rig;
    private SceneBaseDriftSmoke smoke;
    private BusFlightController flight;
    private PoliceChaseManager police;
    private PoliceGameOverManager gameOver;
    private bool paused;
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
        wings = Loop("Flight wings", CroustySound.Flight);
        drift = Loop("Drift", CroustySound.Drift);
        reverse = Loop("Reverse", CroustySound.Reverse);
        chaseMusic = Loop("Chase music", CroustySound.ChaseMusic);
        chaseMusic.priority = 30;
        menuMusic = Loop("Main menu music", CroustySound.MenuMusic);
        menuMusic.priority = 30;
        chaseIntro = Loop("Chase opening siren", CroustySound.PoliceSiren);
        chaseIntro.loop = false;
        chaseIntro.priority = 40;
        engine = Loop("Bus engine", CroustySound.Engine);
        engine.spatialBlend = .25f;
        engine.minDistance = 10;
        engine.maxDistance = 55;
        engine.dopplerLevel = 0;
        engine.priority = 70;
        engineFilter = engine.gameObject.AddComponent<AudioLowPassFilter>();
        engineFilter.lowpassResonanceQ = 1;
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
        if (mode == LoadSceneMode.Single)
        {
            inMainMenu = scene.name == "CroustyMainMenu";
            if (!inMainMenu) { menuMusic.Stop(); menuMusic.volume = 0; }
        }
        bus = FindAnyObjectByType<BusController>();
        body = bus != null ? bus.GetComponent<Rigidbody>() : null;
        rig = bus != null ? bus.GetComponent<BusWheelRig>() : null;
        smoke = bus != null ? bus.GetComponent<SceneBaseDriftSmoke>() : null;
        flight = bus != null ? bus.GetComponent<BusFlightController>() : null;
        police = FindAnyObjectByType<PoliceChaseManager>();
        gameOver = FindAnyObjectByType<PoliceGameOverManager>();
        listener = FindAnyObjectByType<AudioListener>();
        sirenLeader = null;
        foreach (var emitter in sirenEmitters)
            if (emitter != null) { emitter.MixWeight = 0; emitter.Source.Stop(); emitter.Source.volume = 0; }
        sirenEmitters.Clear();
        engine.Stop(); engine.volume = 0; engineGear = 0; engineRevs = engineLoad = 0;
        ResetChaseAudio();
        wings.Stop(); wings.volume = 0;
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
        best.volume = cue.volume;
        best.source.volume = best.volume * effectsVolume * (ui ? 1 : GameplayMix);
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
        var menuCue = bank != null ? bank.Find(CroustySound.MenuMusic) : null;
        if (inMainMenu && menuMusic.clip != null)
        {
            menuMusic.volume = (menuCue != null ? menuCue.volume : 0) * musicVolume;
            if (!menuMusic.isPlaying) menuMusic.Play();
        }
        bool stopped = Time.timeScale <= 0;
        foreach (var voice in voices)
        {
            if (!voice.ui && stopped) voice.end += Time.unscaledDeltaTime;
            if (stopped != paused && !voice.ui) { if (stopped) voice.source.Pause(); else voice.source.UnPause(); }
            if (voice.source.isPlaying)
            {
                if (AudioSettings.dspTime >= voice.end) voice.source.Stop();
                else voice.source.volume = voice.volume * effectsVolume * (voice.ui ? 1 : GameplayMix) * Mathf.Clamp01((float)(voice.end - AudioSettings.dspTime) / .035f);
            }
        }
        paused = stopped;
        bool alive = body != null && (gameOver == null || !gameOver.IsGameOver);
        UpdateChaseAudio(alive, stopped, Time.unscaledDeltaTime);
        bool driving = alive && !stopped && bus != null && bus.enabled && (flight == null || !flight.IsAirborne);
        float speed = body != null ? Vector3.Dot(body.linearVelocity,body.rotation * Vector3.forward) : 0;
        float driftAmount = smoke != null ? smoke.DriftIntensity : 0;
        UpdateLoop(ambience,CroustySound.Ambience,alive ? (stopped ? .35f : 1f) * Mathf.Lerp(1, .15f, chaseEnvelope) : 0,true);
        UpdateLoop(drift,CroustySound.Drift,driving ? driftAmount : 0);
        UpdateLoop(reverse,CroustySound.Reverse,driving && speed < -.5f ? 1 : 0);
        if (!alive || flight == null || !flight.isActiveAndEnabled || flight.CurrentMode != BusFlightController.FlightMode.Flying)
        { wings.Stop(); wings.volume = 0; }
        else if (stopped) wings.Pause();
        else
        {
            wings.UnPause();
            UpdateLoop(wings, CroustySound.Flight, 1);
        }
        UpdateEngine(alive, stopped, speed, Time.unscaledDeltaTime);
        UpdatePoliceSirens(alive && !stopped, Time.unscaledDeltaTime);
        if (alive && !stopped && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) Play(CroustySound.Horn);
    }
    private void UpdateEngine(bool alive, bool stopped, float signedSpeed, float dt)
    {
        if (!alive || engine.clip == null) { engine.Stop(); engine.volume = 0; return; }
        if (stopped) { engine.Pause(); return; }
        engine.transform.position = bus.transform.position;
        float speedKmh = Mathf.Abs(signedSpeed) * 3.6f;
        float throttle = rig != null && rig.IsReady ? rig.ThrottleAmount : bus.ThrottleInput;
        bool braking = speedKmh > 3 && throttle * signedSpeed < 0;
        float loadTarget = braking ? 0 : Mathf.Abs(throttle);
        if (bus.PetAccelerationMultiplier > 1) loadTarget = 1;
        if (!bus.enabled || flight != null && flight.IsAirborne) loadTarget *= .2f;
        engineLoad = Mathf.Lerp(engineLoad, loadTarget, 1 - Mathf.Exp(-dt * (loadTarget > engineLoad ? 5 : 9)));

        // Virtual gearbox: hysteresis prevents gear chatter around shift speeds.
        if (signedSpeed < -.5f) engineGear = 0;
        else
        {
            while (engineGear < 4 && speedKmh > (engineGear + 1) * 28f) engineGear++;
            while (engineGear > 0 && speedKmh < engineGear * 28f - 8f) engineGear--;
        }
        float gearSpeed = Mathf.Clamp01((speedKmh - engineGear * 28f + 12f) / 40f);
        float targetRevs = Mathf.Clamp01(gearSpeed * .65f + engineLoad * .35f);
        if (speedKmh < 1) targetRevs = engineLoad * .32f;
        if (flight != null && flight.IsAirborne) targetRevs = .18f;
        engineRevs = Mathf.Lerp(engineRevs, targetRevs, 1 - Mathf.Exp(-dt * 5));
        engine.pitch = Mathf.Lerp(bank.engineIdlePitch, bank.engineMaxPitch, engineRevs);
        var cue = bank.Find(CroustySound.Engine);
        float targetVolume = (cue != null ? cue.volume : 0) * effectsVolume * GameplayMix * Mathf.Lerp(.32f, 1, engineLoad) * Mathf.Lerp(.8f, 1, engineRevs);
        engine.volume = Mathf.Lerp(engine.volume, targetVolume, 1 - Mathf.Exp(-dt * 7));
        engineFilter.cutoffFrequency = Mathf.Lerp(1100, 6500, engineLoad);
        engine.UnPause();
        if (!engine.isPlaying && engine.volume > .001f) engine.Play();
    }

    public static float SirenDistanceGain(float distance, float near, float far)
    {
        float t = Mathf.Clamp01((distance - near) / Mathf.Max(1, far - near));
        return (1 - t) * (1 - t); // Quiet well before the hard cutoff.
    }

    private void UpdatePoliceSirens(bool audible, float dt)
    {
        if (listener == null) listener = FindAnyObjectByType<AudioListener>();
        Vector3 ear = listener != null ? listener.transform.position : bus != null ? bus.transform.position : Vector3.zero;
        var cue = bank != null ? bank.Find(CroustySound.PoliceSiren) : null;
        bool pursuing = audible && police != null && police.ChaseStarted && cue != null && cue.clip != null;
        float near = bank != null ? bank.sirenNearDistance : 8;
        float far = bank != null ? Mathf.Max(near + 1, bank.sirenFarDistance) : 150;
        PoliceCarAI nearest = null;
        float nearestDistance = far;
        if (police != null)
            foreach (var car in police.Cars)
            {
                if (car == null) continue;
                var emitter = car.GetComponent<PoliceSirenEmitter>();
                if (emitter == null) emitter = car.gameObject.AddComponent<PoliceSirenEmitter>();
                if (!sirenEmitters.Contains(emitter)) sirenEmitters.Add(emitter);
                if (!pursuing || !car.isActiveAndEnabled || car.IsDestroyed || !(car.InPursuit || car.IsRoadblock)) continue;
                float distance = Vector3.Distance(ear, emitter.Source.transform.position);
                if (distance < nearestDistance) { nearest = car; nearestDistance = distance; }
            }
        bool keepLeader = pursuing && sirenLeader != null && sirenLeader.isActiveAndEnabled && !sirenLeader.IsDestroyed &&
            (sirenLeader.InPursuit || sirenLeader.IsRoadblock);
        if (keepLeader)
        {
            float distance = Vector3.Distance(ear, sirenLeader.transform.position + Vector3.up * 1.6f);
            keepLeader = distance < far && distance <= nearestDistance * 1.15f + 5;
        }
        if (!keepLeader) sirenLeader = nearest;
        float totalWeight = 0;
        for (int i = sirenEmitters.Count - 1; i >= 0; i--)
        {
            var emitter = sirenEmitters[i];
            if (emitter == null) { sirenEmitters.RemoveAt(i); continue; }
            bool selected = pursuing && emitter.isActiveAndEnabled && sirenLeader != null && emitter.gameObject == sirenLeader.gameObject;
            emitter.MixWeight = Mathf.MoveTowards(emitter.MixWeight, selected ? 1 : 0, dt / Mathf.Max(.05f, bank != null ? bank.sirenCrossfadeSeconds : .3f));
            totalWeight += emitter.MixWeight;
        }
        foreach (var emitter in sirenEmitters)
        {
            var source = emitter.Source;
            float distance = Vector3.Distance(ear, source.transform.position);
            float volume = pursuing ? cue.volume * effectsVolume * SirenDistanceGain(distance, near, far) * emitter.MixWeight / Mathf.Max(1, totalWeight) : 0;
            source.volume = volume * (chaseMusicStarted ? .35f : 1f);
            if (volume <= .0001f) { source.Pause(); continue; }
            if (source.clip != cue.clip) source.clip = cue.clip;
            if (!source.isPlaying)
            {
                // Every handover enters the same place in the two-tone cycle.
                source.timeSamples = (int)(AudioSettings.dspTime * cue.clip.frequency % cue.clip.samples);
                source.Play();
            }
        }
    }
    private void UpdateLoop(AudioSource source, CroustySound sound, float strength, bool music = false)
    {
        var cue = bank != null ? bank.Find(sound) : null;
        float target = cue != null ? cue.volume * Mathf.Clamp01(strength) * (music ? musicVolume : effectsVolume * GameplayMix) : 0;
        source.volume = Mathf.MoveTowards(source.volume,target,Time.unscaledDeltaTime * .8f);
        if (source.volume > .001f && !source.isPlaying && source.clip != null) source.Play();
        if (source.volume <= .001f && source.isPlaying) source.Stop();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void ResetChaseAudio()
    {
        chaseMusic.Stop(); chaseIntro.Stop();
        chaseMusic.volume = chaseIntro.volume = 0;
        wasChasing = chaseAudioPaused = chaseMusicStarted = false;
        chaseIntroRemaining = chaseEnvelope = 0;
    }

    private void UpdateChaseAudio(bool alive, bool stopped, float dt)
    {
        if (!alive || chaseMusic.clip == null) { ResetChaseAudio(); return; }
        // Pause freezes both the opening cue and the exit fade, including their clocks.
        if (stopped)
        {
            if (!chaseAudioPaused) { chaseMusic.Pause(); chaseIntro.Pause(); chaseAudioPaused = true; }
            return;
        }
        if (chaseAudioPaused) { chaseMusic.UnPause(); chaseIntro.UnPause(); chaseAudioPaused = false; }
        bool chasing = police != null && police.ChaseStarted;
        if (chasing && !wasChasing)
        {
            chaseMusic.Stop(); chaseMusicStarted = false;
            chaseIntro.Stop(); chaseEnvelope = 1;
            chaseIntroRemaining = chaseIntro.clip != null ? Mathf.Min(bank.chaseIntroSeconds, chaseIntro.clip.length) : 0;
            // The fleet plays the introductory siren from the actual cars.
            // Keep this timer for the music entrance without a second 2D siren.
        }
        else if (chasing && chaseIntroRemaining > 0)
            chaseIntroRemaining = Mathf.Max(0, chaseIntroRemaining - dt);

        if (chasing)
        {
            chaseEnvelope = 1;
            if (chaseIntroRemaining <= 0 && !chaseMusicStarted)
            {
                chaseIntro.Stop(); chaseMusic.time = 0;
                chaseMusic.volume = bank.Find(CroustySound.ChaseMusic).volume * musicVolume;
                chaseMusic.Play(); chaseMusicStarted = true; // Immediate full level: no entry fade.
            }
        }
        else
        {
            chaseIntroRemaining = 0;
            chaseEnvelope = Mathf.MoveTowards(chaseEnvelope, 0, dt / Mathf.Max(.1f, bank.chaseFadeOutSeconds));
            if (chaseEnvelope <= 0) { chaseMusic.Stop(); chaseIntro.Stop(); chaseMusicStarted = false; }
        }
        chaseMusic.volume = bank.Find(CroustySound.ChaseMusic).volume * musicVolume * chaseEnvelope;
        var sirenCue = bank.Find(CroustySound.PoliceSiren);
        chaseIntro.volume = (sirenCue != null ? sirenCue.volume : 0) * effectsVolume * chaseEnvelope;
        wasChasing = chasing;
    }
}
