using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.Voice;
using NUnit.Framework;
using UnityEngine;
using Director = AICompanion.AnimationDirector.AnimationDirector;

namespace AICompanion.Tests.EditMode
{
    public class TrayMenuTests
    {
        [Test]
        public void Menu_ShowHideLabelFollowsVisibility()
        {
            var menu = new TrayMenuState();

            Assert.AreEqual("Hide companion", menu.VisibilityLabel);
            Assert.IsTrue(menu.CompanionVisible);
            var hidden = menu.Apply(TrayCommand.ToggleVisible);
            Assert.IsTrue(hidden.VisibilityChanged);
            Assert.IsFalse(menu.CompanionVisible);
            Assert.AreEqual("Show companion", menu.VisibilityLabel);
            menu.Apply(TrayCommand.ToggleVisible);
            Assert.IsTrue(menu.CompanionVisible);
        }

        [Test]
        public void Menu_PauseAndStartupAreCheckMarks()
        {
            var items = TrayMenuState.Build(true, false, false);
            Assert.AreEqual(TrayCommand.TogglePause, items[3].Command);
            Assert.IsFalse(items[3].Checked);
            Assert.IsTrue(items[3].SeparatorBefore);
            Assert.IsFalse(items[4].Checked);

            var paused = TrayMenuState.Build(false, true, true);
            Assert.AreEqual("Show companion", paused[0].Label);
            Assert.IsTrue(paused[3].Checked);
            Assert.AreEqual("Pause", paused[3].Label);
            Assert.IsTrue(paused[4].Checked);
            Assert.AreEqual("Start with Windows", paused[4].Label);
            Assert.AreEqual(TrayCommand.Quit, paused[5].Command);
            Assert.IsTrue(paused[5].SeparatorBefore);
            Assert.IsFalse(paused[5].Checked);
        }

        [Test]
        public void Menu_OpenChatAndSettingsAndQuitDoNotTogglePause()
        {
            var menu = new TrayMenuState();
            menu.Paused = true;
            menu.CompanionVisible = true;

            var chat = menu.Apply(TrayCommand.OpenChat);
            Assert.IsTrue(chat.OpenChat);
            Assert.IsFalse(chat.PauseChanged);
            Assert.IsTrue(menu.Paused);
            Assert.IsTrue(menu.CompanionVisible);

            var settings = menu.Apply(TrayCommand.OpenSettings);
            Assert.IsTrue(settings.OpenSettings);
            Assert.IsTrue(menu.Paused);

            var quit = menu.Apply(TrayCommand.Quit);
            Assert.IsTrue(quit.Quit);
            Assert.IsFalse(quit.VisibilityChanged);
            Assert.IsTrue(menu.Paused);
            Assert.IsFalse(menu.Apply(TrayCommand.None).Handled);
        }

        [Test]
        public void Menu_SettingsRaisesTheOpenHook()
        {
            var menu = new TrayMenuState();
            var calls = 0;
            menu.SettingsRequested += () => calls++;

            Assert.IsTrue(menu.Apply(TrayCommand.OpenSettings).OpenSettings);
            menu.RaiseSettings();

            Assert.AreEqual(1, calls);
            Assert.IsFalse(menu.Apply(TrayCommand.OpenSettings).PauseChanged);
        }

        [Test]
        public void Pause_BlocksSpeechListeningAndReactions()
        {
            Assert.IsTrue(CompanionPause.SpeechAllowed(false, false));
            Assert.IsFalse(CompanionPause.SpeechAllowed(true, false));
            Assert.IsFalse(CompanionPause.SpeechAllowed(false, true));
            Assert.IsFalse(CompanionPause.SpeechAllowed(true, true));
            Assert.IsFalse(CompanionPause.ListeningAllowed(true));
            Assert.IsTrue(CompanionPause.ListeningAllowed(false));
            Assert.IsFalse(CompanionPause.ReactionsAllowed(true));
            Assert.IsTrue(CompanionPause.ReactionsAllowed(false));
        }

        [Test]
        public void Pause_SuppressesDirectorCuesUntilCleared()
        {
            var host = new GameObject("tray-pause");
            try
            {
                var animator = host.AddComponent<CountingAnimator>();
                var director = host.AddComponent<Director>();
                director.Resolve();
                director.SetSuppressed(true);

                Assert.IsTrue(director.ReactionsSuppressed);
                Assert.AreEqual(Emotion.Neutral, animator.Emotion);
                Assert.AreEqual(1, animator.EmotionSets);
                Assert.IsFalse(animator.IsTalking);

                director.Play(new AnimationCue(Emotion.Happy, 1f, "wave"));
                Assert.AreEqual(1, animator.EmotionSets);
                Assert.AreEqual(Emotion.Neutral, animator.Emotion);

                director.SetSuppressed(false);
                Assert.IsFalse(director.ReactionsSuppressed);
                Assert.AreEqual(1, animator.EmotionSets);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PushToTalk_PauseDropsARecordingWithoutTranscribing()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.Cancelled, gate.Tick(true, false, false, true));
            Assert.AreEqual(PushToTalkPhase.Idle, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, false, false, true));
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, false, false));
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(false, false, false));
            Assert.AreEqual(PushToTalkSignal.Started, gate.Tick(true, false, false));
        }

        [Test]
        public void PushToTalk_PauseBlocksANewHold()
        {
            var gate = new PushToTalkGate();
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(true, true, false, true));
            Assert.AreEqual(PushToTalkPhase.Idle, gate.Phase);
            Assert.AreEqual(PushToTalkSignal.None, gate.Tick(false, true, false, true));
        }

        [Test]
        public void Startup_BuildsAQuotedPerUserRunCommand()
        {
            Assert.AreEqual(@"Software\Microsoft\Windows\CurrentVersion\Run", WindowsStartupEntry.RunSubKey);
            Assert.AreEqual("AI Companion", WindowsStartupEntry.ValueName);
            Assert.AreEqual(
                "\"C:\\Games\\AI Companion\\AICompanion.exe\"",
                WindowsStartupEntry.BuildCommand(@"C:\Games\AI Companion\AICompanion.exe"));
            Assert.AreEqual(
                "\"C:\\AICompanion.exe\"",
                WindowsStartupEntry.BuildCommand("\"C:\\AICompanion.exe\""));
            Assert.AreEqual(string.Empty, WindowsStartupEntry.BuildCommand("  "));
            Assert.AreEqual(string.Empty, WindowsStartupEntry.BuildCommand(null));
            Assert.AreEqual(string.Empty, WindowsStartupEntry.BuildCommand("C:\\bad\"quote.exe"));
        }

        [Test]
        public void Startup_EnableWritesTheCommandAndDisableRemovesIt()
        {
            var store = new MemoryStartupRegistration();
            var path = @"C:\AI Companion\AICompanion.exe";

            Assert.IsFalse(StartupToggle.TryApply(store, true, "   "));
            Assert.IsNull(store.Command(WindowsStartupEntry.ValueName));

            Assert.IsTrue(StartupToggle.TryApply(store, true, path));
            bool enabled;
            Assert.IsTrue(store.TryGetEnabled(WindowsStartupEntry.ValueName, out enabled));
            Assert.IsTrue(enabled);
            Assert.AreEqual(WindowsStartupEntry.BuildCommand(path), store.Command(WindowsStartupEntry.ValueName));

            Assert.IsTrue(StartupToggle.TryApply(store, false, path));
            Assert.IsTrue(store.TryGetEnabled(WindowsStartupEntry.ValueName, out enabled));
            Assert.IsFalse(enabled);
            Assert.IsNull(store.Command(WindowsStartupEntry.ValueName));
        }

        [Test]
        public void Startup_FailedWriteDoesNotReportSuccess()
        {
            var store = new MemoryStartupRegistration();
            store.Fail = true;
            Assert.IsFalse(StartupToggle.TryApply(store, true, @"C:\AICompanion.exe"));
            Assert.IsFalse(StartupToggle.TryApply(null, false, @"C:\AICompanion.exe"));
        }

        [Test]
        public void HideTaskbar_DefaultsOnUnlessAPrefTurnsItOff()
        {
            Assert.IsTrue(TrayPreferences.ResolveHideTaskbar(true, false, 0));
            Assert.IsFalse(TrayPreferences.ResolveHideTaskbar(false, false, 1));
            Assert.IsFalse(TrayPreferences.ResolveHideTaskbar(true, true, 0));
            Assert.IsTrue(TrayPreferences.ResolveHideTaskbar(false, true, 1));
        }

        [Test]
        public void TrayClick_LeftSelectsAndRightOpensTheMenu()
        {
            uint packed = (1u << 16) | TrayClick.LeftButtonUp;
            Assert.AreEqual(TrayClick.LeftButtonUp, TrayClick.EventCode(packed, true));
            Assert.AreEqual(TrayClickKind.Primary, TrayClick.Classify(TrayClick.EventCode(packed, true)));
            Assert.AreEqual(TrayClickKind.Primary, TrayClick.Classify(TrayClick.Select));
            Assert.AreEqual(TrayClickKind.Primary, TrayClick.Classify(TrayClick.KeySelect));
            Assert.AreEqual(TrayClickKind.Context, TrayClick.Classify(TrayClick.EventCode(TrayClick.RightButtonUp, false)));
            Assert.AreEqual(TrayClickKind.Context, TrayClick.Classify(TrayClick.ContextMenu));
            Assert.AreEqual(TrayClickKind.None, TrayClick.Classify(0x0200));
        }

        [Test]
        public void TrayClick_DebounceCollapsesADoubleEvent()
        {
            var previous = 0;
            var hasPrevious = false;
            Assert.IsTrue(TrayClick.Allow(ref previous, 1000, 300, ref hasPrevious));
            Assert.IsFalse(TrayClick.Allow(ref previous, 1200, 300, ref hasPrevious));
            Assert.IsTrue(TrayClick.Allow(ref previous, 1300, 300, ref hasPrevious));
        }

        [Test]
        public void TrayIcon_AddOnceAndRemoveOnceAcrossQuitAndExplorerRestart()
        {
            var life = new TrayIconLifecycle();
            Assert.IsTrue(life.TryMarkAdded());
            Assert.IsFalse(life.TryMarkAdded());
            Assert.AreEqual(TrayPresence.Shown, life.Presence);
            Assert.AreEqual(1, life.AddCount);

            life.OnTaskbarRecreated();
            Assert.AreEqual(TrayPresence.Absent, life.Presence);
            Assert.IsTrue(life.TryMarkAdded());
            Assert.AreEqual(2, life.AddCount);

            Assert.IsTrue(life.TryMarkRemoved());
            Assert.IsFalse(life.TryMarkRemoved());
            Assert.AreEqual(1, life.RemoveCount);
            Assert.AreEqual(TrayPresence.Removed, life.Presence);
            life.OnTaskbarRecreated();
            Assert.AreEqual(TrayPresence.Removed, life.Presence);
        }

        [Test]
        public void TrayIcon_PlaceholderHasAFaceAndTransparentCorners()
        {
            var pixels = TrayIconImage.CreateBgra();
            Assert.AreEqual(TrayIconImage.Size * TrayIconImage.Size * 4, pixels.Length);
            Assert.AreEqual(0, TrayIconImage.Alpha(pixels, 0, 0));
            Assert.AreEqual(255, TrayIconImage.Alpha(pixels, 16, 16));
            Assert.Greater(TrayIconImage.Red(pixels, 16, 16), 200);
            Assert.Less(TrayIconImage.Red(pixels, 10, 12), 80);

            var reds = new HashSet<byte>();
            for (var i = 2; i < pixels.Length; i += 4)
                reds.Add(pixels[i]);
            Assert.GreaterOrEqual(reds.Count, 3);
        }

        sealed class MemoryStartupRegistration : IStartupRegistration
        {
            readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public bool Fail;

            public bool TryGetEnabled(string valueName, out bool enabled)
            {
                enabled = false;
                if (Fail)
                    return false;
                string value;
                enabled = _values.TryGetValue(valueName, out value) && !string.IsNullOrWhiteSpace(value);
                return true;
            }

            public bool TrySetEnabled(string valueName, bool enabled, string command)
            {
                if (Fail)
                    return false;
                if (enabled)
                {
                    if (string.IsNullOrWhiteSpace(command))
                        return false;
                    _values[valueName] = command;
                    return true;
                }

                _values.Remove(valueName);
                return true;
            }

            public string Command(string valueName)
            {
                string value;
                return _values.TryGetValue(valueName, out value) ? value : null;
            }
        }
    }

    /// <summary>
    /// Top-level so EditMode can add it. Unity does not add nested MonoBehaviours.
    /// </summary>
    public sealed class CountingAnimator : MonoBehaviour, IAvatarAnimator
    {
        public int EmotionSets;

        public bool IsTalking { get; private set; }

        public Emotion Emotion { get; private set; }

        public float EmotionIntensity { get; private set; } = 1f;

        public void SetTalking(bool talking)
        {
            IsTalking = talking;
        }

        public void SetEmotion(Emotion emotion, float intensity)
        {
            Emotion = emotion;
            EmotionIntensity = intensity;
            EmotionSets++;
        }
    }
}
