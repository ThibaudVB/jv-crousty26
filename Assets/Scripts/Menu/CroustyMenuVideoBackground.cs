using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Plays the menu entrance once, then keeps the prepared background looping.</summary>
public sealed class CroustyMenuVideoBackground : MonoBehaviour
{
    private VideoPlayer entry;
    private VideoPlayer loop;
    private RawImage image;
    private bool entranceFinished;

    private void Awake()
    {
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

    private void OnEntryPrepared(VideoPlayer player) => player.Play();

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
        if (entry != null) entry.Stop();
        if (loop != null) loop.Stop();
        if (image != null) Destroy(image.gameObject);
    }
}
