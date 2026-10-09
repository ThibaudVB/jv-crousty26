using UnityEngine;

namespace V1
{
    // Sons générés par code : pas besoin de fichiers audio dans le projet.
    // Chaque son n'est créé qu'une fois puis gardé en cache.
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static AudioClip horn;
        private static AudioClip smallHorn;
        private static AudioClip screech;
        private static AudioClip whoosh;

        // Klaxon de bus : deux notes graves mélangées, en boucle
        public static AudioClip Horn => horn != null ? horn : horn = CreateHorn("Klaxon bus", 250f, 315f);

        // Klaxon de voiture, plus aigu
        public static AudioClip SmallHorn => smallHorn != null ? smallHorn : smallHorn = CreateHorn("Klaxon voiture", 420f, 525f);

        // Crissement de pneus (bruit filtré, en boucle)
        public static AudioClip Screech => screech != null ? screech : screech = CreateScreech();

        // Souffle court pour le boost
        public static AudioClip Whoosh => whoosh != null ? whoosh : whoosh = CreateWhoosh();

        // Crée une source audio réglée pour un son en 3D
        public static AudioSource CreateSource(GameObject owner, AudioClip clip, bool loop, float volume,
            float minDistance = 8f, float maxDistance = 120f)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0.3f;
            return source;
        }

        private static AudioClip CreateHorn(string name, float lowHz, float highHz)
        {
            // 1 seconde avec des fréquences entières : la boucle ne claque pas
            float[] data = new float[SampleRate];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float wave =
                    Mathf.Sin(2f * Mathf.PI * lowHz * t) +
                    Mathf.Sin(2f * Mathf.PI * highHz * t) +
                    0.35f * Mathf.Sin(2f * Mathf.PI * lowHz * 2f * t) +
                    0.25f * Mathf.Sin(2f * Mathf.PI * highHz * 3f * t);

                // Saturation douce : son plus "cuivré"
                data[i] = SoftClip(wave * 0.9f) * 0.6f;
            }
            return Build(name, data);
        }

        private static AudioClip CreateScreech()
        {
            float[] data = new float[SampleRate];
            var random = new System.Random(7);
            float low = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Bruit filtré + sifflement aigu qui oscille
                low = Mathf.Lerp(low, noise, 0.35f);
                float whistle = Mathf.Sin(2f * Mathf.PI * (1800f + 120f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * t);
                data[i] = (low * 0.6f + whistle * 0.25f) * 0.6f;
            }

            // Fondu sur les bords pour que la boucle ne claque pas
            Fade(data, 400);
            return Build("Crissement", data);
        }

        private static AudioClip CreateWhoosh()
        {
            int length = SampleRate * 6 / 10;
            float[] data = new float[length];
            var random = new System.Random(3);
            float low = 0f;
            for (int i = 0; i < length; i++)
            {
                float progress = (float)i / length;
                float envelope = Mathf.Sin(Mathf.PI * progress) * (1f - progress * 0.5f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Filtre qui s'ouvre : le souffle devient plus aigu
                low = Mathf.Lerp(low, noise, Mathf.Lerp(0.05f, 0.5f, progress));
                data[i] = low * envelope * 0.9f;
            }
            return Build("Boost", data);
        }

        private static float SoftClip(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }

        private static void Fade(float[] data, int samples)
        {
            for (int i = 0; i < samples && i < data.Length; i++)
            {
                float k = (float)i / samples;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        private static AudioClip Build(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
