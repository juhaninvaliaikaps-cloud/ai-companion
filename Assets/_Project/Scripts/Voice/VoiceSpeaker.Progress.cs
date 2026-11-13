using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>
    /// JUH-108: which sentence is playing and how far, for the spoken-text glow in the new chat.
    /// Each queued clip keeps the sentence it was made from. Read-only; playback is unchanged.
    /// </summary>
    public sealed partial class VoiceSpeaker
    {
        readonly Queue<string> _clipTexts = new Queue<string>();
        string _currentText = string.Empty;
        int _clipSerial;

        public bool TryGetSpokenClip(out int clipSerial, out string text, out float time, out float length)
        {
            clipSerial = _clipSerial;
            text = _currentText;
            time = 0f;
            length = 0f;
            if (_current == null || _source == null || !_playback.Playing || !SourceBusy())
                return false;

            length = _current.length;
            var left = (float)(_playUntil - AudioSettings.dspTime);
            time = Mathf.Clamp(length - left, 0f, length);
            return length > 0f;
        }
    }
}
