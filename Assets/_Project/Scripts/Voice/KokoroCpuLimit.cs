namespace AICompanion.Voice
{
    /// <summary>
    /// JUH-168: while a game is in front, Kokoro runs with only a few threads, at below-normal
    /// priority. Kokoro runs on the CPU (onnxruntime), and by default it keeps about five of the
    /// 5600X's six cores busy for each sentence. On top of a game's full GPU load, that jump in
    /// power draw is the kind the PC couldn't take on 2026-11-21. Measured on that PC for one
    /// sentence of 11.9 s audio: every core 3.6 s (5.4 cores busy), 2 threads 5.3 s (1.8 cores,
    /// half the CPU time), still more than twice real time.
    /// </summary>
    public static class KokoroCpuLimit
    {
        public const int DefaultCores = 2;
        public const int MaxCores = 16;

        public const string OldScriptLine =
            "Voice: this kokoro_server.py doesn't limit its threads. Copy tools/kokoro/kokoro_server.py over your copy (see docs/OFFLINE_TTS.md) so Kokoro uses fewer cores while a game is in front.";

        /// <summary>The cores setting, kept between 1 and <see cref="MaxCores"/>.</summary>
        public static int Clean(int cores)
        {
            if (cores < 1)
                return 1;
            return cores > MaxCores ? MaxCores : cores;
        }

        /// <summary>Limited only while the setting is on and a game is in front.</summary>
        public static bool Want(bool enabled, bool gameInFront)
        {
            return enabled && gameInFront;
        }

        /// <summary>The threads a request asks for: the cleaned cores when limited, else 0 (every core).</summary>
        public static int RequestThreads(bool limited, int cores)
        {
            return limited ? Clean(cores) : 0;
        }

        /// <summary>A limited request whose reply doesn't name its threads came from an older script.</summary>
        public static bool OldScript(int askedThreads, int replyThreads)
        {
            return askedThreads > 0 && replyThreads < 0;
        }

        /// <summary>The Player.log line for a change.</summary>
        public static string LogLine(bool limited, int cores)
        {
            if (!limited)
                return "Voice: Kokoro uses every core again";
            var n = Clean(cores);
            return "Voice: Kokoro uses " + n + (n == 1 ? " thread" : " threads") + " at below-normal priority while a game is in front";
        }
    }
}
