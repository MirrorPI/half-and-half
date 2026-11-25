using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace HalfAndHalf.Presentation
{
    public sealed class CutAudio : MonoBehaviour
    {
        public AudioClip[] CutVariants;
        public AudioClip BladeClip, ResultClip;
        public AudioMixerGroup CutGroup, UiGroup, MusicGroup;
        public AudioSource CutSource, BladeSource, ResultSource, MusicSource;
        public int LastVariant { get; private set; } = -1;
        public bool Haptics = true;
        public float EffectsVolume = .8f, MusicVolume = .5f;
        AudioClip requestedScore;
        float musicGain = 1;
        bool hasScore;
        bool contacted, playResonance;
        AudioSource resonance;
        const float ResonanceGain = .62f;
        Coroutine fade;
        void Awake()
        {
            CutSource = Source(CutGroup); BladeSource = Source(CutGroup);
            ResultSource = Source(UiGroup); MusicSource = Source(MusicGroup);
        }
        AudioSource Source(AudioMixerGroup group)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            source.outputAudioMixerGroup = group; source.volume = EffectsVolume;
            return source;
        }
        public void ApplySettings(float effects, float music, bool vibration)
        {
            EffectsVolume = Mathf.Clamp01(effects); MusicVolume = Mathf.Clamp01(music); Haptics = vibration;
            if (fade == null) CutSource.volume = EffectsVolume;
            BladeSource.volume = EffectsVolume * .22f; ResultSource.volume = EffectsVolume * .35f;
            if (resonance != null) resonance.volume = EffectsVolume * ResonanceGain;
            MusicSource.volume = MusicVolume * musicGain;
        }
        public void SetStageMusic(AudioClip clip)
        {
            if (requestedScore == clip) return;
            requestedScore = clip;
            hasScore = true;
        }

        void Update()
        {
            if (!hasScore) return;
            float dt = Time.unscaledDeltaTime;
            if (MusicSource.clip != requestedScore)
            {
                musicGain = MusicSource.isPlaying ? Mathf.MoveTowards(musicGain, 0, dt / .28f) : 0;
                if (musicGain <= 0)
                {
                    MusicSource.Stop();
                    MusicSource.clip = requestedScore;
                    MusicSource.volume = 0;
                    MusicSource.loop = true;
                    if (requestedScore != null) MusicSource.Play();
                }
            }
            else
            {
                musicGain = Mathf.MoveTowards(musicGain, 1, dt / .65f);
            }
            MusicSource.volume = MusicVolume * musicGain;
        }

        public void BeginCut(bool whirlpool = false)
        {
            StopFade(); StopSources(); contacted = false;
            playResonance = whirlpool;
            if (whirlpool && resonance == null)
            {
                resonance = Source(CutGroup);
                resonance.clip = Resources.Load<AudioClip>("WhirlpoolResonance");
            }
            ApplySettings(EffectsVolume, MusicVolume, Haptics);
            if (BladeClip != null) { BladeSource.clip = BladeClip; BladeSource.Play(); }
        }
        public void Progress(float progress, float contactProgress)
        {
            if (contacted || progress < contactProgress) return;
            contacted = true;
            if (playResonance && resonance.clip != null) resonance.PlayDelayed(.18f);
            if (CutVariants != null && CutVariants.Length > 0)
            {
                // Randomize within the material while excluding the last played take.
                int next = CutVariants.Length == 1 ? 0 : Random.Range(0, CutVariants.Length - (LastVariant >= 0 ? 1 : 0));
                if (LastVariant >= 0 && CutVariants.Length > 1 && next >= LastVariant) next++;
                LastVariant = next; CutSource.clip = CutVariants[next]; CutSource.Play();
            }
#if UNITY_IOS && !UNITY_EDITOR
            if (Haptics) Handheld.Vibrate();
#endif
        }
        public void Result()
        { if (ResultClip != null) { ResultSource.clip = ResultClip; ResultSource.Play(); } }
        public void ResetAttempt()
        {
            if (resonance != null) resonance.Stop();
            contacted = false; BladeSource.Stop(); ResultSource.Stop();
            StopFade();
            if (CutSource.isPlaying && isActiveAndEnabled) fade = StartCoroutine(FadeTail());
            else CutSource.Stop();
        }
        IEnumerator FadeTail()
        {
            float initial = CutSource.volume; double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < .045)
            { CutSource.volume = initial * (1 - (float)((Time.realtimeSinceStartupAsDouble - start) / .045)); yield return null; }
            CutSource.Stop(); CutSource.volume = EffectsVolume; fade = null;
        }
        void StopFade() { if (fade != null) StopCoroutine(fade); fade = null; }
        void StopSources() { if (resonance != null) resonance.Stop(); CutSource.Stop(); BladeSource.Stop(); ResultSource.Stop(); }
        void OnDisable() { StopFade(); if (CutSource != null) StopSources(); if (MusicSource != null) { MusicSource.Stop(); MusicSource.clip = null; } }
    }
}
