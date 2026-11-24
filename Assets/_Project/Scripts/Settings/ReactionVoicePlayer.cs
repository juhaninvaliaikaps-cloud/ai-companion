using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// Plays one short reaction wav when a cue, a mood rest look, an interaction, or a
    /// 2D portrait change offers an expression (JUH-192, JUH-193). The pack is read from
    /// disk. A missing folder, a bad manifest, or a missing wav stays quiet. Added by
    /// <see cref="SettingsPanel"/>. The clip lives on its own object so it does not take
    /// the speech <c>AudioSource</c>. When a spoken line armed <see cref="ReactionSpeechLead"/>,
    /// playback of that line waits until this clip ends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReactionVoicePlayer : MonoBehaviour
    {
        const float ReloadSeconds = 5f;
        const int MaxWavBytes = 8 * 1024 * 1024;

        ReactionVoiceCatalog _catalog = ReactionVoiceCatalog.Empty;
        ReactionVoiceCatalog _adultCatalog = ReactionVoiceCatalog.Empty;
        readonly ReactionVoiceSession _session = new ReactionVoiceSession();
        readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);
        AudioSource _source;
        GameObject _host;
        AudioClip _clip;
        ICompanionPause _pause;
        bool _pauseSearched;
        string _appData;
        string _persistent;
        string _folder = string.Empty;
        string _adultFolder = string.Empty;
        DateTime _stamp;
        DateTime _adultStamp;
        float _nextReload;
        double _playingUntil;
        bool _loggedMissingPack;

        void Awake()
        {
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _persistent = Application.persistentDataPath;
            _host = new GameObject("ReactionVoice");
            _host.transform.SetParent(transform, false);
            _source = _host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            Refresh();
            _nextReload = Time.unscaledTime + ReloadSeconds;
        }

        void OnDestroy()
        {
            ReleaseClip();
            if (_host != null)
                Destroy(_host);
        }

        void LateUpdate()
        {
            if (_clip != null && _source != null && _source.isPlaying && !Allowed())
            {
                _source.Stop();
                ReleaseClip();
                _playingUntil = Time.unscaledTime;
            }

            var now = Time.unscaledTime;
            if (now >= _nextReload)
            {
                _nextReload = now + ReloadSeconds;
                Refresh();
            }

            string expression;
            ReactionVoiceCause cause;
            var taken = ReactionVoiceRequests.TryTake(out expression, out cause);
            var started = false;
            if (taken && Allowed())
            {
                ReactionVoiceClip chosen = default(ReactionVoiceClip);
                var folder = _folder;
                var adult = IntimateExpressions.Active && _adultCatalog.ClipCount > 0
                    && _session.TryChoose(_adultCatalog, expression, cause, now, Index, out chosen);
                if (adult)
                    folder = _adultFolder;
                if ((adult || _session.TryChoose(_catalog, expression, cause, now, Index, out chosen))
                    && TryPlay(chosen, folder))
                {
                    started = true;
                    _playingUntil = now + PlaybackSeconds(_clip, chosen);
                    Debug.Log("Reaction voice: " + chosen.Id + " -> " + chosen.Expression);
                    if (ReactionSpeechLead.NeedsAnswer || ReactionSpeechLead.Blocks(now))
                        ReactionSpeechLead.NotePlayback(_playingUntil);
                }
            }

            if (!Allowed())
            {
                // F6 or tray pause stopped the clip. Do not keep the next line waiting
                // for a sound that is no longer playing.
                ReactionSpeechLead.Release(now);
            }
            else if (!started && ReactionSpeechLead.NeedsAnswer)
            {
                // No new clip. A sound that is already playing still finishes before the line.
                var until = _source != null && _source.isPlaying ? _playingUntil : now;
                ReactionSpeechLead.NotePlayback(until);
            }
        }

        static double PlaybackSeconds(AudioClip clip, ReactionVoiceClip chosen)
        {
            var seconds = clip != null ? clip.length : 0d;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0d)
                seconds = chosen.DurationSeconds;
            return ReactionVoiceSession.ClampSeconds(seconds);
        }

        static int Index(int count)
        {
            return UnityEngine.Random.Range(0, count);
        }

        // True when the request was consumed, including missing clips or disabled voice.
        // A busy voice channel is the only case where the caller should try again.
        public bool FinishRibbonReward(RibbonRewardSound reward)
        {
            var now = Time.unscaledTime;
            if (reward == RibbonRewardSound.None || !Allowed() || _source == null)
                return true;
            if (_source.isPlaying || ReactionSpeechLead.NeedsAnswer || ReactionSpeechLead.Blocks(now))
                return false;

            var discovery = reward == RibbonRewardSound.Discovery;
            var expression = discovery ? "surprised" : "smile";
            var id = discovery ? "surprised_oh_1" : "smile_mm_1";
            if (_catalog.TryFind(expression, id, out var chosen) && TryPlay(chosen))
            {
                _playingUntil = now + PlaybackSeconds(_clip, chosen);
                Debug.Log("Ribbon reward voice: " + chosen.Id);
            }
            return true;
        }

        bool Allowed()
        {
            if (!_pauseSearched)
            {
                _pauseSearched = true;
                _pause = GetComponent<ICompanionPause>();
            }

            if (_pause != null && _pause.IsPaused)
                return false;
            return PlayerPrefs.GetInt(VoiceSpeaker.MutePref, 0) == 0;
        }

        void Refresh()
        {
            RefreshAdult();
            try
            {
                var folder = ReactionVoicePaths.Resolve(_appData, _persistent, Directory.Exists);
                if (string.IsNullOrEmpty(folder))
                {
                    if (!_loggedMissingPack)
                    {
                        _loggedMissingPack = true;
                        var expected = ReactionVoicePaths.Expected(_appData, _persistent);
                        Debug.Log("Reaction voice: no pack at " + (expected ?? "the ProjectLeia folder") + ". Reactions stay quiet.");
                    }

                    _folder = string.Empty;
                    _catalog = ReactionVoiceCatalog.Empty;
                    return;
                }

                var manifest = ReactionVoicePaths.Manifest(folder);
                DateTime stamp;
                try
                {
                    stamp = File.GetLastWriteTimeUtc(manifest);
                }
                catch (Exception)
                {
                    stamp = DateTime.MinValue;
                }

                if (string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase) && stamp == _stamp)
                    return;

                List<string> warnings;
                _catalog = ReactionVoiceCatalog.Load(folder, out warnings);
                _folder = folder;
                _stamp = stamp;
                _loggedMissingPack = false;
                for (var i = 0; i < warnings.Count; i++)
                    LogOnce(warnings[i]);
                if (_catalog.ClipCount > 0)
                    LogOnce("Reaction voice: " + _catalog.ClipCount.ToString() + " clips from " + folder);
            }
            catch (Exception ex)
            {
                LogOnce("Reaction voice: the pack could not be loaded (" + ex.GetType().Name + ").");
                _catalog = ReactionVoiceCatalog.Empty;
            }
        }

        void RefreshAdult()
        {
            var folder = ReactionVoicePaths.AdultExpected(_appData, _persistent);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                _adultFolder = string.Empty;
                _adultCatalog = ReactionVoiceCatalog.Empty;
                return;
            }

            var manifest = Path.Combine(folder, ReactionVoicePaths.AdultManifestFileName);
            DateTime stamp;
            try
            {
                stamp = File.GetLastWriteTimeUtc(manifest);
            }
            catch (Exception)
            {
                stamp = DateTime.MinValue;
            }

            if (string.Equals(folder, _adultFolder, StringComparison.OrdinalIgnoreCase) && stamp == _adultStamp)
                return;

            List<string> warnings;
            _adultCatalog = ReactionVoiceCatalog.Load(folder, ReactionVoicePaths.AdultManifestFileName, out warnings);
            _adultFolder = folder;
            _adultStamp = stamp;
            for (var i = 0; i < warnings.Count; i++)
                LogOnce(warnings[i]);
            if (_adultCatalog.ClipCount > 0)
                LogOnce("Reaction voice: " + _adultCatalog.ClipCount.ToString() + " adult clips from " + folder);
        }

        bool TryPlay(ReactionVoiceClip chosen, string folder)
        {
            if (_source == null || string.IsNullOrEmpty(folder))
                return false;

            var path = Path.Combine(folder, chosen.FileName);
            byte[] bytes;
            try
            {
                if (!File.Exists(path))
                {
                    LogOnce("Reaction voice: missing '" + chosen.FileName + "'.");
                    return false;
                }

                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxWavBytes)
                {
                    LogOnce("Reaction voice: '" + chosen.FileName + "' is empty or too large.");
                    return false;
                }

                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                LogOnce("Reaction voice: could not read '" + chosen.FileName + "' (" + ex.GetType().Name + ").");
                return false;
            }

            float[] samples;
            int channels;
            int rate;
            if (!WavPcm.TryDecode(bytes, 48000, out samples, out channels, out rate))
            {
                LogOnce("Reaction voice: '" + chosen.FileName + "' is not a PCM wav.");
                return false;
            }

            AudioClip clip;
            if (!TryBuild(chosen.Id, samples, channels, rate, out clip))
                return false;

            if (_source.isPlaying)
                _source.Stop();
            ReleaseClip();
            _clip = clip;
            _source.clip = clip;
            _source.Play();
            return true;
        }

        static bool TryBuild(string id, float[] samples, int channels, int rate, out AudioClip clip)
        {
            clip = null;
            if (samples == null || channels <= 0 || channels > 2 || rate <= 0)
                return false;
            var frames = samples.Length / channels;
            if (frames <= 0)
                return false;

            try
            {
                clip = AudioClip.Create(string.IsNullOrEmpty(id) ? "reaction" : id, frames, channels, rate, false);
                if (clip == null || clip.samples <= 0)
                {
                    Release(clip);
                    clip = null;
                    return false;
                }

                if (!clip.SetData(samples, 0))
                {
                    Release(clip);
                    clip = null;
                    return false;
                }
            }
            catch (Exception)
            {
                Release(clip);
                clip = null;
                return false;
            }

            return true;
        }

        void ReleaseClip()
        {
            if (_source != null)
                _source.clip = null;
            Release(_clip);
            _clip = null;
        }

        static void Release(AudioClip clip)
        {
            if (clip == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(clip);
            else
                UnityEngine.Object.DestroyImmediate(clip);
        }

        void LogOnce(string line)
        {
            if (string.IsNullOrEmpty(line) || !_logged.Add(line))
                return;
            Debug.Log(line);
        }
    }
}
