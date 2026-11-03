using AICompanion.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// Guards the baked animator assets. A clipped muscle range or a looping one-shot shows up
    /// in the player as a broken pose, so these checks run without a scene.
    /// </summary>
    public class AnimatorBakeTests
    {
        const string ControllerPath = "Assets/_Project/Animations/Companion.controller";
        const string ClipsPath = "Assets/_Project/Animations/";

        static readonly string[] OneShotClips = { "Wave", "Nod", "Shrug", "Laugh", "Turn180" };
        static readonly string[] LoopingClips = { "Idle", "Walk", "Fall" };

        static AnimatorController LoadController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, "Missing " + ControllerPath);
            return controller;
        }

        static AnimationClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipsPath + name + ".anim");
            Assert.IsNotNull(clip, "Missing clip " + name);
            return clip;
        }

        [Test]
        public void BaseEmotionStatesWriteEveryMuscle()
        {
            var controller = LoadController();
            var states = controller.layers[0].stateMachine.states;

            Assert.Greater(states.Length, 0);

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                Assert.IsTrue(
                    state.writeDefaultValues,
                    "State " + state.name + " must write default values, or a previous pose leaks through.");
            }
        }

        [Test]
        public void OneShotGesturesPlayOnce()
        {
            for (var i = 0; i < OneShotClips.Length; i++)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(LoadClip(OneShotClips[i]));
                Assert.IsFalse(settings.loopTime, OneShotClips[i] + " must not loop.");
            }
        }

        [Test]
        public void MoodsAndLocomotionLoop()
        {
            for (var i = 0; i < LoopingClips.Length; i++)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(LoadClip(LoopingClips[i]));
                Assert.IsTrue(settings.loopTime, LoopingClips[i] + " must loop.");
            }
        }

        [Test]
        public void TheIdleClipWritesTheArmsSoTheyCannotTpose()
        {
            var emotions = new[] { "Idle" };
            for (var i = 0; i < emotions.Length; i++)
            {
                var clip = LoadClip(emotions[i]);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var writesArms = false;
                for (var b = 0; b < bindings.Length; b++)
                {
                    if (bindings[b].propertyName.Contains("Arm Down-Up"))
                    {
                        writesArms = true;
                        break;
                    }
                }

                Assert.IsTrue(
                    writesArms,
                    clip.name + " must write the arms; the base state resets unanimated muscles to a T-pose.");
            }
        }

        [Test]
        public void TheUpperBodyLayerHasAnAvatarMask()
        {
            var controller = LoadController();
            Assert.Greater(controller.layers.Length, AvatarAnimatorLayers.UpperBody, "The upper body gesture layer is missing.");
            Assert.AreEqual(AnimatorLayerBlendingMode.Override, controller.layers[AvatarAnimatorLayers.UpperBody].blendingMode);
            Assert.IsNotNull(controller.layers[AvatarAnimatorLayers.UpperBody].avatarMask, "The upper body layer has no avatar mask.");
            Assert.IsNull(controller.layers[AvatarAnimatorLayers.Gestures].avatarMask, "The walk layer must not carry the upper body mask; it hides her legs.");
        }

        [Test]
        public void GesturesLiveOnTheUpperBodyLayer()
        {
            var controller = LoadController();
            Assert.Greater(controller.layers.Length, AvatarAnimatorLayers.UpperBody);

            var names = new System.Collections.Generic.List<string>();
            var states = controller.layers[AvatarAnimatorLayers.UpperBody].stateMachine.states;
            for (var i = 0; i < states.Length; i++)
                names.Add(states[i].state.name);

            Assert.Contains("Empty", names);
            Assert.Contains("Wave", names);
            Assert.Contains("Nod", names);
            Assert.Contains("Shrug", names);
            Assert.Contains("Laugh", names);

            var body = new System.Collections.Generic.List<string>();
            var bodyStates = controller.layers[AvatarAnimatorLayers.Gestures].stateMachine.states;
            for (var i = 0; i < bodyStates.Length; i++)
                body.Add(bodyStates[i].state.name);

            Assert.Contains("Walk", body);
            Assert.Contains("Turn180", body);
            Assert.Contains("Fall", body);
            Assert.IsFalse(body.Contains("Wave"), "Wave must live on the upper body layer only.");
            Assert.IsFalse(body.Contains("Laugh"), "Laugh must live on the upper body layer only.");
        }

        [Test]
        public void NoClipLiftsHerOffTheGround()
        {
            var clips = System.IO.Directory.GetFiles("Assets/_Project/Animations", "*.anim");
            Assert.Greater(clips.Length, 0);

            for (var i = 0; i < clips.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clips[i].Replace('\\', '/'));
                if (clip == null)
                    continue;

                var bindings = AnimationUtility.GetCurveBindings(clip);
                for (var b = 0; b < bindings.Length; b++)
                {
                    var binding = bindings[b];
                    Assert.IsFalse(
                        binding.propertyName.StartsWith("Root T"),
                        clip.name + " animates " + binding.propertyName + "; root translation belongs to the presence.");
                    Assert.IsFalse(
                        binding.type == typeof(Transform) && binding.propertyName.StartsWith("m_LocalPosition"),
                        clip.name + " animates transform position " + binding.path + ", which lifts her off the ground line.");
                }
            }
        }
    }
}
