using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Plays the menu entrance once, then keeps the prepared background looping.</summary>
public sealed class CroustyMenuVideoBackground : MonoBehaviour
{
    private VideoPlayer entry;
    private VideoPlayer loop;
    private RawImage image;
    private bool entranceFinished;
    private static bool startupPlayed;
    public static bool IsStartupPlaying { get; private set; }
    public static bool IsFadingToMenu { get; private set; }
    private GameObject startupOverlay;
    private EventSystem startupEvents;
    private bool restoreEvents;
    private int lastSkipFrame = -1;
    [Min(5f)] public float idleTrailerSeconds = 60f;
    private float lastActivity;
    private bool sequenceRunning;

    private void OnEnable() => lastActivity = Time.unscaledTime;

    private void Update()
    {
        if (sequenceRunning) return;
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        bool activity = keyboard != null && keyboard.anyKey.isPressed ||
            mouse != null && (mouse.delta.ReadValue().sqrMagnitude > .1f ||
                mouse.scroll.ReadValue().sqrMagnitude > 0 || mouse.leftButton.isPressed || mouse.rightButton.isPressed) ||
            pad != null && (pad.leftStick.ReadValue().sqrMagnitude > .1f ||
                pad.rightStick.ReadValue().sqrMagnitude > .1f);
        if (pad != null)
            foreach (var control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed)
                    activity = true;
        if (activity) lastActivity = Time.unscaledTime;
        if (Time.unscaledTime - lastActivity >= idleTrailerSeconds)
            StartCoroutine(PlayStartup(false));
    }

    private bool SkipPressed()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;
        bool pressed = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame ||
            keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) ||
            pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        if (!pressed || lastSkipFrame == Time.frameCount) return false;
        lastSkipFrame = Time.frameCount;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStartup() { startupPlayed = false; IsStartupPlaying = false; IsFadingToMenu = false; }

    private void Awake()
    {
        bool showStartup = !startupPlayed;
        startupPlayed = true;
        IsStartupPlaying = showStartup;
        // Use the full canvas so the video also fills wide and tall displays.
        var canvas = GetComponentInParent<Canvas>();
        var background = new GameObject("Fond vidéo du menu", typeof(RectTransform), typeof(RawImage));
        background.transform.SetParent(canvas.transform, false);
        background.transform.SetAsFirstSibling();
        var rect = (RectTransform)background.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image = background.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.color = Color.black;
        entry = CreatePlayer(background, "Entry_MainMenu.mp4", false);
        loop = CreatePlayer(background, "Loop.mp4", true);
        entry.prepareCompleted += OnEntryPrepared;
        loop.prepareCompleted += OnLoopPrepared;
        entry.loopPointReached += OnEntranceFinished;
        entry.frameReady += ShowFrame;
        loop.frameReady += ShowFrame;
        entry.Prepare();
        loop.Prepare();
        if (showStartup) StartCoroutine(PlayStartup());
    }

    private IEnumerator PlayStartup(bool includeStudio = true)
    {
        sequenceRunning = true;
        IsStartupPlaying = true;
        IsFadingToMenu = false;
        startupOverlay = new GameObject("Studio and trailer", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var overlayCanvas = startupOverlay.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 32000;
        var group = startupOverlay.GetComponent<CanvasGroup>();
        group.alpha = includeStudio ? 1f : 0f;
        var black = new GameObject("Black background", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(startupOverlay.transform, false);
        var blackRect = (RectTransform)black.transform;
        blackRect.anchorMin = Vector2.zero; blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = blackRect.offsetMax = Vector2.zero;
        black.GetComponent<Image>().color = Color.black;
        var backdrop = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
        backdrop.transform.SetParent(startupOverlay.transform, false);
        var videoImage = backdrop.GetComponent<RawImage>();
        var videoRect = videoImage.rectTransform;
        videoRect.anchorMin = Vector2.zero; videoRect.anchorMax = Vector2.one;
        videoRect.offsetMin = videoRect.offsetMax = Vector2.zero;
        videoImage.color = Color.black;
        startupEvents = EventSystem.current;
        restoreEvents = startupEvents != null && startupEvents.enabled;
        if (startupEvents != null) startupEvents.enabled = false;
        var player = CreatePlayer(backdrop, "intro_studio.mp4", false);
        player.sendFrameReadyEvents = false;
        var audio = backdrop.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.ignoreListenerPause = true;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.controlledAudioTrackCount = 1;
        player.SetTargetAudioSource(0, audio);
        bool ended = false, failed = false;
        player.loopPointReached += _ => ended = true;
        player.errorReceived += (_, message) => { failed = true; Debug.LogWarning("Startup video: " + message); };
        var videos = includeStudio ? new[] { "intro_studio.mp4", "TRAILER 16_9.mp4" }
            : new[] { "TRAILER 16_9.mp4" };
        foreach (string filename in videos)
        {
            bool fadeTrailerIn = includeStudio && filename == "TRAILER 16_9.mp4";
            float trailerFade = fadeTrailerIn ? 0f : 1f;
            ended = failed = false;
            videoImage.texture = null; videoImage.color = Color.black;
            player.Stop();
            player.url = new Uri(Path.Combine(Application.streamingAssetsPath, "MainMenu", filename)).AbsoluteUri;
            player.Prepare();
            float deadline = Time.realtimeSinceStartup + 15f;
            bool skipped = false;
            while (!player.isPrepared && !failed && Time.realtimeSinceStartup < deadline)
            {
                if (SkipPressed()) { skipped = true; break; }
                yield return null;
            }
            if (skipped) { player.Stop(); yield return null; continue; }
            if (!player.isPrepared || failed) continue;
            player.Play();
            deadline = Time.realtimeSinceStartup + Mathf.Max(30f, (float)player.length + 15f);
            while (!ended && !failed && Time.realtimeSinceStartup < deadline)
            {
                if (SkipPressed()) { skipped = true; break; }
                if (player.texture != null)
                {
                    videoImage.texture = player.texture;
                    if (fadeTrailerIn)
                        trailerFade = Mathf.MoveTowards(trailerFade, 1f, Time.unscaledDeltaTime / .6f);
                    videoImage.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, trailerFade));
                    group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / .8f);
                    // Letterbox the full studio/trailer frame rather than stretching it.
                    float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                    float aspect = (float)player.texture.width / Mathf.Max(1, player.texture.height);
                    videoRect.localScale = screenAspect > aspect
                        ? new Vector3(aspect / screenAspect, 1, 1) : new Vector3(1, screenAspect / aspect, 1);
                }
                yield return null;
            }
            player.Pause();
            if (filename == "intro_studio.mp4" && ended && !skipped && !failed)
            {
                // Keep the completed studio logo on screen for 1.5 extra seconds.
                float holdUntil = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < holdUntil)
                {
                    if (SkipPressed()) break;
                    yield return null;
                }
            }
            // Require a new press for the next clip, even when both prepare immediately.
            yield return null;
        }
        // Start the animated menu underneath the final video frame, then crossfade into it.
        IsFadingToMenu = true;
        if (includeStudio && entry.isPrepared) entry.Play();
        float startingAlpha = group.alpha;
        for (float elapsed = 0; elapsed < 1f; elapsed += Time.unscaledDeltaTime)
        {
            group.alpha = startingAlpha * (1f - Mathf.SmoothStep(0, 1, elapsed));
            audio.volume = 1f - Mathf.SmoothStep(0, 1, elapsed);
            yield return null;
        }
        if (restoreEvents && startupEvents != null) startupEvents.enabled = true;
        restoreEvents = false;
        Destroy(startupOverlay);
        IsStartupPlaying = IsFadingToMenu = false;
        lastActivity = Time.unscaledTime;
        sequenceRunning = false;
    }

    private VideoPlayer CreatePlayer(GameObject owner, string filename, bool looping)
    {
        var player = owner.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = new Uri(Path.Combine(Application.streamingAssetsPath, "MainMenu", filename)).AbsoluteUri;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = looping;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.sendFrameReadyEvents = true;
        player.errorReceived += OnVideoError;
        return player;
    }

    private void OnEntryPrepared(VideoPlayer player) { if (!IsStartupPlaying) player.Play(); }

    private void OnLoopPrepared(VideoPlayer player)
    {
        if (entranceFinished) player.Play();
    }

    private void OnEntranceFinished(VideoPlayer player)
    {
        entranceFinished = true;
        if (loop.isPrepared) loop.Play();
    }

    private void ShowFrame(VideoPlayer player, long frame)
    {
        image.texture = player.texture;
        image.color = Color.white;
        player.sendFrameReadyEvents = false;
    }

    private void LateUpdate()
    {
        if (image == null || image.texture == null) return;
        // Fill the canvas without stretching the picture; crop only the excess edges.
        var size = image.rectTransform.rect.size;
        if (size.y <= 0) return;
        float videoAspect = (float)image.texture.width / image.texture.height;
        float screenAspect = size.x / size.y;
        image.uvRect = screenAspect > videoAspect
            ? new Rect(0, (1 - videoAspect / screenAspect) / 2, 1, videoAspect / screenAspect)
            : new Rect((1 - screenAspect / videoAspect) / 2, 0, screenAspect / videoAspect, 1);
    }

    private void OnVideoError(VideoPlayer player, string message)
    {
        Debug.LogError($"Fond vidéo du menu : {message}", this);
        if (player == entry) OnEntranceFinished(player);
    }

    private void OnDestroy()
    {
        IsStartupPlaying = IsFadingToMenu = false;
        if (restoreEvents && startupEvents != null) startupEvents.enabled = true;
        if (startupOverlay != null) Destroy(startupOverlay);
        if (entry != null) entry.Stop();
        if (loop != null) loop.Stop();
        if (image != null) Destroy(image.gameObject);
    }
}
