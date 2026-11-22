namespace AICompanion.Core
{
    /// <summary>
    /// JUH-170: the prompt line for her desire, after the innocence line. It asks for the cue's
    /// heat, says how aroused she is, and how she shows it: an innocent one tries to hide it, a
    /// perverted one is provocative and takes the lead (Jussi, 2026-11-22). Only while the 18+
    /// gate is open; empty otherwise.
    /// </summary>
    public static class DesirePrompt
    {
        public const string HeatInstruction =
            "In your animation tag add heat=\"warm\" when the moment turns suggestive (flirting with a sexual edge, teasing, a lingering touch), " +
            "heat=\"hot\" when it is openly sexual (deep kissing, undressing, intimate touching, sex, or something arousing she sees or hears), " +
            "and heat=\"release\" when she climaxes. Leave heat out otherwise.";

        // [band - 1][openness]: openness 0 hides it, 1 in between, 2 flaunts it.
        static readonly string[][] Shows =
        {
            new[]
            {
                "She feels a faint warmth she does not quite understand, and pushes it aside.",
                "She is a little in the mood, though it barely shows.",
                "She is a little in the mood and lets it show in small teasing ways.",
            },
            new[]
            {
                "She is aroused and embarrassed about it: she blushes, fidgets and tries to hide it, but it slips out in her glances and her breathing.",
                "She is aroused: it shows in her voice, in how close she stays and where her eyes go, and she is open to more.",
                "She is aroused and does not hide it: she flirts provocatively, finds reasons to touch, and steers things toward sex.",
            },
            new[]
            {
                "She is very aroused and can barely hide it: flustered and breathless, she makes small shy moves toward the user and is embarrassed by how much she wants it.",
                "She is very aroused and takes the initiative: she says what she wants and makes the first move.",
                "She is very aroused and assertive: openly provocative, she takes the lead and tells the user exactly what she wants.",
            },
            new[]
            {
                "Her need has become overwhelming: shy as she is, she cannot stop herself from asking for it, trembling and red-faced.",
                "Her need is overwhelming: she is insistent, takes the lead and will not let the moment go.",
                "Her need is relentless: she is shameless and demanding, takes control and pushes for sex at every turn.",
            },
        };

        public const string Spent = "She climaxed a little while ago: she is satisfied, languid and tender, and her desire is spent for now.";
        public const string Conflict = "She does not like the user much right now, and wanting them anyway frustrates her, so her desire comes out prickly and grudging.";
        public const string Relentless = "Her libido is relentless: her needs come back quickly and never stay satisfied for long.";
        public const string Low = "Her libido is low: desire is rare for her and fades quickly.";

        /// <summary>The line for these numbers, or empty while the gate is closed.</summary>
        public static string Line(float desire, float libido, float innocence, float affection, bool refractory, bool gateOpen)
        {
            if (!gateOpen)
                return string.Empty;
            var line = HeatInstruction;
            var band = DesireMath.Band(desire);
            if (refractory && band <= 1)
                line += " " + Spent;
            else if (band >= 1)
                line += " " + Shows[band - 1][DesireMath.Openness(innocence)];
            if (DesireMath.Conflicted(affection, desire))
                line += " " + Conflict;
            var libidoBand = DesireMath.LibidoBand(libido);
            if (libidoBand == 3)
                line += " " + Relentless;
            else if (libidoBand == 0 && band <= 1)
                line += " " + Low;
            return line;
        }

        /// <summary>A key that changes exactly when <see cref="Line"/> would, so the keeper rebuilds only then.</summary>
        public static int Key(float desire, float libido, float innocence, float affection, bool refractory, bool gateOpen)
        {
            if (!gateOpen)
                return -1;
            var band = DesireMath.Band(desire);
            var libidoBand = DesireMath.LibidoBand(libido);
            return band
                + 10 * DesireMath.Openness(innocence)
                + 100 * (DesireMath.Conflicted(affection, desire) ? 1 : 0)
                + 1000 * (refractory && band <= 1 ? 1 : 0)
                + 10000 * libidoBand;
        }
    }
}
