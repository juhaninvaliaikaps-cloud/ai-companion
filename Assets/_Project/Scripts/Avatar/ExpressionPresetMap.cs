using System;
using System.Collections.Generic;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Maps blendshape names onto <see cref="ExpressionPreset"/>.
    /// Accepts VRM 1.0 presets, VRM 0.x / VRoid names, the 52 ARKit names,
    /// Daz-style names such as <c>eCTRLEyeSquintL</c>, and common MMD morph names
    /// (にこり, 困る, 怒り, びっくり, まばたき, 照れ). A name that matches nothing
    /// is <see cref="ExpressionPreset.None"/> and is left alone.
    /// </summary>
    public static class ExpressionPresetMap
    {
        static readonly Dictionary<string, ExpressionPreset> Exact = BuildExact();

        public static ExpressionPreset Classify(string blendShapeName)
        {
            var normalized = Normalize(blendShapeName);
            if (normalized.Length == 0)
                return ExpressionPreset.None;

            if (Exact.TryGetValue(normalized, out var preset))
                return preset;

            if (normalized.StartsWith("vrm", StringComparison.Ordinal) && normalized.Length > 3)
            {
                var rest = normalized.Substring(3);
                if (Exact.TryGetValue(rest, out preset))
                    return preset;
            }

            if (Contains(normalized, "まばたき") || Contains(normalized, "瞬き") || Contains(normalized, "マバタキ"))
                return ExpressionPreset.Blink;
            if (Contains(normalized, "にっこり") || Contains(normalized, "にこり") || Contains(normalized, "ニッコリ") || Contains(normalized, "ニコリ") || Contains(normalized, "笑顔"))
                return ExpressionPreset.Joy;
            if (Contains(normalized, "笑い") || Contains(normalized, "わらい"))
                return ExpressionPreset.EyeSquint;
            if (Contains(normalized, "びっくり") || Contains(normalized, "ビックリ") || Contains(normalized, "驚き"))
                return ExpressionPreset.Surprised;
            if (Contains(normalized, "口角上げ") || Contains(normalized, "口角あげ"))
                return ExpressionPreset.Joy;
            if (Contains(normalized, "口角下げ") || Contains(normalized, "口角さげ"))
                return ExpressionPreset.Sorrow;
            if (Contains(normalized, "眉困り") || Contains(normalized, "眉上げ") || Contains(normalized, "眉あげ"))
                return ExpressionPreset.BrowInnerUp;
            if (Contains(normalized, "眉下げ") || Contains(normalized, "眉さげ") || Contains(normalized, "眉怒り"))
                return ExpressionPreset.Angry;
            if (Contains(normalized, "怒り") || Contains(normalized, "おこり"))
                return ExpressionPreset.Angry;
            if (Contains(normalized, "困る") || Contains(normalized, "困り") || Contains(normalized, "悲しみ") || Contains(normalized, "悲しい"))
                return ExpressionPreset.Sorrow;
            if (Contains(normalized, "なごみ") || Contains(normalized, "和み"))
                return ExpressionPreset.Fun;
            if (Contains(normalized, "照れ") || Contains(normalized, "照れる") || Contains(normalized, "赤面") || Contains(normalized, "頬染め") || Contains(normalized, "赤らめ") || Contains(normalized, "テレ"))
                return ExpressionPreset.Blush;
            if (Contains(normalized, "見開き"))
                return ExpressionPreset.EyeWide;
            if (normalized == "下" || normalized == "目下")
                return ExpressionPreset.EyeLookDown;

            if (Contains(normalized, "eyesquint") || Contains(normalized, "eyesmile") || Contains(normalized, "eyejoy") || Contains(normalized, "squint"))
                return ExpressionPreset.EyeSquint;
            if (Contains(normalized, "browinnerup") || Contains(normalized, "browsinnerup"))
                return ExpressionPreset.BrowInnerUp;
            if (Contains(normalized, "browouterup") || Contains(normalized, "browsouterup"))
                return ExpressionPreset.BrowOuterUp;
            if (Contains(normalized, "browsup") || Contains(normalized, "browraise") || Contains(normalized, "eyebrowup") || Contains(normalized, "eyebrowraise"))
                return ExpressionPreset.BrowInnerUp;
            if (Contains(normalized, "eyewide") || Contains(normalized, "eyeswide") || Contains(normalized, "wideeye"))
                return ExpressionPreset.EyeWide;
            if (Contains(normalized, "lookdown") || Contains(normalized, "eyesdown") || Contains(normalized, "eyedown"))
                return ExpressionPreset.EyeLookDown;
            if (Contains(normalized, "blush") || Contains(normalized, "cheekflush") || Contains(normalized, "facered"))
                return ExpressionPreset.Blush;

            if (normalized.StartsWith("jawopen", StringComparison.Ordinal) || Contains(normalized, "jawopen") || Contains(normalized, "mouthopen"))
                return ExpressionPreset.Aa;
            if (Contains(normalized, "mouthfunnel"))
                return ExpressionPreset.Oh;
            if (Contains(normalized, "mouthpucker"))
                return ExpressionPreset.Ou;
            if (Contains(normalized, "mouthclose"))
                return ExpressionPreset.Ih;
            if (Contains(normalized, "mouthstretch"))
                return ExpressionPreset.Ee;
            if (normalized.StartsWith("eyeblink", StringComparison.Ordinal) || normalized.StartsWith("blink", StringComparison.Ordinal)
                || Contains(normalized, "eyeblink") || Contains(normalized, "eyeclose") || Contains(normalized, "eyesclosed") || Contains(normalized, "blink"))
                return ExpressionPreset.Blink;
            if (normalized.StartsWith("mouthsmile", StringComparison.Ordinal) || Contains(normalized, "mouthsmile") || Contains(normalized, "smile") || Contains(normalized, "joy"))
                return ExpressionPreset.Joy;
            if (normalized.StartsWith("browdown", StringComparison.Ordinal) || Contains(normalized, "browdown") || Contains(normalized, "browsdown")
                || Contains(normalized, "browouterdown") || Contains(normalized, "angry") || normalized.StartsWith("anger", StringComparison.Ordinal))
                return ExpressionPreset.Angry;
            if (normalized.StartsWith("mouthfrown", StringComparison.Ordinal) || Contains(normalized, "mouthfrown") || Contains(normalized, "frown") || Contains(normalized, "sorrow") || Contains(normalized, "sadness"))
                return ExpressionPreset.Sorrow;
            if (Contains(normalized, "surprise") || Contains(normalized, "shock"))
                return ExpressionPreset.Surprised;
            if (Contains(normalized, "fun") && !Contains(normalized, "fung") && !Contains(normalized, "funnel"))
                return ExpressionPreset.Fun;

            if (normalized.Length > 1 && normalized[0] == 'm' && Exact.TryGetValue(normalized.Substring(1), out preset))
                return preset;

            return ExpressionPreset.None;
        }

        static bool Contains(string normalized, string token)
        {
            return normalized.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        public static IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> Map(IEnumerable<string> blendShapeNames)
        {
            var grouped = new Dictionary<ExpressionPreset, List<string>>();
            if (blendShapeNames == null)
                return Freeze(grouped);

            foreach (var name in blendShapeNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var preset = Classify(name);
                if (preset == ExpressionPreset.None)
                    continue;

                Add(grouped, preset, name);
            }

            return Freeze(grouped);
        }

        public static string Normalize(string blendShapeName)
        {
            if (string.IsNullOrWhiteSpace(blendShapeName))
                return string.Empty;

            var source = blendShapeName.Trim();
            var slash = source.LastIndexOf('/');
            if (slash >= 0 && slash < source.Length - 1)
                source = source.Substring(slash + 1);

            var colon = source.LastIndexOf(':');
            if (colon >= 0 && colon < source.Length - 1)
                source = source.Substring(colon + 1);

            var chars = new char[source.Length];
            var count = 0;
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                if (c == '_' || c == '-' || c == ' ' || c == '.')
                    continue;
                chars[count++] = char.ToLowerInvariant(c);
            }

            return new string(chars, 0, count);
        }

        static void Add(Dictionary<ExpressionPreset, List<string>> grouped, ExpressionPreset preset, string name)
        {
            if (!grouped.TryGetValue(preset, out var list))
            {
                list = new List<string>();
                grouped.Add(preset, list);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], name, StringComparison.Ordinal))
                    return;
            }

            list.Add(name);
        }

        static IReadOnlyDictionary<ExpressionPreset, IReadOnlyList<string>> Freeze(Dictionary<ExpressionPreset, List<string>> grouped)
        {
            var frozen = new Dictionary<ExpressionPreset, IReadOnlyList<string>>(grouped.Count);
            foreach (var pair in grouped)
                frozen.Add(pair.Key, pair.Value);
            return frozen;
        }

        static Dictionary<string, ExpressionPreset> BuildExact()
        {
            var map = new Dictionary<string, ExpressionPreset>(StringComparer.Ordinal);
            Add(map, ExpressionPreset.Aa, "aa", "a", "vrmaa", "fclmtha", "jawopen", "mouthopen", "あ", "あ2", "あ２");
            Add(map, ExpressionPreset.Ih, "ih", "i", "vrmih", "fclmthi", "mouthclose", "い");
            Add(map, ExpressionPreset.Ou, "ou", "u", "vrmou", "fclmthu", "mouthpucker", "う");
            Add(map, ExpressionPreset.Ee, "ee", "e", "vrmee", "fclmthe", "え");
            Add(map, ExpressionPreset.Oh, "oh", "o", "vrmoh", "fclmtho", "mouthfunnel", "お");
            Add(map, ExpressionPreset.Blink, "blink", "blinkleft", "blinkright", "eyeblinkleft", "eyeblinkright", "eyeclose", "eyeclosed", "eyesclosed", "fcleyeclose", "fcleyeclosel", "fcleyecloser", "まばたき", "まばたき左", "まばたき右");
            Add(map, ExpressionPreset.Joy, "joy", "happy", "fclalljoy", "fclmthjoy", "mouthsmileleft", "mouthsmileright", "mouthsmile", "にこり", "にっこり", "笑顔");
            Add(map, ExpressionPreset.Angry, "angry", "anger", "mad", "fclallangry", "fclbrwangry", "fclmthangry", "browdownleft", "browdownright", "browdown", "怒り");
            Add(map, ExpressionPreset.Sorrow, "sorrow", "sad", "sadness", "fclallsorrow", "fclbrwsorrow", "fclmthsorrow", "fcleyesorrow", "mouthfrownleft", "mouthfrownright", "mouthfrown", "困る", "悲しみ");
            Add(map, ExpressionPreset.Fun, "fun", "relaxed", "fclallfun", "fclbrwfun", "なごみ");
            Add(map, ExpressionPreset.Surprised, "surprised", "surprise", "shock", "fclallsurprised", "fclbrwsurprised", "びっくり", "驚き");
            Add(map, ExpressionPreset.EyeSquint, "eyesquint", "eyesquintleft", "eyesquintright", "eyesmile", "eyesmileleft", "eyesmileright", "fcleyejoy", "笑い");
            Add(map, ExpressionPreset.BrowInnerUp, "browinnerup", "browinnerupleft", "browinnerupright");
            Add(map, ExpressionPreset.BrowOuterUp, "browouterup", "browouterupleft", "browouterupright");
            Add(map, ExpressionPreset.EyeWide, "eyewide", "eyewideleft", "eyewideright", "eyeswide", "fcleyespread", "見開き");
            Add(map, ExpressionPreset.EyeLookDown, "eyelookdown", "eyelookdownleft", "eyelookdownright", "eyeslookdown", "eyesdown", "eyedown", "lookdown", "下");
            Add(map, ExpressionPreset.Blush, "blush", "cheekblush", "faceblush", "tere", "照れ", "赤面", "頬染め");
            return map;
        }

        static void Add(Dictionary<string, ExpressionPreset> map, ExpressionPreset preset, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
                map[names[i]] = preset;
        }
    }
}
