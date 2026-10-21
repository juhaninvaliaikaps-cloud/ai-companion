using System.IO;
using AICompanion.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AICompanion.Editor.Avatars
{
    /// <summary>
    /// Rewrites the shared humanoid clips through the animation API and builds
    /// Companion.controller when that asset is missing. The committed YAML is what
    /// Play mode uses after a normal import. Run the menu if a clip stays on the bind pose.
    /// </summary>
    static class CompanionAnimatorBaker
    {
        const string Folder = "Assets/_Project/Animations";
        const string ControllerPath = Folder + "/Companion.controller";

        static readonly string[] ClipNames =
        {
            "Idle",
            "Talk",
            "Happy",
            "Sad",
            "Surprised",
            "Thinking"
        };

        [MenuItem("AI Companion/Rebuild Basic Animator")]
        public static void RebuildFromMenu()
        {
            Rebuild(force: true);
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt the Companion animator clips in " + Folder + ".");
        }

        public static void Curve(AnimationClip clip, string muscle, float duration, float center, float amplitude, float cycles, float phase)
        {
            Keyframe[] keys;
            if (Mathf.Abs(amplitude) < 1e-8f || Mathf.Abs(cycles) < 1e-8f)
            {
                keys = new[]
                {
                    new Keyframe(0f, center, 0f, 0f),
                    new Keyframe(duration, center, 0f, 0f)
                };
            }
            else
            {
                const int steps = 8;
                keys = new Keyframe[steps + 1];
                var omega = cycles * Mathf.PI * 2f / duration;
                for (var i = 0; i <= steps; i++)
                {
                    var time = duration * i / steps;
                    var angle = omega * time + phase;
                    var value = center + amplitude * Mathf.Sin(angle);
                    var slope = amplitude * omega * Mathf.Cos(angle);
                    keys[i] = new Keyframe(time, value, slope, slope);
                }
            }

            var binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle);
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }

        static void Rebuild(bool force)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Animations"));
            var clips = new AnimationClip[ClipNames.Length];
            for (var i = 0; i < ClipNames.Length; i++)
                clips[i] = EnsureClip(ClipNames[i], force);

            EnsureController(clips);
        }

        static AnimationClip EnsureClip(string clipName, bool force)
        {
            var path = Folder + "/" + clipName + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!force && clip != null && AnimationUtility.GetCurveBindings(clip).Length > 0)
                return clip;

            var created = clip == null;
            if (created)
                clip = new AnimationClip();

            clip.name = clipName;
            clip.frameRate = 30f;
            clip.legacy = false;
            clip.ClearCurves();
            CompanionClipCurves.Populate(clip);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);

            return clip;
        }

        static void EnsureController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                if (!ControllerLooksValid(controller))
                    Debug.LogWarning("Companion.controller did not import with Idle, Talk, and the emotion parameters. Delete that asset and run AI Companion/Rebuild Basic Animator if the body stays on the bind pose.");
                return;
            }

            if (File.Exists(Absolute(ControllerPath)))
            {
                Debug.LogWarning("Companion.controller is on disk but did not import. The committed YAML was left in place.");
                return;
            }

            CreateController(clips);
        }

        static bool ControllerLooksValid(AnimatorController controller)
        {
            if (controller.layers == null || controller.layers.Length < 2)
                return false;

            var talking = false;
            var emotion = false;
            var intensity = false;
            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.name == AvatarAnimatorParameters.Talking && parameter.type == AnimatorControllerParameterType.Bool)
                    talking = true;
                else if (parameter.name == AvatarAnimatorParameters.Emotion && parameter.type == AnimatorControllerParameterType.Int)
                    emotion = true;
                else if (parameter.name == AvatarAnimatorParameters.EmotionIntensity && parameter.type == AnimatorControllerParameterType.Float)
                    intensity = true;
            }

            if (!talking || !emotion || !intensity)
                return false;

            var states = controller.layers[0].stateMachine.states;
            return states != null && states.Length >= 5;
        }

        static void CreateController(AnimationClip[] clips)
        {
            var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            for (var i = 0; i < clips.Length; i++)
                byName[clips[i].name] = clips[i];

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(AvatarAnimatorParameters.Talking, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AvatarAnimatorParameters.Emotion, AnimatorControllerParameterType.Int);
            controller.AddParameter(AvatarAnimatorParameters.EmotionIntensity, AnimatorControllerParameterType.Float);

            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != AvatarAnimatorParameters.EmotionIntensity)
                    continue;
                parameters[i].defaultFloat = 1f;
            }

            controller.parameters = parameters;

            var baseMachine = controller.layers[0].stateMachine;
            AddEmotion(baseMachine, AvatarAnimatorStates.Idle, byName["Idle"], 0, true);
            AddEmotion(baseMachine, AvatarAnimatorStates.Happy, byName["Happy"], 1, false);
            AddEmotion(baseMachine, AvatarAnimatorStates.Sad, byName["Sad"], 2, false);
            AddEmotion(baseMachine, AvatarAnimatorStates.Surprised, byName["Surprised"], 3, false);
            AddEmotion(baseMachine, AvatarAnimatorStates.Thinking, byName["Thinking"], 4, false);

            controller.AddLayer("Talk");
            var layers = controller.layers;
            var talkLayer = layers[1];
            talkLayer.blendingMode = AnimatorLayerBlendingMode.Additive;
            talkLayer.defaultWeight = 1f;
            layers[1] = talkLayer;
            controller.layers = layers;

            var talkMachine = controller.layers[1].stateMachine;
            var silent = talkMachine.AddState(AvatarAnimatorStates.Silent);
            silent.writeDefaultValues = false;
            var talk = talkMachine.AddState(AvatarAnimatorStates.Talk);
            talk.motion = byName["Talk"];
            talk.writeDefaultValues = false;
            talkMachine.defaultState = silent;

            var toTalk = silent.AddTransition(talk);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, AvatarAnimatorParameters.Talking);
            toTalk.hasExitTime = false;
            toTalk.hasFixedDuration = true;
            toTalk.duration = 0.25f;
            toTalk.canTransitionToSelf = false;

            var toSilent = talk.AddTransition(silent);
            toSilent.AddCondition(AnimatorConditionMode.IfNot, 0f, AvatarAnimatorParameters.Talking);
            toSilent.hasExitTime = false;
            toSilent.hasFixedDuration = true;
            toSilent.duration = 0.25f;
            toSilent.canTransitionToSelf = false;
        }

        static void AddEmotion(AnimatorStateMachine machine, string stateName, Motion motion, int emotionValue, bool defaultState)
        {
            var state = machine.AddState(stateName);
            state.motion = motion;
            state.writeDefaultValues = false;
            if (defaultState)
                machine.defaultState = state;

            var transition = machine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.Equals, emotionValue, AvatarAnimatorParameters.Emotion);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = AvatarAnimatorRules.TransitionSeconds;
            transition.canTransitionToSelf = false;
        }

        static string Absolute(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }
    }
}
