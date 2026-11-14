using System;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Memory
{
    /// <summary>A new open thread the model proposed at session end. <see cref="IsValid"/> needs a text and at least one outcome.</summary>
    [Serializable]
    public sealed class NewThread
    {
        public string text = string.Empty;
        public string goodOutcome = string.Empty;
        public string badOutcome = string.Empty;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(text)
                    && (!string.IsNullOrWhiteSpace(goodOutcome) || !string.IsNullOrWhiteSpace(badOutcome));
            }
        }
    }

    /// <summary>
    /// Keeps her world moving (JUH-117). She starts with two or three open threads from the
    /// character sheet; once they have turned out, nothing new would ever happen to her. So
    /// the session-end request may also propose one new thread, and this stores it while
    /// fewer than <see cref="MaxOpen"/> are open.
    /// </summary>
    public static class WorldThreads
    {
        /// <summary>At most this many threads are open at once.</summary>
        public const int MaxOpen = 3;

        public const int MaxLength = 200;

        /// <summary>
        /// Add this to the session-end request. The reply may then carry a "newThread" object
        /// next to whatever else it returns; <see cref="TryParse"/> reads it.
        /// </summary>
        public const string Instruction =
            "You may also add one new thing in her own life that she is now waiting for, planning or dreading, as " +
            "\"newThread\": { \"text\": \"\", \"goodOutcome\": \"\", \"badOutcome\": \"\" }. " +
            "Ordinary life, one sentence each, in the third person. It is about her, not the user, and nothing sexual. Leave it out when nothing fits.";

        [Serializable]
        sealed class Envelope
        {
            public NewThread newThread = new NewThread();
        }

        /// <summary>How many threads are open in this store.</summary>
        public static int OpenCount(MemoryStore store)
        {
            if (store == null)
                return 0;
            var count = 0;
            var entries = store.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].isThread)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Reads a "newThread" object out of a model reply (prose and code fences around the
        /// JSON are fine). Null when there is none or it is incomplete.
        /// </summary>
        public static NewThread TryParse(string modelReply)
        {
            var json = CharacterSheet.ExtractObject(modelReply);
            if (json == null)
                return null;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope == null || envelope.newThread == null || !envelope.newThread.IsValid)
                    return null;
                envelope.newThread.text = Clean(envelope.newThread.text);
                envelope.newThread.goodOutcome = Clean(envelope.newThread.goodOutcome);
                envelope.newThread.badOutcome = Clean(envelope.newThread.badOutcome);
                return envelope.newThread.IsValid ? envelope.newThread : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Stores a new open thread, unless <see cref="MaxOpen"/> are open already, "Her own
        /// world" or "Life while you are away" is switched off, or the same text is already remembered. Returns the entry, or null.
        /// </summary>
        public static MemoryEntry Add(MemoryStore store, NewThread thread, DateTime nowUtc, bool privateMode)
        {
            if (store == null || thread == null || !thread.IsValid || !PisaraSwitches.World || !PisaraSwitches.AwayEvents)
                return null;
            if (OpenCount(store) >= MaxOpen)
                return null;

            var before = store.Count;
            var now = nowUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            var entry = store.AddEntry(new MemoryEntry
            {
                text = Clean(thread.text),
                origin = MemoryOrigins.Life,
                horizon = MemoryHorizons.Long,
                isThread = true,
                goodOutcome = Clean(thread.goodOutcome),
                badOutcome = Clean(thread.badOutcome),
                happenedAtUtc = now,
                createdAtUtc = now,
                isPrivate = privateMode,
            });
            if (store.Count == before)
                return null;
            store.Save();
            return entry;
        }

        static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var line = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return line.Length <= MaxLength ? line : line.Substring(0, MaxLength).TrimEnd();
        }
    }
}
