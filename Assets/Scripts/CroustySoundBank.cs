using System;
using UnityEngine;

public enum CroustySound { ButtonPop, Paper, DeliveryCoin, RunComplete, Alarm, Pet, Acceleration, Ambience, Drift, Explosion, Flight, PoliceSiren, Reverse, Horn, FireRing, Engine, ChaseMusic, MenuMusic }

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
    [Header("Moteur")]
    [Range(.3f, 1.5f)] public float engineIdlePitch = .68f;
    [Range(1f, 3f)] public float engineMaxPitch = 1.85f;
    [Header("Sirenes : une seule voix dominante, sur les voitures")]
    [Min(1f)] public float sirenNearDistance = 8f;
    [Min(10f)] public float sirenFarDistance = 150f;
    [Min(.05f)] public float sirenCrossfadeSeconds = .3f;
    [Header("Musique de poursuite")]
    [Min(.1f)] public float chaseIntroSeconds = 1.4f;
    [Min(.1f)] public float chaseFadeOutSeconds = 1.2f;
    public Cue Find(CroustySound sound)
    {
        if (cues != null) foreach (var cue in cues) if (cue.sound == sound) return cue;
        return null;
    }
}
