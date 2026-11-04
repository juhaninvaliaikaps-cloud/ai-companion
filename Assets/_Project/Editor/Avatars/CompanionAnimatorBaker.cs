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
                PruneDeadParts(controller);
                EnsureIkPass(controller);
                EnsureGestureLayer(controller, clips);
                EnsureUpperBodyLayer(controller, clips);
                EnsureBaseDefaults(controller);
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

        /// <summary>
        /// The touch drag needs OnAnimatorIK, and Unity only calls that for layers with an IK
        /// pass. Existing controllers were baked without it, so turn it on for the base layer.
        /// </summary>
        static bool EnsureIkPass(AnimatorController controller)
        {
            var layers = controller.layers;
            if (layers == null || layers.Length == 0)
                return false;

            var baseLayer = layers[0];
            if (baseLayer.iKPass)
                return false;

            baseLayer.iKPass = true;
            layers[0] = baseLayer;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            return true;
        }

        /// <summary>
        /// Base emotion states must write every muscle. Older controllers were baked with this
        /// off, so a mood entered after Walking or Fall kept the tucked legs and raised arms,
        /// which read as floating with swimming arms. Fix existing controllers in place.
        /// </summary>
        static bool EnsureBaseDefaults(AnimatorController controller)
        {
            var layers = controller.layers;
            if (layers == null || layers.Length == 0)
                return false;

            var machine = layers[0].stateMachine;
            var states = machine != null ? machine.states : null;
            if (states == null)
                return false;

            var changed = false;
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state == null || state.writeDefaultValues)
                    continue;

                state.writeDefaultValues = true;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(controller);

            return changed;
        }

                /// <summary>
        /// The face-only controller: an Idle base state plus the gesture and upper-body layers.
        /// Talking and emotions are blendshapes now, so their parameters are not required.
        /// </summary>
        static bool ControllerLooksValid(AnimatorController controller)
        {
            var layers = controller.layers;
            if (layers == null || layers.Length <= AvatarAnimatorLayers.UpperBody)
                return false;

            var baseMachine = layers[0].stateMachine;
            var states = baseMachine != null ? baseMachine.states : null;
            if (states == null)
                return false;

            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == AvatarAnimatorStates.Idle)
                {
                    return layers[AvatarAnimatorLayers.Gestures].stateMachine != null
                        && layers[AvatarAnimatorLayers.UpperBody].stateMachine != null
                        && layers[AvatarAnimatorLayers.UpperBody].avatarMask != null;
                }
            }

            return false;
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
                if (!HasState(machine, AvatarAnimatorStates.Fall))
                {
                    AddFallState(machine, byName);
                    EditorUtility.SetDirty(controller);
                }

                return;
            }

            var empty = machine.AddState(AvatarAnimatorStates.Empty);
            empty.writeDefaultValues = false;
            machine.defaultState = empty;

            for (var i = 0; i < gestureNames.Length; i++)
            {
                var state = machine.AddState(gestureNames[i]);
                state.motion = byName[gestureNames[i]];
                state.writeDefaultValues = false;

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
        /// Gestures that only move the upper body live on their own masked layer, so a wave can
        /// play while the full-body layer keeps walking. The mask disables legs and feet.
        /// </summary>
        static void EnsureUpperBodyLayer(AnimatorController controller, AnimationClip[] clips)
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

            var mask = EnsureUpperBodyMask();
            var layers = controller.layers;
            if (layers.Length <= AvatarAnimatorLayers.UpperBody)
            {
                controller.AddLayer("Upper body");
                layers = controller.layers;
            }

            // The mask belongs to the upper layer only. An earlier bake left a copy on the
            // gesture layer back when the indices were different, which masked out her legs and
            // made the walk clip move only the arms.
            if (layers.Length > AvatarAnimatorLayers.Gestures)
            {
                var gesture = layers[AvatarAnimatorLayers.Gestures];
                if (gesture.avatarMask != null)
                {
                    gesture.avatarMask = null;
                    layers[AvatarAnimatorLayers.Gestures] = gesture;
                }
            }

            var baseLayer = layers[AvatarAnimatorLayers.Base];
            if (baseLayer.avatarMask != null)
            {
                baseLayer.avatarMask = null;
                layers[AvatarAnimatorLayers.Base] = baseLayer;
            }

            var upper = layers[AvatarAnimatorLayers.UpperBody];
            upper.blendingMode = AnimatorLayerBlendingMode.Override;
            upper.defaultWeight = 1f;
            upper.avatarMask = mask;
            layers[AvatarAnimatorLayers.UpperBody] = upper;
            controller.layers = layers;

            var machine = controller.layers[AvatarAnimatorLayers.UpperBody].stateMachine;
            if (machine.states == null || machine.states.Length <= 1)
            {
                var empty = machine.AddState(AvatarAnimatorStates.Empty);
                empty.writeDefaultValues = false;
                machine.defaultState = empty;

                for (var i = 0; i < gestureNames.Length; i++)
                {
                    var state = machine.AddState(gestureNames[i]);
                    state.motion = byName[gestureNames[i]];
                    state.writeDefaultValues = false;

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
            }

            PruneFullBodyGestures(controller, gestureNames);
            EditorUtility.SetDirty(controller);
        }

        static AvatarMask EnsureUpperBodyMask()
        {
            var path = Folder + "/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            return mask;
        }

        /// <summary>
        /// The full-body gesture layer used to hold the gestures too. Leaving them there as well
        /// would fire the same trigger on two layers at once, so they are removed from it.
        /// </summary>
        static void PruneFullBodyGestures(AnimatorController controller, string[] names)
        {
            var layers = controller.layers;
            if (layers.Length <= AvatarAnimatorLayers.Gestures)
                return;

            var machine = layers[AvatarAnimatorLayers.Gestures].stateMachine;
            var states = machine != null ? machine.states : null;
            if (states == null)
                return;

            for (var i = states.Length - 1; i >= 0; i--)
            {
                var state = states[i].state;
                if (state == null)
                    continue;

                for (var n = 0; n < names.Length; n++)
                {
                    if (state.name == names[n])
                    {
                        machine.RemoveState(state);
                        break;
                    }
                }
            }
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

                /// <summary>
        /// A fresh controller for the face-only design: one Idle base state, with the gesture and
        /// upper-body layers added by the ensure passes. No talking or emotion parameters, because
        /// the face and the portrait show those.
        /// </summary>
        static void CreateController(AnimationClip[] clips)
        {
            var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            for (var i = 0; i < clips.Length; i++)
                byName[clips[i].name] = clips[i];

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var baseMachine = controller.layers[0].stateMachine;
            var idle = baseMachine.AddState(AvatarAnimatorStates.Idle);
            idle.motion = byName["Idle"];
            idle.writeDefaultValues = true;
            baseMachine.defaultState = idle;

            // The touch drag needs OnAnimatorIK on a layer with an IK pass.
            var layers = controller.layers;
            var baseLayer = layers[0];
            baseLayer.iKPass = true;
            layers[0] = baseLayer;
            controller.layers = layers;
        }

        /// <summary>
        /// Removes what the face-only design retired: the Talk layer, the body emotion states and
        /// their parameters. Runs on an existing controller so old projects clean up in place.
        /// </summary>
        static bool PruneDeadParts(AnimatorController controller)
        {
            var changed = false;

            var layers = controller.layers;
            for (var i = layers.Length - 1; i >= 1; i--)
            {
                if (!string.Equals(layers[i].name, "Talk", System.StringComparison.Ordinal))
                    continue;

                controller.RemoveLayer(i);
                changed = true;
                layers = controller.layers;
            }

            var dead = new[]
            {
                AvatarAnimatorStates.Happy,
                AvatarAnimatorStates.Sad,
                AvatarAnimatorStates.Surprised,
                AvatarAnimatorStates.Thinking,
                AvatarAnimatorStates.Silent,
            };

            var baseMachine = controller.layers[0].stateMachine;
            var states = baseMachine.states;
            for (var i = states.Length - 1; i >= 0; i--)
            {
                var state = states[i].state;
                if (state == null)
                    continue;

                for (var d = 0; d < dead.Length; d++)
                {
                    if (state.name == dead[d])
                    {
                        baseMachine.RemoveState(state);
                        changed = true;
                        break;
                    }
                }
            }

            var obsolete = new[]
            {
                AvatarAnimatorParameters.Talking,
                AvatarAnimatorParameters.Emotion,
                AvatarAnimatorParameters.EmotionIntensity,
            };

            for (var p = controller.parameters.Length - 1; p >= 0; p--)
            {
                var name = controller.parameters[p].name;
                for (var o = 0; o < obsolete.Length; o++)
                {
                    if (name == obsolete[o])
                    {
                        controller.RemoveParameter(p);
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
                EditorUtility.SetDirty(controller);

            return changed;
        }

        static void AddEmotion(AnimatorStateMachine machine, string stateName, Motion motion, int emotionValue, bool defaultState)
        {
            var state = machine.AddState(stateName);
            state.motion = motion;

            // Base emotion states must write every muscle, not only the ones their clip
            // animates. With this off, a mood entered after Walking or Fall kept its tucked
            // legs and raised arms, which read as floating with swimming arms.
            state.writeDefaultValues = true;
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
