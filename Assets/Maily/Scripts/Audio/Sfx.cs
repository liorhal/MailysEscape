using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>Static gain layer (headless audio floor): every source multiplies through these.
    /// Settings sliders bind here. Upgrade path: an AudioMixer with exposed MusicVol / SFXVol.</summary>
    public static class AudioGain
    {
        public static float Music = 0.8f, Sfx = 1f;
        public static void Load() { Music = PlayerPrefs.GetFloat("MusicVol", 0.8f); Sfx = PlayerPrefs.GetFloat("SFXVol", 1f); }
        public static void Save() { PlayerPrefs.SetFloat("MusicVol", Music); PlayerPrefs.SetFloat("SFXVol", Sfx); PlayerPrefs.Save(); }
    }

    /// <summary>Synthesized sound bank + pooled players. All clips are generated at startup — no assets.</summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        public AudioClip Pickup, Coin, Sting, Bark, Yip, Squawk, Click, Ambient, Music, Hop, Heart;
        public AudioClip Growl, Hit, HitHeavy, Block, Whoosh, Gong;
        public AudioClip Honk, Beep, Card, Chomp, Cluck, Thud, Match, Page, Shot, Siren;
        AudioSource _music, _ambient;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        float _duck = 1f;

        const int SR = 22050;

        void Awake()
        {
            I = this;
            AudioGain.Load();
            var rnd = new System.Random(3);
            Pickup = Make("Pickup", 0.12f, (t, d) => Sine(Mathf.Lerp(880, 1320, Mathf.Clamp01(t / 0.09f)), t) * Decay(t, 0.12f));
            Coin = Make("Coin", 0.35f, (t, d) => (t < 0.08f ? Sq(988, t) * 0.5f : Sine(1319, t)) * Decay(t, 0.35f) * 0.7f);
            Sting = Make("Sting", 0.6f, (t, d) =>
            {
                float s = 0; float[] f = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < f.Length; i++) { float t0 = i * 0.11f; if (t > t0) s += (Sine(f[i], t) + Sq(f[i], t) * 0.2f) * Decay(t - t0, 0.45f) * 0.35f; }
                return s;
            });
            Bark = Make("Bark", 0.42f, (t, d) => Woof(t, 0f, 520f, rnd) + Woof(t, 0.2f, 470f, rnd) * 0.8f);
            Yip = Make("Yip", 0.22f, (t, d) => Woof(t, 0f, 900f, rnd));
            Squawk = Make("Squawk", 0.32f, (t, d) =>
            {
                float vib = Mathf.Sin(t * 2 * Mathf.PI * 28f) * 300f;
                float f = 2300f + vib - t * 1800f;
                return (Saw(f, t) * 0.5f + Sine(f * 1.5f, t) * 0.3f + Noise(rnd) * 0.15f) * Env(t, 0.32f, 0.01f) * 0.7f;
            });
            Click = Make("Click", 0.025f, (t, d) => Noise(rnd) * Decay(t, 0.025f) * 0.6f);
            Hop = Make("Hop", 0.12f, (t, d) => Sine(Mathf.Lerp(300, 520, t / 0.12f), t) * Decay(t, 0.12f) * 0.5f);
            Heart = Make("Heart", 0.4f, (t, d) => (Sine(784, t) * Decay(t, 0.18f) + (t > 0.12f ? Sine(1046.5f, t) * Decay(t - 0.12f, 0.28f) : 0)) * 0.5f);
            // ---- fight sounds ----
            Growl = Make("Growl", 0.9f, (t, d) =>
            {
                float trem = 0.6f + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * 23f);
                float f = 85f + Mathf.Sin(t * 7f) * 8f;
                return (Saw(f, t) * 0.5f + Saw(f * 1.5f, t) * 0.2f + Noise(rnd) * 0.35f) * trem * Env(t, 0.9f, 0.08f) * 0.8f;
            });
            Hit = Make("Hit", 0.16f, (t, d) => (Sine(Mathf.Lerp(220f, 80f, t / 0.16f), t) * 0.8f + Noise(rnd) * Decay(t, 0.05f) * 0.6f) * Decay(t, 0.16f));
            HitHeavy = Make("HitHeavy", 0.32f, (t, d) => (Sine(Mathf.Lerp(160f, 45f, t / 0.32f), t) + Noise(rnd) * Decay(t, 0.08f) * 0.8f) * Decay(t, 0.32f) * 0.95f);
            Block = Make("Block", 0.12f, (t, d) => (Sq(320f, t) * 0.3f + Noise(rnd) * 0.5f) * Decay(t, 0.06f));
            Whoosh = Make("Whoosh", 0.22f, (t, d) =>
            {
                float n = Noise(rnd); float sweep = Mathf.Sin(Mathf.PI * t / 0.22f);
                return n * sweep * 0.35f;
            });
            Gong = Make("Gong", 1.6f, (t, d) => (Sine(110f, t) * 0.6f + Sine(221.5f, t) * 0.35f + Sine(332f, t) * 0.25f + Sine(447f, t) * 0.15f) * Decay(t, 1.6f) * 0.7f);
            // ---- traffic, mini-games, story ----
            Honk = Make("Honk", 0.42f, (t, d) => (Sq(392f, t) * 0.35f + Sq(494f, t) * 0.35f + Saw(196f, t) * 0.2f) * Env(t, 0.42f, 0.01f) * 0.55f);
            Beep = Make("Beep", 0.3f, (t, d) => (t < 0.12f || t > 0.17f ? Sq(1180f, t) * 0.3f : 0f) * 0.6f);
            Card = Make("Card", 0.07f, (t, d) => Noise(rnd) * Decay(t, 0.07f) * 0.55f + Sine(2400f, t) * Decay(t, 0.03f) * 0.15f);
            Chomp = Make("Chomp", 0.09f, (t, d) => Sq(Mathf.Lerp(420f, 260f, t / 0.09f), t) * Decay(t, 0.09f) * 0.35f);
            Cluck = Make("Cluck", 0.26f, (t, d) =>
            {
                float u = t % 0.13f; float f = 700f + 500f * Mathf.Exp(-u * 30f);
                return (Saw(f, u) * 0.4f + Noise(rnd) * 0.2f) * Mathf.Exp(-u * 22f) * 0.8f;
            });
            Thud = Make("Thud", 0.2f, (t, d) => (Sine(Mathf.Lerp(140f, 60f, t / 0.2f), t) + Noise(rnd) * Decay(t, 0.04f) * 0.7f) * Decay(t, 0.2f) * 0.8f);
            Match = Make("Match", 0.35f, (t, d) => (Sine(659.25f, t) * Decay(t, 0.15f) + (t > 0.09f ? Sine(987.8f, t) * Decay(t - 0.09f, 0.25f) : 0f)) * 0.45f);
            Page = Make("Page", 0.5f, (t, d) => Noise(rnd) * Mathf.Sin(Mathf.PI * t / 0.5f) * (0.35f + 0.25f * Mathf.Sin(t * 40f)) * 0.5f);
            Shot = Make("Shot", 0.5f, (t, d) => (Noise(rnd) * Decay(t, 0.12f) * 0.9f + Sine(Mathf.Lerp(120f, 50f, t / 0.5f), t) * Decay(t, 0.3f) * 0.8f) * 0.9f);
            Siren = Make("Siren", 2f, (t, d) => { float f = 760f + 220f * Mathf.Sin(t * Mathf.PI * 2f * 0.5f - Mathf.PI * 0.5f); return (Sine(f, t) * 0.5f + Sq(f, t) * 0.12f) * 0.45f; });
            Ambient = MakeAmbient(rnd);
            Music = MakeMusic();
            LoadOverrides();

            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = Music; _music.loop = true; _music.spatialBlend = 0; _music.Play();
            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.clip = Ambient; _ambient.loop = true; _ambient.spatialBlend = 0; _ambient.Play();
            for (int i = 0; i < 16; i++)
            {
                var s = new GameObject("SfxVoice").AddComponent<AudioSource>();
                s.transform.SetParent(transform); s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3; s.maxDistance = 45;
                _pool.Add(s);
            }
        }

        void Update()
        {
            float target = GameManager.I != null && GameManager.I.State == GameState.Paused ? 0.5f : 1f; // −6 dB duck
            _duck = Mathf.MoveTowards(_duck, target, Time.unscaledDeltaTime * 3f);
            _music.volume = 0.32f * AudioGain.Music * _duck;
            _ambient.volume = 0.45f * AudioGain.Sfx * _duck;
        }

        public void Play2D(AudioClip c, float vol = 1f, float pitch = 1f) => Voice(c, Vector3.zero, vol, pitch, false);
        public void Play3D(AudioClip c, Vector3 pos, float vol = 1f, float pitch = 1f) => Voice(c, pos, vol, pitch, true);

        void Voice(AudioClip c, Vector3 pos, float vol, float pitch, bool spatial)
        {
            if (c == null) return;
            AudioSource s = null;
            foreach (var v in _pool) if (!v.isPlaying) { s = v; break; }
            if (s == null) s = _pool[0];
            s.transform.position = pos; s.spatialBlend = spatial ? 1f : 0f;
            s.pitch = pitch; s.volume = vol * AudioGain.Sfx; s.clip = c; s.Play();
        }

        /// <summary>
        /// Real recordings replace the synthesized sounds: drop a file named after a clip field into
        /// Assets/Maily/Resources/Sfx/ (e.g. Bark.wav, Honk.ogg) and it is used instead.
        /// </summary>
        void LoadOverrides()
        {
            foreach (var f in GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.FieldType != typeof(AudioClip)) continue;
                var clip = Resources.Load<AudioClip>("Sfx/" + f.Name);
                if (clip != null) { f.SetValue(this, clip); Debug.Log("[Maily] Sound override: " + f.Name); }
            }
        }

        // ---------------------------------------------------------------- synthesis
        delegate float Gen(float t, float dur);
        static AudioClip Make(string name, float dur, Gen g)
        {
            int n = Mathf.CeilToInt(dur * SR); var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)SR; data[i] = Mathf.Clamp(g(t, dur) * Edge(t, dur), -1, 1); }
            var c = AudioClip.Create(name, n, 1, SR, false); c.SetData(data, 0); return c;
        }
        static float Edge(float t, float d) => Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((d - t) / 0.005f); // 5 ms fades
        static float Sine(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Sq(float f, float t) => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t));
        static float Saw(float f, float t) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
        static float Tri(float f, float t) => 2f * Mathf.Abs(Saw(f, t)) - 1f;
        static float Noise(System.Random r) => (float)r.NextDouble() * 2 - 1;
        static float Decay(float t, float d) => Mathf.Exp(-5f * t / d);
        static float Env(float t, float d, float a) => Mathf.Clamp01(t / a) * Mathf.Clamp01((d - t) / (d * 0.5f));

        static float Woof(float t, float t0, float f0, System.Random r)
        {
            float u = t - t0; if (u < 0 || u > 0.17f) return 0;
            float f = f0 * (1.15f - u * 2.2f);
            float body = Saw(f, u) * 0.55f + Sine(f * 0.5f, u) * 0.5f + Noise(r) * 0.25f;
            float formant = Sine(f * 2.7f, u) * 0.2f;
            return (body + formant) * Mathf.Clamp01(u / 0.012f) * Mathf.Exp(-u * 16f) * 0.9f;
        }

        static AudioClip MakeAmbient(System.Random r)
        {
            float dur = 8f; int n = (int)(dur * SR); var d = new float[n]; float brown = 0;
            for (int i = 0; i < n; i++) { brown = Mathf.Clamp(brown + Noise(r) * 0.02f, -1, 1); d[i] = brown * 0.25f; }
            // distant bird chirps (bulbuls & sparrows)
            for (int k = 0; k < 9; k++)
            {
                float start = (float)r.NextDouble() * (dur - 0.6f); float f = 2600 + (float)r.NextDouble() * 1800;
                int notes = 2 + r.Next(3);
                for (int m = 0; m < notes; m++)
                {
                    float ts = start + m * 0.11f;
                    for (int i = 0; i < (int)(0.07f * SR); i++)
                    {
                        int idx = (int)(ts * SR) + i; if (idx >= n) break;
                        float t = i / (float)SR; float ff = f * (1 + 0.25f * Mathf.Sin(t * 60));
                        d[idx] += Mathf.Sin(2 * Mathf.PI * ff * t) * Mathf.Sin(Mathf.PI * t / 0.07f) * 0.06f;
                    }
                }
            }
            // seamless loop crossfade
            int xf = SR / 2;
            for (int i = 0; i < xf; i++) { float a = i / (float)xf; d[i] = d[i] * a + d[n - xf + i] * (1 - a); }
            var c = AudioClip.Create("Ambient", n - xf, 1, SR, false);
            var trimmed = new float[n - xf]; System.Array.Copy(d, trimmed, n - xf); c.SetData(trimmed, 0); return c;
        }

        static AudioClip MakeMusic()
        {
            // Light, bouncy loop in C major pentatonic — 112 bpm, 8 bars.
            float bpm = 112f, beat = 60f / bpm; int bars = 8; float dur = bars * 4 * beat;
            int n = Mathf.CeilToInt(dur * SR); var d = new float[n];
            float[] scale = { 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f, 659.25f };
            int[] melody = { 4, 5, 4, 2, 3, 2, 0, -1, 2, 3, 4, 3, 2, 1, 2, -1, 4, 5, 6, 5, 4, 2, 3, -1, 2, 1, 0, 1, 2, 4, 0, -1 };
            int[] bass = { 0, 0, 3, 3, 4, 4, 0, 0 };
            float[] bassRoot = { 130.81f, 130.81f, 174.61f, 174.61f, 196f, 196f, 130.81f, 130.81f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float b = t / beat;
                int step = Mathf.FloorToInt(b) % melody.Length; float u = (b - Mathf.Floor(b)) * beat;
                float s = 0;
                int note = melody[step];
                if (note >= 0) s += (Tri(scale[note], t) * 0.5f + Sine(scale[note] * 2, t) * 0.15f) * Mathf.Exp(-u * 5f) * Mathf.Clamp01(u / 0.01f) * 0.45f;
                int bar = Mathf.FloorToInt(b / 4) % bars; float ub = ((b % 2f)) * beat;
                s += Sine(bassRoot[bar], t) * Mathf.Exp(-ub * 2.5f) * Mathf.Clamp01(ub / 0.01f) * 0.4f;
                // soft off-beat shaker
                float hb = (b * 2f) % 2f; if (hb > 1f) { float uh = (hb - 1f) * beat * 0.5f; s += ((float)((i * 1103515245 + 12345) & 0x7fff) / 16384f - 1f) * Mathf.Exp(-uh * 60f) * 0.05f; }
                d[i] = Mathf.Clamp(s * 0.8f, -1, 1);
            }
            var c = AudioClip.Create("Music", n, 1, SR, false); c.SetData(d, 0); return c;
        }
    }
}
