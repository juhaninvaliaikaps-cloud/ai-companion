using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.Settings;
using AICompanion.Settings.Presenters;
using AICompanion.UI.Presenters;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    // JUH-104 PR B: presenters behave like the old panels' buttons.

    sealed class FakeSpeech : ISpeechOutput
    {
        public bool IsMuted { get; set; }
        public bool IsSpeaking { get; set; }
        public int Cancels;
        public int Begins;
        public readonly List<string> Appended = new List<string>();
        public int Completes;
        public SpeechOrigin LastOrigin;

        public void SetMuted(bool muted) { IsMuted = muted; }
        public void BeginUtterance() { Begins++; }
        public void BeginUtterance(SpeechOrigin origin) { Begins++; LastOrigin = origin; }
        public void Append(string text) { Appended.Add(text); }
        public void CompleteUtterance() { Completes++; }
        public void Cancel() { Cancels++; IsSpeaking = false; }
    }

    sealed class FakeLook : IScreenLook
    {
        public bool IsBusy { get; set; }
        public bool SendEnabled { get; set; } = true;
        public bool LookButtonInteractable { get; set; } = true;
        public string StatusText { get; set; } = string.Empty;
        public bool AutoLookEnabled { get; set; }
        public int Requests;
        public int Stops;
        public void RequestLook() { Requests++; IsBusy = true; }
        public bool RequestAutoLook() { return false; }
        public void Stop() { Stops++; IsBusy = false; }
    }

    public sealed class ChatPresenterTests
    {
        bool _busy;
        FakeSpeech _speech;
        FakeLook _look;
        readonly List<string> _sent = new List<string>();
        int _cancelled;
        bool _sendResult;

        ChatPresenter Make(bool withLook = true)
        {
            _busy = false;
            _speech = new FakeSpeech();
            _look = withLook ? new FakeLook() : null;
            _sent.Clear();
            _cancelled = 0;
            _sendResult = true;
            return new ChatPresenter(() => _busy, () => _speech, () => _look, t => { _sent.Add(t); return _sendResult; }, () => _cancelled++);
        }

        [Test]
        public void PrimaryAction_CancelsAStreamingReplyFirst()
        {
            var p = Make();
            _busy = true;
            _speech.IsSpeaking = true;
            Assert.AreEqual(ChatActionResult.CancelledReply, p.PrimaryAction("hi"));
            Assert.AreEqual(1, _cancelled);
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void PrimaryAction_StopsSpeechWhenNoReplyStreams()
        {
            var p = Make();
            _speech.IsSpeaking = true;
            Assert.AreEqual(ChatActionResult.StoppedSpeech, p.PrimaryAction("hi"));
            Assert.AreEqual(1, _speech.Cancels);
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void PrimaryAction_SendsTheDraftWhenIdle()
        {
            var p = Make();
            Assert.AreEqual(ChatActionResult.Sent, p.PrimaryAction("hello"));
            CollectionAssert.AreEqual(new[] { "hello" }, _sent);
        }

        [Test]
        public void Send_RejectsBlankTextWithoutCallingTheEngine()
        {
            var p = Make();
            Assert.AreEqual(ChatActionResult.Rejected, p.Send("   "));
            Assert.AreEqual(ChatActionResult.Rejected, p.Send(null));
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void Send_ReportsRejectedWhenTheEngineRefuses()
        {
            var p = Make();
            _sendResult = false;
            Assert.AreEqual(ChatActionResult.Rejected, p.Send("hi"));
        }

        [Test]
        public void ToggleMute_FlipsTheVoice()
        {
            var p = Make();
            Assert.IsTrue(p.ToggleMute());
            Assert.IsTrue(_speech.IsMuted);
            Assert.AreEqual("Muted", p.MuteLabel);
            Assert.IsFalse(p.ToggleMute());
            Assert.AreEqual("Mute", p.MuteLabel);
        }

        [Test]
        public void ToggleMute_WithoutAVoiceDoesNothing()
        {
            var p = new ChatPresenter(() => false, () => null, () => null, _ => true, () => { });
            Assert.IsFalse(p.ToggleMute());
        }

        [Test]
        public void Look_StartsThenStops()
        {
            var p = Make();
            Assert.AreEqual("What do you see?", p.LookLabel);
            Assert.AreEqual(ChatLookResult.Started, p.Look());
            Assert.AreEqual("Stop look", p.LookLabel);
            Assert.AreEqual(ChatLookResult.Stopped, p.Look());
            Assert.AreEqual(1, _look.Requests);
            Assert.AreEqual(1, _look.Stops);
        }

        [Test]
        public void Look_MissingWhenNoCapture()
        {
            var p = Make(false);
            Assert.AreEqual(ChatLookResult.Missing, p.Look());
            Assert.IsTrue(p.LookInteractable);
        }

        [Test]
        public void Escape_StopsLookAndCancelsReply()
        {
            var p = Make();
            _look.IsBusy = true;
            _busy = true;
            p.Escape();
            Assert.AreEqual(1, _look.Stops);
            Assert.AreEqual(1, _cancelled);
            Assert.AreEqual(0, _speech.Cancels);
        }

        [Test]
        public void Escape_StopsSpeechWhenNoReplyStreams()
        {
            var p = Make();
            p.Escape();
            Assert.AreEqual(0, _cancelled);
            Assert.AreEqual(1, _speech.Cancels);
        }

        [Test]
        public void Labels_MatchTheOldPanel()
        {
            Assert.AreEqual("Send", ChatPresenter.ActionLabelFor(false, false));
            Assert.AreEqual("Stop", ChatPresenter.ActionLabelFor(true, false));
            Assert.AreEqual("Stop", ChatPresenter.ActionLabelFor(false, true));
            Assert.AreEqual("Rec", ChatPresenter.MicLabelFor(true, true));
            Assert.AreEqual("...", ChatPresenter.MicLabelFor(false, true));
            Assert.AreEqual("Mic", ChatPresenter.MicLabelFor(false, false));
        }
    }

    sealed class FakeSettingsHost : ISettingsHost, IPersonaHost
    {
        public CompanionSettings Settings { get; } = CompanionSettings.CreateDefault();
        public int Applies;
        public int Refreshes;
        public string Status { get; private set; } = string.Empty;
        public string ActiveModelText => "Grok (test)";
        public readonly List<PersonaEntry> Entries = new List<PersonaEntry>();
        public string ActiveId = string.Empty;
        public readonly List<string> Used = new List<string>();
        public int SetupNameUses;

        public void ApplyAndSave() { Applies++; }
        public void SetStatus(string message) { Status = message ?? string.Empty; }
        public void RequestRefresh() { Refreshes++; }

        public IReadOnlyList<PersonaEntry> PersonaEntries => Entries;
        public string ActivePersonaId => ActiveId;
        public void UsePersona(string id) { Used.Add(id); ActiveId = id; }
        public void UseSetupName() { SetupNameUses++; ActiveId = string.Empty; }
    }

    public sealed class SettingsPresenterTests
    {
        [Test]
        public void Change_AppliesSavesAndRefreshes()
        {
            var host = new FakeSettingsHost();
            var p = new SettingsPresenter(host);
            p.SetPrivateMode(true);
            Assert.IsTrue(host.Settings.PrivateMode);
            Assert.AreEqual(1, host.Applies);
            Assert.AreEqual(1, host.Refreshes);
        }

        [Test]
        public void SetProvider_OnlyGrokOrLocal()
        {
            var host = new FakeSettingsHost();
            var p = new SettingsPresenter(host);
            p.SetProvider("LOCAL");
            Assert.AreEqual(ChatDefaults.LocalId, host.Settings.ProviderId);
            p.SetProvider("something-else");
            Assert.AreEqual(ChatDefaults.GrokId, host.Settings.ProviderId);
            Assert.IsTrue(p.GrokSelected);
        }

        [Test]
        public void ClickThrough_CyclesThroughTheThreeModes()
        {
            var first = SettingsPresenter.NextClickThrough("silhouette");
            Assert.AreEqual("panels only", SettingsPresenter.ClickThroughLabel(first));
            var second = SettingsPresenter.NextClickThrough(first);
            Assert.AreEqual("capture the window", SettingsPresenter.ClickThroughLabel(second));
            var third = SettingsPresenter.NextClickThrough(second);
            Assert.AreEqual("silhouette", SettingsPresenter.ClickThroughLabel(third));
        }

        [Test]
        public void NewKeys_DefaultOffAndToggle()
        {
            var host = new FakeSettingsHost();
            var p = new SettingsPresenter(host);
            Assert.IsFalse(host.Settings.NewUiPreview);
            Assert.IsFalse(host.Settings.VoiceNarration);
            p.SetNewUiPreview(true);
            p.SetVoiceNarration(true);
            Assert.IsTrue(host.Settings.NewUiPreview);
            Assert.IsTrue(host.Settings.VoiceNarration);
        }

        [Test]
        public void PrivateEndpointSettings_ApplyAndSaveThroughPresenter()
        {
            var host = new FakeSettingsHost();
            var p = new SettingsPresenter(host);
            p.SetPrivateMode(true);
            p.Change(s =>
            {
                s.PrivateEndpointType = PrivateEndpointTypes.Custom;
                s.PrivateBaseUrl = "https://gpu.example.com/v1";
                s.PrivateModel = "qwen2.5";
            });

            Assert.AreEqual(PrivateEndpointTypes.Custom, host.Settings.PrivateEndpointType);
            Assert.AreEqual("https://gpu.example.com/v1", host.Settings.PrivateBaseUrl);
            Assert.AreEqual("qwen2.5", host.Settings.PrivateModel);
            Assert.GreaterOrEqual(host.Applies, 2);
            Assert.GreaterOrEqual(host.Refreshes, 2);
        }

        [Test]
        public void ToggleLabel_MatchesTheOldButtons()
        {
            Assert.AreEqual("Pause: on", SettingsPresenter.ToggleLabel("Pause", true));
            Assert.AreEqual("Pause: off", SettingsPresenter.ToggleLabel("Pause", false));
        }
    }

    public sealed class SettingsCompatibilityTests
    {
        [Test]
        public void OldSettingsFileWithoutNewKeys_LoadsWithNewKeysOff()
        {
            var old = "{\n  \"version\": 1,\n  \"providerId\": \"grok\",\n  \"privateMode\": true,\n  \"showMoodDebug\": true\n}\n";
            var settings = CompanionSettingsJson.Parse(old, SettingsLegacyPrefs.None);
            Assert.IsTrue(settings.PrivateMode);
            Assert.IsTrue(settings.ShowMoodDebug);
            Assert.IsFalse(settings.NewUiPreview);
            Assert.IsFalse(settings.VoiceNarration);
        }

        [Test]
        public void NewKeys_RoundTrip()
        {
            var settings = CompanionSettings.CreateDefault();
            settings.NewUiPreview = true;
            settings.VoiceNarration = true;
            var json = CompanionSettingsJson.Write(settings);
            StringAssert.Contains("\"newUiPreview\": true", json);
            StringAssert.Contains("\"voiceNarration\": true", json);
            var back = CompanionSettingsJson.Parse(json, SettingsLegacyPrefs.None);
            Assert.IsTrue(back.NewUiPreview);
            Assert.IsTrue(back.VoiceNarration);
        }

        [Test]
        public void ExistingKeys_StillWritten()
        {
            var json = CompanionSettingsJson.Write(CompanionSettings.CreateDefault());
            StringAssert.Contains("\"showMoodDebug\"", json);
            StringAssert.Contains("\"intimateExpressions\"", json);
            StringAssert.Contains("\"privateMode\"", json);
        }
    }

    public sealed class NewUiSwitchTests
    {
        [TearDown]
        public void Reset()
        {
            NewUiSwitch.Reset();
        }

        [Test]
        public void Argument_IsRecognised()
        {
            Assert.IsTrue(NewUiSwitch.ArgumentPresent(new[] { "app.exe", "-newui" }));
            Assert.IsTrue(NewUiSwitch.ArgumentPresent(new[] { "app.exe", "--NewUI" }));
            Assert.IsFalse(NewUiSwitch.ArgumentPresent(new[] { "app.exe", "--setup" }));
            Assert.IsFalse(NewUiSwitch.ArgumentPresent(null));
        }

        [Test]
        public void Resolve_OffByDefault()
        {
            Assert.IsFalse(NewUiSwitch.Resolve(false, new[] { "app.exe" }));
            Assert.IsTrue(NewUiSwitch.Resolve(true, new[] { "app.exe" }));
            Assert.IsTrue(NewUiSwitch.Resolve(false, new[] { "app.exe", "-newui" }));
        }

        [Test]
        public void Set_RaisesChangedOnlyOnChange()
        {
            var count = 0;
            Action handler = () => count++;
            NewUiSwitch.Changed += handler;
            try
            {
                NewUiSwitch.Set(true);
                NewUiSwitch.Set(true);
                NewUiSwitch.Set(false);
                Assert.AreEqual(2, count);
            }
            finally
            {
                NewUiSwitch.Changed -= handler;
            }
        }
    }

    public sealed class PersonaPresenterTests
    {
        static PersonaEntry Entry(string id, string name, bool builtIn = true, bool userFile = false)
        {
            var persona = PersonaDefinition.CreateStarter(id);
            persona.DisplayName = name;
            return new PersonaEntry(persona, builtIn, userFile);
        }

        [Test]
        public void Choices_SetupNameFirstThenPersonas()
        {
            var host = new FakeSettingsHost();
            host.Entries.Add(Entry("friend", "Friend"));
            host.Entries.Add(Entry("coach", "Coach", true, true));
            var choices = new PersonaPresenter(host).Choices();
            Assert.AreEqual(3, choices.Count);
            Assert.IsTrue(choices[0].IsSetupName);
            Assert.AreEqual("Setup name (active)", choices[0].Label);
            Assert.AreEqual("Friend", choices[1].Label);
            Assert.AreEqual("Coach (saved)", choices[2].Label);
        }

        [Test]
        public void Choose_SwitchesThroughTheHost()
        {
            var host = new FakeSettingsHost();
            host.Entries.Add(Entry("friend", "Friend"));
            var p = new PersonaPresenter(host);
            p.Choose("friend");
            CollectionAssert.AreEqual(new[] { "friend" }, host.Used);
            Assert.AreEqual("Friend (active)", p.Choices()[1].Label);
            Assert.AreEqual("Friend", p.ActiveDisplayName());
            p.Choose("friend");
            Assert.AreEqual(1, host.Used.Count, "choosing the active persona again does nothing");
            p.Choose(string.Empty);
            Assert.AreEqual(1, host.SetupNameUses);
        }
    }

    public sealed class PrivacyPresenterTests
    {
        [Test]
        public void Confirm_NewRequestReplacesThePendingOne()
        {
            var c = new PrivacyConfirm();
            c.Request(PrivacyConfirmKind.ClearHistory);
            c.Request(PrivacyConfirmKind.WipeAll);
            Assert.AreEqual(PrivacyConfirmKind.WipeAll, c.Pending);
        }

        [Test]
        public void Confirm_EscapeCancelsFirst()
        {
            var c = new PrivacyConfirm();
            Assert.IsFalse(c.Escape());
            c.Request(PrivacyConfirmKind.DeleteMemory, "m1");
            Assert.AreEqual("m1", c.MemoryId);
            Assert.IsTrue(c.Escape());
            Assert.IsFalse(c.HasPending);
        }

        [Test]
        public void Confirm_TakeRunsOnce()
        {
            var c = new PrivacyConfirm();
            c.Request(PrivacyConfirmKind.DeleteMemory, "m1");
            string id;
            Assert.AreEqual(PrivacyConfirmKind.DeleteMemory, c.Take(out id));
            Assert.AreEqual("m1", id);
            Assert.AreEqual(PrivacyConfirmKind.None, c.Take(out id));
        }

        [Test]
        public void Prompts_MatchTheOldPrivacyTab()
        {
            StringAssert.StartsWith("Clear saved chat history", PrivacyConfirm.Prompt(PrivacyConfirmKind.ClearHistory, 0, null));
            StringAssert.Contains("cannot be undone", PrivacyConfirm.Prompt(PrivacyConfirmKind.WipeAll, 0, null));
            Assert.AreEqual("Delete \"likes tea\"?", PrivacyConfirm.Prompt(PrivacyConfirmKind.DeleteMemory, 1, "likes tea"));
            Assert.AreEqual("Delete all 3 memories? This cannot be undone.", PrivacyConfirm.Prompt(PrivacyConfirmKind.DeleteAllMemories, 3, null));
        }

        [Test]
        public void Export_NeedsAFolder()
        {
            Assert.AreEqual(LocalDataPresenter.ChooseFolderMessage, LocalDataPresenter.Export("C:\\data", "  ", false, null, null));
        }

        [Test]
        public void Export_RefusesAFolderInsideTheDataFolder()
        {
            var root = Path.Combine(Path.GetTempPath(), "happi-root");
            var inside = Path.Combine(root, "export");
            var called = false;
            var status = LocalDataPresenter.Export(root, inside, false, (a, b) => { called = true; return new LocalActionResult { Ok = true }; }, null);
            Assert.AreEqual(LocalDataPresenter.InsideRootMessage, status);
            Assert.IsFalse(called);
        }

        [Test]
        public void Export_ZipGetsADefaultFileName()
        {
            var root = Path.Combine(Path.GetTempPath(), "happi-root");
            var dest = Path.Combine(Path.GetTempPath(), "happi-out");
            string where = null;
            var status = LocalDataPresenter.Export(root, dest, true, null, (a, b) =>
            {
                where = b;
                var result = new LocalActionResult { Ok = true, Files = 4 };
                result.Notes.Add("skipped");
                return result;
            });
            Assert.AreEqual(Path.Combine(dest, "ProjectLeia-export.zip"), where);
            Assert.AreEqual("Exported 4 file(s). Some files were skipped.", status);
            Assert.AreEqual("C:\\x\\a.zip", LocalDataPresenter.ZipPath("C:\\x\\a.zip"));
        }

        [Test]
        public void Export_ExceptionBecomesAMessage()
        {
            var root = Path.Combine(Path.GetTempPath(), "happi-root");
            var dest = Path.Combine(Path.GetTempPath(), "happi-out");
            var status = LocalDataPresenter.Export(root, dest, false, (a, b) => { throw new IOException("disk"); }, null);
            Assert.AreEqual(LocalDataPresenter.FailedMessage, status);
        }
    }

    sealed class FakeScreenPort : IScreenLookPort
    {
        public bool IsBusy { get; set; }
        public bool LookButtonInteractable { get; set; } = true;
        public string StatusText { get; set; } = "Ready.";
        public readonly List<string> Labels = new List<string> { "Display 1 (primary)", "Display 2" };
        public IReadOnlyList<string> MonitorLabels => Labels;
        public int SelectedMonitorIndex { get; set; } = -1;
        public int Refreshes;
        public int Disables;
        public int Looks;
        public int Stops;
        public void RefreshWindows() { Refreshes++; }
        public void RefreshMonitors() { }
        public void DisableFeature() { Disables++; }
        public void SelectMonitor(int index) { SelectedMonitorIndex = index; }
        public void RequestLook() { Looks++; IsBusy = true; }
        public void Stop() { Stops++; IsBusy = false; }
    }

    public sealed class ScreenLookPresenterTests
    {
        [Test]
        public void Toggle_DoesWhatReadAWindowDoes()
        {
            var host = new FakeSettingsHost();
            var port = new FakeScreenPort();
            var p = new ScreenLookPresenter(new SettingsPresenter(host), () => port);
            p.Toggle();
            Assert.IsTrue(host.Settings.ScreenLookEnabled);
            Assert.AreEqual(1, port.Refreshes);
            p.Toggle();
            Assert.IsFalse(host.Settings.ScreenLookEnabled);
            Assert.AreEqual(1, port.Disables);
        }

        [Test]
        public void Monitors_NeedWholeScreenConsent()
        {
            var host = new FakeSettingsHost();
            var port = new FakeScreenPort();
            var p = new ScreenLookPresenter(new SettingsPresenter(host), () => port);
            host.Settings.ScreenLookEnabled = true;
            string note;
            Assert.AreEqual(0, p.Monitors(out note).Count);
            Assert.AreEqual(ScreenLookPresenter.ConsentNeeded, note);
            Assert.IsFalse(p.SelectMonitor(1));
            Assert.AreEqual(-1, port.SelectedMonitorIndex);

            host.Settings.ScreenCaptureWholeMonitor = true;
            host.Settings.ScreenWholeMonitorConsent = true;
            var monitors = p.Monitors(out note);
            Assert.AreEqual(2, monitors.Count);
            Assert.IsTrue(p.SelectMonitor(1));
            Assert.IsTrue(p.Monitors(out note)[1].Selected);
            Assert.IsFalse(p.SelectMonitor(5));
        }

        [Test]
        public void LookNow_RespectsThePolicy()
        {
            var host = new FakeSettingsHost();
            var port = new FakeScreenPort { LookButtonInteractable = false };
            var p = new ScreenLookPresenter(new SettingsPresenter(host), () => port);
            Assert.IsFalse(p.LookNow(), "off: no look");
            host.Settings.ScreenLookEnabled = true;
            Assert.IsFalse(p.LookNow(), "policy says no");
            port.LookButtonInteractable = true;
            Assert.IsTrue(p.LookNow());
            Assert.AreEqual(1, port.Looks);
            Assert.IsTrue(p.LookNow());
            Assert.AreEqual(1, port.Stops);
        }

        [Test]
        public void MissingCapture_IsReported()
        {
            var p = new ScreenLookPresenter(new SettingsPresenter(new FakeSettingsHost()), () => null);
            Assert.IsFalse(p.Available);
            Assert.AreEqual(ScreenLookPresenter.Unavailable, p.StatusText);
        }
    }

    public sealed class VideoAvatarPresenterTests
    {
        [Test]
        public void TestReactions_AreTheEightOldButtonsInOrder()
        {
            var names = new List<string>();
            foreach (var pair in VideoAvatarPresenter.TestReactions)
                names.Add(pair.Value);
            CollectionAssert.AreEqual(new[] { "happy", "concerned", "turn360", "embarrassed", "flustered", "pout", "wink_dance", "turn_around_back" }, names);
        }

        [Test]
        public void ReactionStatus_MatchesTheOldMessages()
        {
            Assert.AreEqual("No playable \"pout\" clip in the video folder.", VideoAvatarPresenter.ReactionStatus(AvatarReactionResult.Refused, "pout", 6f));
            Assert.AreEqual("\"pout\" plays when she is back on the front idle (within 6 s).", VideoAvatarPresenter.ReactionStatus(AvatarReactionResult.Waiting, "pout", 6f));
            Assert.AreEqual(string.Empty, VideoAvatarPresenter.ReactionStatus(AvatarReactionResult.Started, "pout", 6f));
        }

        [Test]
        public void DoSomething_VideoOffersOnlyLoadedClipsPlusWalks()
        {
            var choices = VideoAvatarPresenter.DoSomething(true, new[] { "wink_dance", "", "pout" });
            Assert.AreEqual(6, choices.Count);
            Assert.AreEqual("Wink dance", choices[0].Label);
            Assert.AreEqual("pout", choices[1].Name);
            Assert.AreEqual(DoSomethingKind.Walk, choices[2].Kind);
            Assert.AreEqual(-1f, choices[2].Value);
        }

        [Test]
        public void DoSomething_3DOffersTheDebugEmotions()
        {
            var choices = VideoAvatarPresenter.DoSomething(false, new[] { "pout" });
            Assert.AreEqual(5, choices.Count);
            foreach (var choice in choices)
                Assert.AreEqual(DoSomethingKind.Emotion, choice.Kind);
        }
    }

    public sealed class MoodPresenterTests
    {
        [Test]
        public void Describe_ContentAndStrongMoods()
        {
            Assert.AreEqual("She feels content.", MoodPresenter.Describe(CompanionMood.Content, 0f, string.Empty, false, 12f));
            var happy = MoodPresenter.Describe(CompanionMood.Happy, 0.8f, "happy 0.80", false, 12f);
            StringAssert.StartsWith("She feels happy (strongly).", happy);
            StringAssert.Contains("happy 0.80", happy);
        }

        [Test]
        public void Describe_NeverNamesTheIntimateMoodWhenTheGateIsClosed()
        {
            var text = MoodPresenter.Describe(CompanionMood.Intimate, 0.9f, "intimate 0.90, happy 0.2", false, 12f);
            StringAssert.DoesNotContain("intimate", text);
            Assert.AreEqual("She feels content.", text);
        }

        [Test]
        public void Describe_LateSleepinessSaysWhy()
        {
            StringAssert.Contains("late", MoodPresenter.Describe(CompanionMood.Sleepy, 0.5f, string.Empty, false, 2f));
        }
    }

    sealed class FakeMemoryPort : IMemoryPort
    {
        public readonly List<MemoryEntry> List = new List<MemoryEntry>();
        public IReadOnlyList<MemoryEntry> Entries => List;
        public bool Delete(string id) { return List.RemoveAll(e => e.id == id) > 0; }
        public int DeleteAll() { var n = List.Count; List.Clear(); return n; }
    }

    public sealed class MemoryPresenterTests
    {
        static MemoryEntry Entry(string id, string text)
        {
            return new MemoryEntry { id = id, text = text };
        }

        [Test]
        public void Forget_TakesTwoSteps()
        {
            var port = new FakeMemoryPort();
            port.List.Add(Entry("a", "likes tea"));
            port.List.Add(Entry("b", "has a cat"));
            var p = new MemoryPresenter(() => port);
            Assert.AreEqual(2, p.Page().Count);
            p.AskForget("a");
            Assert.AreEqual(2, port.List.Count, "nothing is deleted before confirm");
            Assert.AreEqual("Delete \"likes tea\"?", p.PendingPrompt());
            Assert.IsTrue(p.ConfirmPending());
            Assert.AreEqual(1, port.List.Count);
            Assert.IsFalse(p.ConfirmPending(), "a confirm runs once");
        }

        [Test]
        public void ForgetAll_AfterConfirm()
        {
            var port = new FakeMemoryPort();
            port.List.Add(Entry("a", "likes tea"));
            var p = new MemoryPresenter(() => port);
            p.AskForgetAll();
            StringAssert.StartsWith("Delete all 1 memories?", p.PendingPrompt());
            p.Confirm.Cancel();
            Assert.IsFalse(p.ConfirmPending());
            Assert.AreEqual(1, port.List.Count);
            p.AskForgetAll();
            Assert.IsTrue(p.ConfirmPending());
            Assert.AreEqual(0, port.List.Count);
        }
    }
}
