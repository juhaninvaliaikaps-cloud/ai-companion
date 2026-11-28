using System.Text.RegularExpressions;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: a typed scene can request the third-window adult clip even when the model
    /// forgets to emit a reaction tag. Text remains uncapped; only menu animations are capped.
    /// JUH-190: a mention, question or wish is not an action, so only an actual initiation
    /// ("I kiss you", "kiss me", "they make out", "she gives him a blowjob") opens a clip.
    /// </summary>
    public static class AdultSceneIntent
    {
        public static string Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var value = " " + text.ToLowerInvariant() + " ";
            if (!LooksLikeAction(value))
                return string.Empty;
            // A make-out is romantic, not explicit, so it is available outside the 18+ gate.
            if (!IntimateExpressions.Active)
            {
                if (value.Contains("make out") || value.Contains("making out"))
                    return AvatarInteractionCatalog.MakeOut;
                if (value.Contains("kiss"))
                    return AvatarInteractionCatalog.Kiss;
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

            // Actual intercourse. Facing/on-top comes first only after the behind tests, so
            // "fucking her from behind" cannot be read as cowgirl.
            var behind = value.Contains("doggystyle") || value.Contains("doggy style")
                || value.Contains("from behind") || value.Contains("fucking her ass")
                || value.Contains("fuck her ass") || value.Contains("in the ass") || value.Contains("anal")
                || value.Contains("bent over")
                || (value.Contains("behind") && (value.Contains("fuck") || value.Contains("sex")
                    || value.Contains("ride") || value.Contains("penetrat")));
            if (behind)
                return AvatarInteractionCatalog.LapdanceBehind;

            var facing = value.Contains("cowgirl") || value.Contains("missionary")
                || value.Contains("on top of you") || value.Contains("on top of me")
                || value.Contains("riding you") || value.Contains("riding me")
                || value.Contains("rides me") || value.Contains("straddl")
                || value.Contains("facing you") || value.Contains("penetrat");
            if (facing)
                return AvatarInteractionCatalog.LapdanceFacing;

            // A plain "we have sex" has no position; the facing loop is the neutral default.
            if (value.Contains("sex") || value.Contains("fuck") || value.Contains("thrust"))
                return AvatarInteractionCatalog.LapdanceFacing;

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

        /// <summary>
        /// True only for a real initiation, not for mentioning, asking, imagining or wanting.
        /// Conservative on purpose: a false positive puts a sexual clip on screen.
        /// </summary>
        static bool LooksLikeAction(string value)
        {
            if (IsMentionOrWish(value))
                return false;

            // First/third person acting, or a direct imperative.
            if (Regex.IsMatch(value, @"\b(i|we|they|he|she)\b.{0,22}\b(kiss|kisses|kissed|kissing|make out|made out|making out)\b"))
                return true;
            if (Regex.IsMatch(value, @"\b(kiss me|kiss her|kiss him|kiss you|make out with|making out with|start making out)\b"))
                return true;
            if (Regex.IsMatch(value, @"\b(i|we|they|he|she)\b.{0,24}\b(lean|leans|leaned|pull|pulls|pulled|start|starts|started|begin|begins|began)\b.{0,24}\b(kiss|kissed|kissing|make out|making out)\b"))
                return true;

            // A blowjob has to be actually described, not merely named.
            if (Regex.IsMatch(value, @"\b(i|we|they|he|she)\b.{0,40}\b(blowjob|blow job|suck|sucks|sucking|give|gives|giving|gave|takes him|took him|going down on)\b"))
                return true;

            // Intercourse and the positions that only happen during it.
            if (Regex.IsMatch(value, @"\b(i|we|they|he|she)\b.{0,40}\b(sex|fuck|fucks|fucked|fucking|penetrat|penetrated|riding|rides|rode|straddl|cowgirl|missionary|doggystyle|doggy style|anal|thrust|thrusts|thrusting|on top of you|on top of me)\b"))
                return true;
            if (Regex.IsMatch(value, @"\b(cowgirl|missionary|doggystyle|doggy style|from behind|anal sex|penetration)\b"))
                return true;

            // A touch must name what is being grabbed, cupped or touched.
            if (Regex.IsMatch(value, @"\b(i|we|they|he|she)\b.{0,30}\b(grab|grabs|grabbed|touch|touches|touched|cup|cups|cupped|feel|feels|felt|fondle|fondles|fondled|kiss|kisses|kissed|suck|sucks|sucked|slap|slaps|slapped|spank|spanks|spanked|pat|pats|patted|stroke|strokes|stroked|hold|holds|held)\b.{0,22}\b(breast|breasts|chest|butt|ass|shoulder|hair|head|cheek|face|hand|vagina|pussy|crotch|finger)\b"))
                return true;

            return false;
        }

        static bool IsMentionOrWish(string value)
        {
            var words = new[]
            {
                "mention", "mentioned", "mentioning", "talk about", "talked about", "talking about",
                "ask about", "asked about", "asking about", "question", "wonder", "wondered",
                "think about", "thought about", "thinking about", "imagine", "imagined", "imagining",
                "fantasize", "fantasized", "fantasising", "fantasizing", "dream", "dreamed", "dreamt",
                "what if", "if we ", "if you ", "should", "could", "would", "maybe", "might",
                "want to", "wanted to", "wanting to", "want ", "wanted ", "need ", "needed ",
                "plan to", "planned to", "planning to",
                "like ", "love ", "enjoy", "joke", "joking", "joked", "not ", "don't", "didn't",
                "doesn't", "never", "without", "?", "idea of", "thought of", "mention of",
            };
            for (var i = 0; i < words.Length; i++)
            {
                if (value.Contains(words[i]))
                    return true;
            }
            return false;
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
