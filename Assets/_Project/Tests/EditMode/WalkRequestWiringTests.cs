using System.Collections.Generic;
using System.Reflection;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using AICompanion.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// A reply's cue has to reach <see cref="DesktopPresence"/> and move her.
    /// ChatConversation stores <see cref="CueStreamFilter.Cue"/> and ChatPanel passes
    /// that to <see cref="Director.Play"/>. The Windows player then runs the same
    /// <see cref="DesktopPresence.Advance"/> from LateUpdate. EditMode has no window
    /// chrome, so the test passes the rect the player would have read.
    /// </summary>
    public class WalkRequestWiringTests
    {
        const int WindowWidth = 480;
        const int WindowHeight = 720;
        const int AreaRight = 1920;
        const int AreaBottom = 720;

        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void ReplyWalkLeft_MovesThePresenceAndDoesNotPlayTheCatalogClip()
        {
            var host = Track(new GameObject("walk-host"));
            var avatar = host.AddComponent<RecordingAvatar>();
            var legs = host.AddComponent<RecordingLocomotion>();
            var director = host.AddComponent<Director>();
            var presence = host.AddComponent<DesktopPresence>();
            AssignCatalog(director, WalkCatalog());

            Assert.IsNotNull(host.GetComponent<IWalkRequest>());
            Assert.AreSame(presence, host.GetComponent<IWalkRequest>());

            // She is already standing, which is the player after the first grounded frames.
            Step(presence, 0.1f, 800, true);
            Step(presence, 0.1f, 800, false);
            Assert.AreEqual(PresenceState.Idle, presence.Model.State);
            var start = presence.Model.X;

            var filter = new CueStreamFilter();
            filter.Push("On my way.\n<animation emotion=\"neutral\" gesture=\"Walk\" direction=\"Left\" />");
            filter.Flush();
            Assert.AreEqual("On my way.", filter.Text);
            Assert.AreEqual("walk", filter.Cue.Gesture);
            Assert.AreEqual(-1f, filter.Cue.Direction, 0.0001f);

            LogAssert.Expect(LogType.Log, "Walk: request received dir=-1 secs=12");
            LogAssert.Expect(LogType.Log, "AnimationDirector: cue walk -> requested");
            director.Play(filter.Cue);

            var previous = start;
            for (var i = 0; i < 5; i++)
            {
                Step(presence, 0.1f, 800, false);
                Assert.AreEqual(PresenceState.Walking, presence.Model.State, "Idle must not replace a requested walk.");
                Assert.Less(presence.Model.X, previous);
                previous = presence.Model.X;
            }

            Assert.Less(presence.Model.X, start - 30f);
            Assert.IsFalse(presence.Model.FacingRight);
            Assert.IsTrue(legs.Walking);
            Assert.AreEqual(-1f, legs.Direction, 0.0001f);
            Assert.AreEqual(0, avatar.PlayCalls, "The walk row must not play the on-the-spot clip.");

            LogAssert.Expect(LogType.Log, "AnimationDirector: cue stop -> requested");
            var stop = new CueStreamFilter();
            stop.Push("Okay.\n<animation emotion=\"neutral\" gesture=\"stop\" />");
            stop.Flush();
            director.Play(stop.Cue);

            var stopped = presence.Model.X;
            for (var i = 0; i < 3; i++)
                Step(presence, 0.1f, 800, false);

            Assert.AreEqual(PresenceState.Idle, presence.Model.State);
            Assert.AreEqual(stopped, presence.Model.X, 0.001f);
            Assert.IsFalse(legs.Walking);
            Assert.AreEqual(0f, legs.Direction, 0.0001f);
            Assert.AreEqual(0, avatar.PlayCalls);
        }

        [Test]
        public void ReplyWalk_OnAnotherObject_StillMovesThatPresence()
        {
            var directorHost = Track(new GameObject("director-only"));
            var presenceHost = Track(new GameObject("presence-only"));
            directorHost.AddComponent<RecordingAvatar>();
            var legs = presenceHost.AddComponent<RecordingLocomotion>();
            var director = directorHost.AddComponent<Director>();
            var presence = presenceHost.AddComponent<DesktopPresence>();
            AssignCatalog(director, WalkCatalog());

            Assert.IsNull(directorHost.GetComponent<IWalkRequest>());
            Assert.AreSame(presence, presenceHost.GetComponent<IWalkRequest>());

            Step(presence, 0.1f, 800, true);
            Step(presence, 0.1f, 800, false);
            var start = presence.Model.X;

            var filter = new CueStreamFilter();
            filter.Push("Going.\n<animation emotion=\"neutral\" gesture=\"walk\" direction=\"right\" />");
            filter.Flush();

            LogAssert.Expect(LogType.Log, "Walk: request received dir=1 secs=12");
            LogAssert.Expect(LogType.Log, "AnimationDirector: cue walk -> requested");
            director.Play(filter.Cue);
            Step(presence, 0.1f, 800, false);

            Assert.AreEqual(PresenceState.Walking, presence.Model.State);
            Assert.Greater(presence.Model.X, start);
            Assert.IsTrue(presence.Model.FacingRight);
            Assert.IsTrue(legs.Walking);
            Assert.AreEqual(1f, legs.Direction, 0.0001f);
        }

        [Test]
        public void ReplyWalk_BeforeTheModelExists_SurvivesTheFallAndThenMoves()
        {
            var host = Track(new GameObject("falling-host"));
            host.AddComponent<RecordingAvatar>();
            var legs = host.AddComponent<RecordingLocomotion>();
            var director = host.AddComponent<Director>();
            var presence = host.AddComponent<DesktopPresence>();
            AssignCatalog(director, WalkCatalog());

            var filter = new CueStreamFilter();
            filter.Push("Coming.\n<animation emotion=\"neutral\" gesture=\"walk_right\" />");
            filter.Flush();
            Assert.AreEqual("walk", filter.Cue.Gesture);
            Assert.AreEqual(1f, filter.Cue.Direction, 0.0001f);

            LogAssert.Expect(LogType.Log, "Walk: request received dir=1 secs=12");
            LogAssert.Expect(LogType.Log, "AnimationDirector: cue walk -> requested");
            director.Play(filter.Cue);
            Assert.IsNull(presence.Model);

            // Window is in the air: creating the model must not drop the request.
            var first = FallStep(presence, 0.1f);
            Assert.AreEqual(PresenceState.Falling, presence.Model.State);
            Assert.AreEqual(0f, presence.Model.X, 0.001f);
            Assert.IsFalse(first.Walking);

            var moved = false;
            for (var i = 0; i < 40 && !moved; i++)
            {
                FallStep(presence, 0.1f);
                moved = presence.Model.X > 20f;
            }

            Assert.IsTrue(moved, "The queued walk was dropped when the model was created.");
            Assert.AreEqual(PresenceState.Walking, presence.Model.State);
            Assert.IsTrue(legs.Walking);
            Assert.AreEqual(1f, legs.Direction, 0.0001f);
        }

        [Test]
        public void WalkCue_WithoutAPresence_LogsAndDoesNotPlayTheCatalogClip()
        {
            var host = Track(new GameObject("no-presence"));
            var avatar = host.AddComponent<RecordingAvatar>();
            var director = host.AddComponent<Director>();
            AssignCatalog(director, WalkCatalog());

            LogAssert.Expect(LogType.Log, "Walk: no IWalkRequest found");
            director.Play(new AnimationCue(Emotion.Neutral, 0.5f, "walk", -1f));

            Assert.AreEqual(0, avatar.PlayCalls);
            Assert.AreEqual(Emotion.Neutral, avatar.Emotion);

            var legs = host.AddComponent<RecordingLocomotion>();
            var presence = host.AddComponent<DesktopPresence>();
            Step(presence, 0.1f, 800, true);
            Step(presence, 0.1f, 800, false);
            var start = presence.Model.X;

            LogAssert.Expect(LogType.Log, "Walk: request received dir=-1 secs=12");
            LogAssert.Expect(LogType.Log, "AnimationDirector: cue walk -> requested");
            director.Play(new AnimationCue(Emotion.Neutral, 0.5f, "walk_left"));
            Step(presence, 0.1f, 800, false);

            Assert.Less(presence.Model.X, start);
            Assert.IsTrue(legs.Walking);
            Assert.AreEqual(0, avatar.PlayCalls);
        }

        static PresenceFrame Step(DesktopPresence presence, float seconds, int left, bool dragging)
        {
            return presence.Advance(
                seconds,
                left,
                0,
                left + WindowWidth,
                WindowHeight,
                0,
                0,
                AreaRight,
                AreaBottom,
                dragging);
        }

        /// <summary>A short window high above a tall work area, so the first step is a fall.</summary>
        static PresenceFrame FallStep(DesktopPresence presence, float seconds)
        {
            return presence.Advance(seconds, 400, 0, 500, 100, 0, 0, 1920, 2000, false);
        }

        AnimationClipCatalog WalkCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<AnimationClipCatalog>();
            catalog.SetEntries(new[]
            {
                new AnimationEntry
                {
                    id = "walk",
                    description = "walk",
                    emotion = Emotion.Neutral,
                    intensity = 0.5f,
                    gesture = "walk",
                    animatorState = "Walk",
                },
                new AnimationEntry
                {
                    id = "stop",
                    description = "stop",
                    emotion = Emotion.Neutral,
                    intensity = 0.3f,
                    gesture = "stop",
                },
            });
            _created.Add(catalog);
            return catalog;
        }

        GameObject Track(GameObject host)
        {
            _created.Add(host);
            return host;
        }

        static void AssignCatalog(Director director, AnimationClipCatalog catalog)
        {
            typeof(Director)
                .GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, catalog);
        }
    }

    sealed class RecordingLocomotion : MonoBehaviour, IAvatarLocomotion
    {
        public bool Walking;
        public float Direction;
        public bool Falling;

        public bool IsFalling
        {
            get { return Falling; }
        }

        public void SetWalking(bool walking)
        {
            Walking = walking;
        }

        public void SetFalling(bool falling)
        {
        }

        public void SetWalkingDirection(float direction)
        {
            Direction = direction;
        }
    }
}
