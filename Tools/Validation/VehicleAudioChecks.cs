// Copy to Assets in a full isolated project; execute VehicleAudioChecks.Run without -quit.
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VehicleAudioChecks : MonoBehaviour
{
    private int failures;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Get(object owner, string name) => owner.GetType().GetField(name, Flags).GetValue(owner);
    private static void Set(object owner, string name, object value) =>
        (owner.GetType().GetField(name, Flags) ?? owner.GetType().GetField("<" + name + ">k__BackingField", Flags)).SetValue(owner, value);
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    private void Check(bool good, string name)
    {
        if (!good) failures++;
        Debug.Log((good ? "VEHICLE_AUDIO_PASS " : "VEHICLE_AUDIO_FAIL ") + name);
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/SceneBase.unity");
        new GameObject("Vehicle audio checks").AddComponent<VehicleAudioChecks>();
        EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        yield return new WaitForSecondsRealtime(2);
        var sound = CroustySoundDesignManager.Instance;
        sound.enabled = false;
        sound.effectsVolume = 1;
        var bus = FindAnyObjectByType<BusController>();
        bus.GetComponent<Rigidbody>().isKinematic = true;
        var police = FindAnyObjectByType<PoliceChaseManager>();
        police.enabled = false;
        Set(sound, "rig", null);
        var engine = (AudioSource)Get(sound, "engine");
        Check(engine.clip != null && engine.clip.samples > 1000 && engine.loop, "recorded engine imported and loops");
        Check(sound.bank.Find(CroustySound.PoliceSiren).clip != null && sound.bank.Find(CroustySound.PoliceSiren).volume < .25f, "new siren and reduced level imported");
        Set(bus, "throttleInput", 0f);
        for (int i = 0; i < 30; i++) Call(sound, "UpdateEngine", true, false, 0f, .05f);
        float idlePitch = engine.pitch, idleVolume = engine.volume;
        Check(engine.isPlaying && idlePitch < 1, "quiet low-pitched idle");
        Set(bus, "throttleInput", 1f);
        for (int i = 0; i < 30; i++) Call(sound, "UpdateEngine", true, false, 7.5f, .05f);
        float loadedPitch = engine.pitch, loadedVolume = engine.volume;
        Check(loadedPitch > idlePitch + .3f && loadedVolume > idleVolume * 2, "pitch and volume rise under throttle/load");
        for (int i = 0; i < 20; i++) Call(sound, "UpdateEngine", true, false, 8.1f, .05f);
        Check(engine.pitch < loadedPitch, "gear shift lowers revs");
        Set(bus, "throttleInput", 0f);
        for (int i = 0; i < 30; i++) Call(sound, "UpdateEngine", true, false, 7.5f, .05f);
        Check(engine.volume < loadedVolume * .6f, "coasting reduces engine load at the same speed");
        Set(bus, "throttleInput", -1f);
        for (int i = 0; i < 30; i++) Call(sound, "UpdateEngine", true, false, 7.5f, .05f);
        Check((float)Get(sound, "engineLoad") < .01f, "braking does not rev the engine");
        for (int i = 0; i < 30; i++) Call(sound, "UpdateEngine", true, false, -5f, .05f);
        Check(engine.volume > idleVolume * 2 && (int)Get(sound, "engineGear") == 0, "reverse has engine load without forward gear shifts");
        Call(sound, "UpdateEngine", true, true, -5f, .05f);
        Check(!engine.isPlaying, "pause silences engine immediately");
        Call(sound, "UpdateEngine", true, false, -5f, .05f);
        Check(engine.isPlaying, "engine resumes after pause");
        sound.effectsVolume = 0;
        Call(sound, "UpdateEngine", true, false, -5f, 2f);
        Check(engine.volume < .0001f, "effects slider mutes engine");
        sound.effectsVolume = 1;
        Call(sound, "UpdateEngine", false, false, 0f, .05f);
        Check(!engine.isPlaying, "engine stops after game over");

        foreach (var car in police.Cars) if (car != null) car.enabled = false;
        var cars = (List<PoliceCarAI>)Get(police, "cars"); cars.Clear();
        Set(police, "chasing", true);
        var listener = FindAnyObjectByType<AudioListener>();
        var origin = listener.transform.position;
        for (int i = 0; i < 6; i++)
        {
            var car = new GameObject("Audio test police " + i).AddComponent<PoliceCarAI>();
            car.transform.position = origin + Vector3.forward * (10 + i * 3);
            Set(car, "State", PoliceCarAI.Mode.Chase);
            cars.Add(car);
        }
        Call(sound, "UpdatePoliceSirens", true, 1f);
        var emitters = cars.Select(c => c.GetComponent<PoliceSirenEmitter>()).ToArray();
        float cap = sound.bank.Find(CroustySound.PoliceSiren).volume;
        Check(emitters.All(e => e.Source.spatialBlend == 1 && e.Source.transform.parent == e.transform), "3D sirens physically attached to car roofs");
        Check(emitters.Count(e => e.Source.isPlaying) == 1 && emitters.Sum(e => e.Source.volume) <= cap, "six nearby cars sound like one siren, with a fixed volume ceiling");
        var leader = (PoliceCarAI)Get(sound, "sirenLeader");
        cars[1].transform.position = origin + Vector3.forward * 9;
        Call(sound, "UpdatePoliceSirens", true, .02f);
        Check((PoliceCarAI)Get(sound, "sirenLeader") == leader, "small distance changes do not chatter between cars");
        leader.transform.position = origin + Vector3.forward * 60;
        bool bounded = true;
        for (int i = 0; i < 20; i++)
        {
            Call(sound, "UpdatePoliceSirens", true, .02f);
            bounded &= emitters.Sum(e => e.Source.volume) <= cap + .00001f;
        }
        Check(bounded && (PoliceCarAI)Get(sound, "sirenLeader") == cars[1], "crossfade follows the closer car without stacking loudness");
        foreach (var car in cars) car.transform.position = origin + Vector3.forward * 100;
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(emitters.Sum(e => e.Source.volume) < cap * .15f, "distant pursuit is much quieter");
        foreach (var car in cars) car.transform.position = origin + Vector3.forward * 180;
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(emitters.All(e => e.Source.volume == 0 && !e.Source.isPlaying), "police outside hearing range are silent");
        cars[0].transform.position = origin;
        Set(cars[0], "State", PoliceCarAI.Mode.Patrol);
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(emitters.All(e => e.Source.volume == 0), "nearby patrol car is silent");
        Set(cars[0], "State", PoliceCarAI.Mode.Roadblock);
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(emitters[0].Source.isPlaying, "active roadblock can carry the siren");
        float fullVolume = emitters[0].Source.volume;
        sound.effectsVolume = .5f;
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(Mathf.Abs(emitters[0].Source.volume - fullVolume * .5f) < .0001f, "effects volume also scales sirens");
        Call(sound, "UpdatePoliceSirens", false, .02f);
        Check(emitters.All(e => !e.Source.isPlaying), "pause or game over silences all police immediately");
        sound.effectsVolume = 1;
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Set(cars[0], "State", PoliceCarAI.Mode.Destroyed);
        cars[0].enabled = false;
        Call(sound, "UpdatePoliceSirens", true, 1f);
        Check(emitters.All(e => e.Source.volume == 0), "destroyed police no longer produce sirens");
        Check(CroustySoundDesignManager.SirenDistanceGain(8, 8, 150) == 1 && CroustySoundDesignManager.SirenDistanceGain(150, 8, 150) == 0, "distance curve reaches full near gain and silence at cutoff");

        sound.enabled = true;
        SceneManager.LoadScene("CroustyMainMenu");
        yield return null; yield return null;
        Check(!engine.isPlaying && engine.volume == 0, "returning to menu clears vehicle audio");
        Debug.Log("VEHICLE_AUDIO_DONE failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif
