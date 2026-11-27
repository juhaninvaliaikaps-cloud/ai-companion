using AICompanion.Core;
using AICompanion.Settings;
using UnityEngine;

namespace AICompanion.NewUI
{
    [DisallowMultipleComponent]
    sealed class RibbonAudio : MonoBehaviour
    {
        const int SampleRate = 44100;
        const float NavigationSpacing = 0.055f;

        SettingsPanel _settings;
        ReactionVoicePlayer _voice;
        AudioSource _source;
        AudioClip _navigation;
        AudioClip _activation;
        AudioClip _send;
        AudioClip _affection;
        AudioClip _discovery;
        float _nextNavigation;

        public void Attach(SettingsPanel settings)
        {
            _settings = settings;
            if (_source != null)
                return;

            var host = new GameObject("Ribbon UI Sounds");
            host.transform.SetParent(transform, false);
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _navigation = MakeNavigationClick();
            _activation = MakeClip("Ribbon Activate", 0.064f, 0.22f, 770f, 970f);
            _send = MakeClip("Ribbon Send", 0.135f, 0.27f, 590f, 1180f);
            _affection = MakeClip("Ribbon Affection", 0.23f, 0.30f, 349.23f, 523.25f);
            _discovery = MakeClip("Ribbon Discovery", 0.34f, 0.31f, 659.25f, 880f, 1318.51f);
        }

        public void Detach()
        {
            _settings = null;
            if (_source != null)
                _source.Stop();
            _nextNavigation = 0f;
        }

        public void Navigate()
        {
            var now = Time.unscaledTime;
            if (now < _nextNavigation)
                return;
            _nextNavigation = now + NavigationSpacing;
            Play(_navigation);
        }

        public void Activate() => Play(_activation);
        public void Send() => Play(_send);

        public void Reward(RibbonRewardSound reward)
        {
            if (_settings == null || reward == RibbonRewardSound.None)
                return;
            Play(reward == RibbonRewardSound.Discovery ? _discovery : _affection);
        }

        public bool FinishRewardVoice(RibbonRewardSound reward)
        {
            if (_voice == null)
                TryGetComponent(out _voice);
            return _settings == null || _voice == null || _voice.FinishRibbonReward(reward);
        }

        void Play(AudioClip clip)
        {
            var settings = _settings != null ? _settings.CurrentSettings : null;
            if (settings != null && settings.RibbonSoundVolume > 0f && clip != null)
                _source.PlayOneShot(clip, settings.RibbonSoundVolume);
        }

        void OnDestroy()
        {
            if (_source != null)
                Destroy(_source.gameObject);
            Destroy(_navigation);
            Destroy(_activation);
            Destroy(_send);
            Destroy(_affection);
            Destroy(_discovery);
        }

        static AudioClip MakeNavigationClick()
        {
            var samples = new float[Mathf.CeilToInt(0.040f * SampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;
                var attack = Mathf.Min(1f, time / 0.0014f);
                var release = Mathf.Min(1f, (samples.Length - i) / (0.005f * SampleRate));
                // A low, damped body with a much shorter contact sound at the front.
                var body = 0.13f * Mathf.Sin(2f * Mathf.PI * (245f - 900f * time) * time)
                    * Mathf.Exp(-time / 0.008f);
                var contact = (0.055f * Mathf.Sin(2f * Mathf.PI * 730f * time)
                    + 0.020f * Mathf.Sin(2f * Mathf.PI * 1510f * time)) * Mathf.Exp(-time / 0.0026f);
                samples[i] = (body + contact) * attack * release;
            }

            var clip = AudioClip.Create("Ribbon Navigate", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip MakeClip(string name, float seconds, float amplitude, params float[] pitches)
        {
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            for (var note = 0; note < pitches.Length; note++)
            {
                var start = Mathf.RoundToInt(note * seconds * 0.20f * SampleRate);
                var duration = (samples.Length - start) / (float)SampleRate;
                var angularFrequency = 2f * Mathf.PI * pitches[note];
                for (var i = start; i < samples.Length; i++)
                {
                    var time = (i - start) / (float)SampleRate;
                    var attack = Mathf.Min(1f, time / 0.005f);
                    var tail = Mathf.Min(1f, (duration - time) / 0.012f);
                    var decay = Mathf.Exp(-4.5f * time / duration);
                    var phase = angularFrequency * time;
                    var tone = Mathf.Sin(phase) + 0.19f * Mathf.Sin(2f * phase);
                    samples[i] += tone * (amplitude / pitches.Length) * attack * tail * decay;
                }
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
