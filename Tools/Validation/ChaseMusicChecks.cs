#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ChaseMusicChecks : MonoBehaviour
{
    private int failures;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object owner, string field) => owner.GetType().GetField(field, Flags).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
    private static void Tick(CroustySoundDesignManager sound, bool alive, bool pause, float dt) =>
        typeof(CroustySoundDesignManager).GetMethod("UpdateChaseAudio", Flags).Invoke(sound, new object[] {alive, pause, dt});
    private void Check(bool good, string label) { if (!good) failures++; Debug.Log((good ? "CHASE_PASS " : "CHASE_FAIL ") + label); }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        new GameObject("Chase music checks").AddComponent<ChaseMusicChecks>();
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        var sound = CroustySoundDesignManager.Instance; sound.enabled = false;
        var police = FindAnyObjectByType<PoliceChaseManager>(); police.enabled = false;
        sound.musicVolume = sound.effectsVolume = 1;
        var music = (AudioSource)Get(sound, "chaseMusic");
        var intro = (AudioSource)Get(sound, "chaseIntro");
        Check(music.clip != null && music.loop && music.clip.length > 30, "user track imported and set to loop");
        var samples = new float[music.clip.samples * music.clip.channels];
        Check(music.clip.GetData(samples, 0), "track decoded for level measurement");
        double energy = 0; float peak = 0;
        foreach (float sample in samples) { energy += sample * sample; peak = Mathf.Max(peak, Mathf.Abs(sample)); }
        Debug.Log("CHASE_LEVEL peak=" + peak + " rms=" + System.Math.Sqrt(energy / samples.Length) + " duration=" + music.clip.length);
        Set(police, "chasing", false); Tick(sound, true, false, .1f);
        Check(!music.isPlaying && !intro.isPlaying, "quiet outside pursuit");
        Set(police, "chasing", true); Tick(sound, true, false, .01f);
        Check(intro.isPlaying && !music.isPlaying && intro.time < .2f, "one siren cycle starts before music");
        Tick(sound, true, false, .7f);
        float remaining = (float)Get(sound, "chaseIntroRemaining");
        Tick(sound, true, true, 5);
        Check(!intro.isPlaying && remaining == (float)Get(sound, "chaseIntroRemaining"), "pause freezes intro clock");
        Tick(sound, true, false, .8f);
        Check(music.isPlaying && !intro.isPlaying && Mathf.Abs(music.volume - .55f) < .001f, "music enters at full configured level without fade");
        Tick(sound, true, false, 10);
        Check(!intro.isPlaying && (bool)Get(sound, "chaseMusicStarted"), "no repeated opening siren during pursuit");
        sound.musicVolume = .4f; Tick(sound, true, false, .1f);
        Check(Mathf.Abs(music.volume - .22f) < .001f, "music slider scales track");
        Set(police, "chasing", false); Tick(sound, true, false, .6f);
        Check(music.isPlaying && Mathf.Abs(music.volume - .11f) < .001f, "escape fades music gradually");
        Tick(sound, true, true, 5);
        Check(!music.isPlaying && Mathf.Abs((float)Get(sound, "chaseEnvelope") - .5f) < .001f, "pause freezes exit fade");
        Tick(sound, true, false, .7f);
        Check(!music.isPlaying && music.volume == 0, "music stops after exit fade");
        Set(police, "chasing", true); Tick(sound, true, false, .01f);
        Check(intro.isPlaying && !music.isPlaying, "new pursuit restarts siren then music");
        Set(police, "chasing", false); Tick(sound, true, false, 2);
        Check(!intro.isPlaying && !music.isPlaying, "escape during intro cancels music launch");
        Set(police, "chasing", true); Tick(sound, true, false, .01f); Tick(sound, true, false, 2);
        Tick(sound, false, false, .1f);
        Check(!music.isPlaying && !intro.isPlaying && (float)Get(sound, "chaseEnvelope") == 0, "game over clears chase audio and ducking");
        Debug.Log("CHASE_DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
