using System;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Avatar
{
    /// <summary>
    /// Face poses loaded from <c>ExpressionPresets.json</c>. Each emotion is a list of
    /// channel weights. A missing emotion, channel, or file leaves that part at zero.
    /// </summary>
    public sealed class ExpressionPresetLibrary
    {
        public const string ResourceName = "ExpressionPresets";

        static ExpressionPresetLibrary _active;

        readonly FacePose[] _poses;

        ExpressionPresetLibrary(FacePose[] poses)
        {
            _poses = poses ?? new FacePose[0];
        }

        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _poses.Length; i++)
                {
                    if (!IsZero(_poses[i]))
                        count++;
                }

                return count;
            }
        }

        public static ExpressionPresetLibrary Active
        {
            get
            {
                if (_active == null)
                    _active = Load();
                return _active;
            }
        }

        public static void SetActiveForTests(ExpressionPresetLibrary library)
        {
            _active = library ?? Load();
        }

        public static void ResetActive()
        {
            _active = null;
        }

        public FacePose Get(Emotion emotion)
        {
            var index = (int)emotion;
            if (index < 0 || index >= _poses.Length)
                return default;
            return _poses[index];
        }

        public static ExpressionPresetLibrary Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                var parsed = Parse(asset.text);
                if (parsed.Count > 0)
                    return parsed;
            }

#if UNITY_EDITOR
            var path = System.IO.Path.Combine(Application.dataPath, "_Project/Resources/ExpressionPresets.json");
            if (System.IO.File.Exists(path))
            {
                var parsed = Parse(System.IO.File.ReadAllText(path));
                if (parsed.Count > 0)
                    return parsed;
            }
#endif
            return Parse(null);
        }

        public static ExpressionPresetLibrary Parse(string json)
        {
            var poses = new FacePose[16];
            if (string.IsNullOrWhiteSpace(json))
                return new ExpressionPresetLibrary(poses);

            ExpressionPresetFile file;
            try
            {
                file = JsonUtility.FromJson<ExpressionPresetFile>(json);
            }
            catch (Exception)
            {
                return new ExpressionPresetLibrary(poses);
            }

            if (file == null || file.poses == null)
                return new ExpressionPresetLibrary(poses);

            for (var i = 0; i < file.poses.Length; i++)
            {
                var row = file.poses[i];
                if (row == null || !EmotionNames.TryParse(row.emotion, out var emotion))
                    continue;

                var index = (int)emotion;
                if (index < 0 || index >= poses.Length)
                    continue;

                var pose = poses[index];
                var weights = row.weights;
                if (weights != null)
                {
                    for (var w = 0; w < weights.Length; w++)
                    {
                        var weight = weights[w];
                        if (weight == null || !TryChannel(weight.channel, out var channel))
                            continue;
                        pose.Set(channel, Sanitize(weight.weight));
                    }
                }

                poses[index] = pose;
            }

            return new ExpressionPresetLibrary(poses);
        }

        public static bool TryChannel(string channel, out ExpressionPreset preset)
        {
            preset = ExpressionPreset.None;
            if (string.IsNullOrWhiteSpace(channel))
                return false;

            switch (channel.Trim().ToLowerInvariant())
            {
                case "aa":
                    preset = ExpressionPreset.Aa;
                    return true;
                case "ih":
                    preset = ExpressionPreset.Ih;
                    return true;
                case "ou":
                    preset = ExpressionPreset.Ou;
                    return true;
                case "ee":
                    preset = ExpressionPreset.Ee;
                    return true;
                case "oh":
                    preset = ExpressionPreset.Oh;
                    return true;
                case "blink":
                    preset = ExpressionPreset.Blink;
                    return true;
                case "joy":
                    preset = ExpressionPreset.Joy;
                    return true;
                case "angry":
                    preset = ExpressionPreset.Angry;
                    return true;
                case "sorrow":
                    preset = ExpressionPreset.Sorrow;
                    return true;
                case "fun":
                    preset = ExpressionPreset.Fun;
                    return true;
                case "surprised":
                    preset = ExpressionPreset.Surprised;
                    return true;
                case "eyesquint":
                    preset = ExpressionPreset.EyeSquint;
                    return true;
                case "browinnerup":
                    preset = ExpressionPreset.BrowInnerUp;
                    return true;
                case "browouterup":
                    preset = ExpressionPreset.BrowOuterUp;
                    return true;
                case "eyewide":
                    preset = ExpressionPreset.EyeWide;
                    return true;
                case "eyelookdown":
                    preset = ExpressionPreset.EyeLookDown;
                    return true;
                case "blush":
                    preset = ExpressionPreset.Blush;
                    return true;
                default:
                    return false;
            }
        }

        static float Sanitize(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        static bool IsZero(FacePose pose)
        {
            var last = (int)ExpressionPreset.Blush;
            for (var i = 1; i <= last; i++)
            {
                if (pose.Get((ExpressionPreset)i) > 0f)
                    return false;
            }

            return true;
        }
    }

    [Serializable]
    public sealed class ExpressionPresetFile
    {
        public ExpressionPresetRow[] poses;
    }

    [Serializable]
    public sealed class ExpressionPresetRow
    {
        public string emotion;
        public ExpressionChannelWeight[] weights;
    }

    [Serializable]
    public sealed class ExpressionChannelWeight
    {
        public string channel;
        public float weight;
    }
}
