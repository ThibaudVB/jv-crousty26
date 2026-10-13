using System;
using UnityEngine;

public enum CroustySound { ButtonPop, Paper, DeliveryCoin, RunComplete, Alarm, Pet, Acceleration, Ambience, Drift, Explosion, Flight, PoliceSiren, Reverse, Horn }

[CreateAssetMenu(menuName = "Crousty/Sound bank")]
public sealed class CroustySoundBank : ScriptableObject
{
    [Serializable]
    public sealed class Cue
    {
        public CroustySound sound;
        public AudioClip clip;
        [Range(0,1)] public float volume = .6f;
        [Min(0)] public float maximumDuration;
        [Min(0)] public float startTime;
    }
    public Cue[] cues;
    public Cue Find(CroustySound sound)
    {
        if (cues != null) foreach (var cue in cues) if (cue.sound == sound) return cue;
        return null;
    }
}
