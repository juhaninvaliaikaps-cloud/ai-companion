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
            "Thinking",
            "Wave",
            "Nod",
            "Shrug",
            "Laugh",
            "Walk",
            "Turn180",
            "Fall"
        };

        /// <summary>Clips that loop. Gestures and the turn play once.</summary>
        static readonly string[] LoopingClips =
        {
            "Idle",
            "Talk",
            "Happy",
            "Sad",
            "Surprised",
            "Thinking",
            "Walk",
            "Fall"
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

        /// <summary>
        /// Writes explicit keyframes for one muscle. Used by gestures, where a sine is
        /// the wrong shape: a wave is a raise, two wags, and a drop.
        /// </summary>
        public static void Keys(AnimationClip clip, string muscle, params (float time, float value)[] samples)
        {
            var keys = new Keyframe[samples.Length];
            for (var i = 0; i < samples.Length; i++)
                keys[i] = new Keyframe(samples[i].time, samples[i].value, 0f, 0f);

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
            settings.loopTime = IsLooping(clipName);
            settings.loopBlend = IsLooping(clipName);
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (created)
                AssetDatabase.CreateAsset(clip, path);
            else
                EditorUtility.SetDirty(clip);

            return clip;
        }

        static bool IsLooping(string clipName)
        {
            for (var i = 0; i < LoopingClips.Length; i++)
            {
                if (LoopingClips[i] == clipName)
                    return true;
            }

            return false;
        }

        static void EnsureController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                EnsureGestureLayer(controller, clips);
                if (!ControllerLooksValid(controller))
                    Debug.LogWarning("Companion.controller did not import with Idle, Talk, the emotion parameters, and the gesture layer. Delete that asset and run AI Companion/Rebuild Basic Animator if the body stays on the bind pose.");
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
            if (states == null || states.Length < 5)
                return false;

            if (controller.layers.Length <= AvatarAnimatorLayers.Gestures)
                return false;

            var gestureStates = controller.layers[AvatarAnimatorLayers.Gestures].stateMachine.states;
            return gestureStates != null && gestureStates.Length >= 7;
        }

        /// <summary>
        /// Adds the gesture layer to an existing controller. The layer is an override
        /// layer with writeDefaultValues off, so a gesture only moves the muscles its
        /// clip names and the emotion pose underneath is left alone. Gestures run here
        /// instead of on the base layer because the emotion states use AnyState
        /// transitions, and those would pull a gesture back to Idle mid-move.
        /// </summary>
        static void EnsureGestureLayer(AnimatorController controller, AnimationClip[] clips)
        {
            var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            for (var i = 0; i < clips.Length; i++)
                byName[clips[i].name] = clips[i];

            var gestureNames = new[]
            {
                AvatarAnimatorStates.Wave,
                AvatarAnimatorStates.Nod,
                AvatarAnimatorStates.Shrug,
                AvatarAnimatorStates.Laugh
            };

            var parameters = new System.Collections.Generic.HashSet<string>();
            var existing = controller.parameters;
            for (var i = 0; i < existing.Length; i++)
                parameters.Add(existing[i].name);

            for (var i = 0; i < gestureNames.Length; i++)
            {
                if (!parameters.Contains(gestureNames[i]))
                    controller.AddParameter(gestureNames[i], AnimatorControllerParameterType.Trigger);
            }

            if (!parameters.Contains(AvatarAnimatorStates.Turn180))
                controller.AddParameter(AvatarAnimatorStates.Turn180, AnimatorControllerParameterType.Trigger);
            if (!parameters.Contains("Walking"))
                controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            if (!parameters.Contains(AvatarAnimatorParameters.Falling))
                controller.AddParameter(AvatarAnimatorParameters.Falling, AnimatorControllerParameterType.Bool);

            var layers = controller.layers;
            if (layers.Length <= AvatarAnimatorLayers.Gestures)
            {
                controller.AddLayer("Gestures");
                layers = controller.layers;
                var layer = layers[AvatarAnimatorLayers.Gestures];
                layer.blendingMode = AnimatorLayerBlendingMode.Override;
                layer.defaultWeight = 1f;
                layers[AvatarAnimatorLayers.Gestures] = layer;
                controller.layers = layers;
            }

            var machine = controller.layers[AvatarAnimatorLayers.Gestures].stateMachine;
            if (machine.states != null && machine.states.Length > 1)
            {
                var changed = false;
                if (!HasState(machine, AvatarAnimatorStates.Fall))
                {
                    AddFallState(machine, byName);
                    changed = true;
                }

                changed |= AddWalkInterrupts(machine);

                if (changed)
                    EditorUtility.SetDirty(controller);

                return;
            }

            var empty = machine.AddState(AvatarAnimatorStates.Empty);
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            var gestureStates = new AnimatorState[gestureNames.Length];
            for (var i = 0; i < gestureNames.Length; i++)
            {
                var state = machine.AddState(gestureNames[i]);
                state.motion = byName[gestureNames[i]];
                state.writeDefaultValues = false;
                gestureStates[i] = state;

                var enter = empty.AddTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, gestureNames[i]);
                enter.hasExitTime = false;
                enter.hasFixedDuration = true;
                enter.duration = 0.15f;
                enter.canTransitionToSelf = false;

                var leave = state.AddTransition(empty);
                leave.hasExitTime = true;
                leave.exitTime = 0.95f;
                leave.hasFixedDuration = true;
                leave.duration = 0.2f;
                leave.canTransitionToSelf = false;
            }

            var walk = machine.AddState(AvatarAnimatorStates.Walk);
            walk.motion = byName["Walk"];
            walk.writeDefaultValues = false;
            var toWalk = empty.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");
            toWalk.hasExitTime = false;
            toWalk.hasFixedDuration = true;
            toWalk.duration = 0.2f;
            var fromWalk = walk.AddTransition(empty);
            fromWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");
            fromWalk.hasExitTime = false;
            fromWalk.hasFixedDuration = true;
            fromWalk.duration = 0.2f;
            fromWalk.canTransitionToSelf = false;

            // A trigger that arrives while Walking is true had no transition out of the Walk
            // state, so the gesture never showed. Every gesture can now interrupt the walk.
            for (var g = 0; g < gestureStates.Length; g++)
            {
                if (gestureStates[g] == null)
                    continue;

                var toGesture = walk.AddTransition(gestureStates[g]);
                toGesture.AddCondition(AnimatorConditionMode.If, 0f, gestureNames[g]);
                toGesture.hasExitTime = false;
                toGesture.hasFixedDuration = true;
                toGesture.duration = 0.12f;
                toGesture.canTransitionToSelf = false;
            }

            var turn = machine.AddState(AvatarAnimatorStates.Turn180);
            turn.motion = byName["Turn180"];
            turn.writeDefaultValues = false;
            var toTurn = empty.AddTransition(turn);
            toTurn.AddCondition(AnimatorConditionMode.If, 0f, AvatarAnimatorStates.Turn180);
            toTurn.hasExitTime = false;
            toTurn.hasFixedDuration = true;
            toTurn.duration = 0.1f;
            toTurn.canTransitionToSelf = false;
            var fromTurn = turn.AddTransition(empty);
            fromTurn.hasExitTime = true;
            fromTurn.exitTime = 0.95f;
            fromTurn.hasFixedDuration = true;
            fromTurn.duration = 0.2f;
            fromTurn.canTransitionToSelf = false;

            AddFallState(machine, byName);

            EditorUtility.SetDirty(controller);
        }

        /// <summary>
        /// A gesture trigger that arrives while Walking is true has no transition from the
        /// Walk state, so the clip never shows. Add a Walk to gesture transition per gesture,
        /// skipping the ones that already exist so the bake stays repeatable.
        /// </summary>
        static bool AddWalkInterrupts(AnimatorStateMachine machine)
        {
            var states = machine != null ? machine.states : null;
            if (states == null)
                return false;

            AnimatorState walk = null;
            var gestures = new System.Collections.Generic.List<AnimatorState>();
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state == null)
                    continue;

                if (state.name == AvatarAnimatorStates.Walk)
                {
                    walk = state;
                    continue;
                }

                if (AvatarAnimatorStates.Wave == state.name
                    || AvatarAnimatorStates.Nod == state.name
                    || AvatarAnimatorStates.Shrug == state.name
                    || AvatarAnimatorStates.Laugh == state.name
                    || AvatarAnimatorStates.Turn180 == state.name
                    || AvatarAnimatorStates.Fall == state.name)
                {
                    gestures.Add(state);
                }
            }

            if (walk == null || gestures.Count == 0)
                return false;

            var added = false;
            for (var g = 0; g < gestures.Count; g++)
            {
                var gesture = gestures[g];
                if (HasTransition(walk, gesture.name))
                    continue;

                var transition = walk.AddTransition(gesture);
                transition.AddCondition(AnimatorConditionMode.If, 0f, gesture.name);
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = 0.12f;
                transition.canTransitionToSelf = false;
                added = true;
            }

            return added;
        }

        static bool HasTransition(AnimatorState from, string destinationName)
        {
            var transitions = from != null ? from.transitions : null;
            if (transitions == null)
                return false;

            for (var i = 0; i < transitions.Length; i++)
            {
                var target = transitions[i].destinationState;
                if (target != null && target.name == destinationName)
                    return true;
            }

            return false;
        }

        static bool HasState(AnimatorStateMachine machine, string name)
        {
            var states = machine != null ? machine.states : null;
            if (states == null)
                return false;

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state != null && state.name == name)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The drop from DesktopPresence sets the Falling bool. The clip loops while she
        /// is in the air and the pointer returns to Empty when she lands.
        /// </summary>
        static void AddFallState(AnimatorStateMachine machine, System.Collections.Generic.Dictionary<string, AnimationClip> byName)
        {
            var empty = machine != null ? machine.defaultState : null;
            if (empty == null || byName == null || !byName.ContainsKey("Fall"))
                return;
            if (HasState(machine, AvatarAnimatorStates.Fall))
                return;

            var fall = machine.AddState(AvatarAnimatorStates.Fall);
            fall.motion = byName["Fall"];
            fall.writeDefaultValues = false;

            var toFall = empty.AddTransition(fall);
            toFall.AddCondition(AnimatorConditionMode.If, 0f, AvatarAnimatorParameters.Falling);
            toFall.hasExitTime = false;
            toFall.hasFixedDuration = true;
            toFall.duration = 0.12f;
            toFall.canTransitionToSelf = false;

            var fromFall = fall.AddTransition(empty);
            fromFall.AddCondition(AnimatorConditionMode.IfNot, 0f, AvatarAnimatorParameters.Falling);
            fromFall.hasExitTime = false;
            fromFall.hasFixedDuration = true;
            fromFall.duration = 0.2f;
            fromFall.canTransitionToSelf = false;
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
