using UnityEngine;

// A real positional source on each vehicle. The sound manager gives the whole fleet
// one shared volume budget, rather than stacking a full-volume siren on every car.
[DisallowMultipleComponent]
public sealed class PoliceSirenEmitter : MonoBehaviour
{
    public AudioSource Source { get; private set; }
    public float MixWeight { get; set; }

    private void Awake()
    {
        var roof = new GameObject("Police siren - roof");
        roof.transform.SetParent(transform, false);
        roof.transform.localPosition = new Vector3(0, 1.6f, 0);
        Source = roof.AddComponent<AudioSource>();
        Source.playOnAwake = false;
        Source.loop = true;
        Source.volume = 0;
        Source.spatialBlend = 1;
        Source.dopplerLevel = 0; // Keep the common siren phase stable during handovers.
        Source.spread = 35;
        Source.priority = 90;
        Source.ignoreListenerPause = true;
        // Distance gain is calculated once by the fleet mixer against the listener.
        Source.rolloffMode = AudioRolloffMode.Custom;
        Source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
        var filter = roof.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = 3800;
        filter.lowpassResonanceQ = 1;
    }

    private void OnDisable()
    {
        MixWeight = 0;
        if (Source != null) { Source.volume = 0; Source.Stop(); }
    }
}
