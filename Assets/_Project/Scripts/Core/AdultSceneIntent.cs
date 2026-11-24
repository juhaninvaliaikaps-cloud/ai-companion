namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: a typed scene can request the third-window adult clip even when the model
    /// forgets to emit a reaction tag. Text remains uncapped; only menu animations are capped.
    /// </summary>
    public static class AdultSceneIntent
    {
        public static string Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var value = " " + text.ToLowerInvariant() + " ";
            // A make-out is romantic, not explicit, so it is available outside the 18+ gate.
            if (!IntimateExpressions.Active)
            {
                if (value.Contains("make out") || value.Contains("making out"))
                    return AvatarInteractionCatalog.MakeOut;
                if (value.Contains("breast") || value.Contains("breasts") || value.Contains("chest"))
                    return value.Contains(" keeps ") || value.Contains("loop")
                        ? AvatarInteractionCatalog.TouchBreastLoop
                        : AvatarInteractionCatalog.TouchBreast;
                if (value.Contains("butt") || value.Contains("ass "))
                    return value.Contains(" keeps ") || value.Contains("loop")
                        ? AvatarInteractionCatalog.TouchButtLoop
                        : AvatarInteractionCatalog.TouchButt;
                return string.Empty;
            }

            var lying = value.Contains("lying") || value.Contains(" lie ") || value.Contains("lies down")
                || value.Contains("lie down") || value.Contains("lie together")
                || value.Contains("lying together") || value.Contains("lying down");
            var onTop = value.Contains("on top") || value.Contains("riding");
            var keeps = value.Contains(" keeps ") || value.Contains(" keeps") || value.Contains("loop")
                || value.Contains("continuously") || value.Contains("again and again");

            // Mouth / blowjob first: several later phrases ("kiss") can appear in the same line.
            if (value.Contains("blowjob") || value.Contains("blow job")
                || value.Contains("sucking") || value.Contains("sucks him")
                || value.Contains("mouth around") || value.Contains("going down on")
                || value.Contains("takes him in her mouth") || value.Contains("in her mouth"))
            {
                if (onTop)
                    return AvatarInteractionCatalog.BlowjobOnTop;
                if (lying)
                    return AvatarInteractionCatalog.BlowjobLying;
                return AvatarInteractionCatalog.BlowjobKneeling;
            }

            if (value.Contains("butt slap") || value.Contains("slaps your butt")
                || value.Contains("spanks your butt") || value.Contains("slap her butt"))
                return AvatarInteractionCatalog.ButtSlap;

            if (lying && (value.Contains("make out") || value.Contains("making out")))
                return value.Contains("slow") || value.Contains("second")
                    ? AvatarInteractionCatalog.LyingMakeOut2
                    : AvatarInteractionCatalog.LyingMakeOut;
            if (lying && value.Contains("kiss"))
                return AvatarInteractionCatalog.LyingKiss;
            if (value.Contains("make out") || value.Contains("making out"))
                return AvatarInteractionCatalog.MakeOut;
            if (value.Contains("kiss"))
                return AvatarInteractionCatalog.Kiss;

            if (value.Contains("breast") || value.Contains("breasts") || value.Contains("chest"))
                return keeps ? AvatarInteractionCatalog.TouchBreastLoop : AvatarInteractionCatalog.TouchBreast;
            if (value.Contains("vagina") || value.Contains("pussy") || value.Contains("crotch"))
                return AvatarInteractionCatalog.TouchVagina;
            if (value.Contains("butt") || value.Contains("ass "))
                return keeps ? AvatarInteractionCatalog.TouchButtLoop : AvatarInteractionCatalog.TouchButt;
            if (value.Contains("finger in") || value.Contains("finger into")
                || value.Contains("suck your finger") || value.Contains("sucks your finger"))
                return keeps ? AvatarInteractionCatalog.FingerSuckLoop : AvatarInteractionCatalog.FingerMouth;

            if (value.Contains("stroke") && value.Contains("hair"))
                return AvatarInteractionCatalog.StrokeHair;
            if (value.Contains("pat") && value.Contains("head"))
                return AvatarInteractionCatalog.PatHead;
            if (value.Contains("cup") && value.Contains("cheek"))
                return AvatarInteractionCatalog.CupCheek;
            if (value.Contains("touch") && value.Contains("face"))
                return AvatarInteractionCatalog.TouchFace;
            if (value.Contains("hold") && value.Contains("hand"))
                return AvatarInteractionCatalog.HoldHand;
            if (value.Contains("let go") || value.Contains("lets go"))
                return AvatarInteractionCatalog.LetGo;
            if (value.Contains("shoulder"))
                return AvatarInteractionCatalog.TouchShoulder;

            return string.Empty;
        }

        /// <summary>Opens the matching clip, if any. Does not queue a second chat message.</summary>
        public static void Trigger(string text)
        {
            var cue = Detect(text);
            if (cue.Length == 0)
                return;
            AdultSceneNow.Compose(cue, string.Empty);
            AdultCueRouter.Request(cue);
        }
    }
}
