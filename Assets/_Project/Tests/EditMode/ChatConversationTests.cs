using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.UI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public class ChatConversationTests
    {
        [TearDown]
        public void ResetInputGuard()
        {
            DebugInputGuard.SetTextFieldFocused(false);
        }

        [Test]
        public void TryBeginSend_AppendsTheUserTurnAndAStreamingReply()
        {
            var conversation = new ChatConversation();

            Assert.IsTrue(conversation.TryBeginSend("  hello  ", "  be kind  ", 0.2f, 16, out var request));

            Assert.IsTrue(conversation.IsBusy);
            Assert.AreEqual(1, conversation.Generation);
            Assert.AreEqual(2, conversation.Lines.Count);
            Assert.AreEqual(ChatLineKind.User, conversation.Lines[0].Kind);
            Assert.AreEqual("hello", conversation.Lines[0].Text);
            Assert.IsTrue(conversation.Lines[1].Streaming);
            Assert.AreEqual(string.Empty, conversation.Lines[1].Text);
            Assert.AreEqual("be kind", request.SystemPrompt);
            Assert.IsNull(request.Model);
            Assert.AreEqual(0.2f, request.Temperature, 0.0001f);
            Assert.AreEqual(16, request.MaxTokens);
            Assert.AreEqual(1, request.Messages.Count);
            Assert.AreEqual(ChatRole.User, request.Messages[0].Role);
            Assert.AreEqual("hello", request.Messages[0].Content);
            Assert.IsFalse(conversation.TryBeginSend("next", "be kind", 0.2f, 16, out _));
            Assert.AreEqual(2, conversation.Lines.Count);
        }

        [Test]
        public void PushDelta_AppendsOnlyTheStreamingAssistant()
        {
            var conversation = new ChatConversation();
            conversation.TryBeginSend("hi", "persona", 0.7f, 32, out _);

            conversation.PushDelta("Hel");
            conversation.PushDelta("lo");
            conversation.PushDelta(conversation.Generation - 1, "stale");
            conversation.PushDelta(string.Empty);

            Assert.AreEqual("Hello", conversation.Lines[1].Text);
            Assert.IsTrue(conversation.Lines[1].Streaming);
            StringAssert.Contains("Hello", conversation.FormatTranscript());
            StringAssert.Contains("You", conversation.FormatTranscript());
            StringAssert.Contains("Companion", conversation.FormatTranscript());
        }

        [Test]
        public void Complete_SealsDeltasAndKeepsTheFullSessionWhileRequestsTrim()
        {
            var conversation = new ChatConversation(2, 4000);
            conversation.AddSystem("local note");
            Finish(conversation, "one", "A");
            Finish(conversation, "two", "B");
            Assert.IsTrue(conversation.TryBeginSend("three", "persona", 0.7f, 32, out var request));

            Assert.AreEqual(7, conversation.Lines.Count);
            Assert.AreEqual(ChatLineKind.System, conversation.Lines[0].Kind);
            Assert.AreEqual(2, request.Messages.Count);
            Assert.AreEqual("B", request.Messages[0].Content);
            Assert.AreEqual(ChatRole.Assistant, request.Messages[0].Role);
            Assert.AreEqual("three", request.Messages[1].Content);
            Assert.IsFalse(TranscriptOf(request).Contains("local note"));
            Assert.IsFalse(TranscriptOf(request).Contains("one"));
        }

        [Test]
        public void BuildRequest_DropsOldestTurnsPastTheCharacterCapAndKeepsTheLast()
        {
            var conversation = new ChatConversation(20, 10);
            Finish(conversation, "12345", "abcde");
            Assert.IsTrue(conversation.TryBeginSend("Z", "persona", 0.7f, 32, out var trimmed));

            Assert.AreEqual(2, trimmed.Messages.Count);
            Assert.AreEqual("abcde", trimmed.Messages[0].Content);
            Assert.AreEqual("Z", trimmed.Messages[1].Content);

            var huge = new ChatConversation(20, 10);
            Assert.IsTrue(huge.TryBeginSend(new string('q', 50), "persona", 0.7f, 32, out var kept));
            Assert.AreEqual(1, kept.Messages.Count);
            Assert.AreEqual(50, kept.Messages[0].Content.Length);
        }

        [Test]
        public void Complete_UsesTheResultTextWhenNoDeltaArrived()
        {
            var conversation = new ChatConversation();
            conversation.TryBeginSend("hi", "persona", 0.7f, 32, out _);
            conversation.Complete(ChatResult.Success("Hello"));

            Assert.IsFalse(conversation.IsBusy);
            Assert.AreEqual("Hello", conversation.Lines[1].Text);
            Assert.IsFalse(conversation.Lines[1].Streaming);
        }

        [Test]
        public void Complete_EmptySuccessBecomesASystemLine()
        {
            var conversation = new ChatConversation();
            conversation.TryBeginSend("hi", "persona", 0.7f, 32, out _);
            conversation.Complete(ChatResult.Success(string.Empty));

            Assert.AreEqual(2, conversation.Lines.Count);
            Assert.AreEqual(ChatLineKind.System, conversation.Lines[1].Kind);
            Assert.AreEqual("Empty reply.", conversation.Lines[1].Text);
        }

        [Test]
        public void Complete_ErrorUsesTheUserMessageAndDropsAnEmptyPlaceholder()
        {
            var conversation = new ChatConversation();
            conversation.TryBeginSend("hi", "persona", 0.7f, 32, out _);
            conversation.Complete(ChatResult.Fail(ChatErrorKind.InvalidApiKey, "Invalid API key", "Chat error: xai-secret"));

            Assert.IsFalse(conversation.IsBusy);
            Assert.AreEqual(2, conversation.Lines.Count);
            Assert.AreEqual(ChatLineKind.System, conversation.Lines[1].Kind);
            Assert.AreEqual("Invalid API key", conversation.Lines[1].Text);
            StringAssert.DoesNotContain("xai-secret", conversation.FormatTranscript());
        }

        [Test]
        public void Complete_PartialReplyStaysOnScreenWhenTheRequestFailsOrIsCancelled()
        {
            var failed = new ChatConversation();
            failed.TryBeginSend("hi", "persona", 0.7f, 32, out _);
            failed.PushDelta("Hi");
            failed.Complete(ChatResult.Fail(ChatErrorKind.Unreachable, "Couldn't reach Grok", "Chat error: HTTP 500 from Grok: down"));

            Assert.AreEqual("Hi", failed.Lines[1].Text);
            Assert.IsFalse(failed.Lines[1].Streaming);
            Assert.AreEqual("Couldn't reach Grok", failed.Lines[2].Text);

            var cancelled = new ChatConversation();
            cancelled.TryBeginSend("hi", "persona", 0.7f, 32, out _);
            cancelled.PushDelta("Part");
            cancelled.Complete(ChatResult.Fail(ChatErrorKind.Cancelled, ChatErrors.Cancelled));

            Assert.AreEqual("Part", cancelled.Lines[1].Text);
            Assert.AreEqual("Request cancelled", cancelled.Lines[2].Text);
            Assert.IsFalse(cancelled.Lines[1].Streaming);
        }

        [Test]
        public void BlankSystemPrompt_FallsBackToTheCompanionDefault()
        {
            var conversation = new ChatConversation();
            conversation.TryBeginSend("hi", "   ", 0.7f, 32, out var request);

            Assert.AreEqual(ChatDefaults.SystemPrompt, request.SystemPrompt);
            StringAssert.Contains("desktop companion", ChatDefaults.SystemPrompt);
        }

        [Test]
        public void Exchange_StreamsIntoTheLastReplyAndClearsTalking()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var talking = new List<bool>();
            var spoken = new List<string>();
            exchange.Talking = talking.Add;
            exchange.SpokenDelta = spoken.Add;
            var provider = new ScriptedProvider { Deltas = new[] { "He", "llo" }, Result = ChatResult.Success("Hello") };

            var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Hello", conversation.Lines[1].Text);
            Assert.IsFalse(conversation.IsBusy);
            Assert.AreEqual(1, provider.Calls);
            Assert.AreEqual("persona", provider.Last.SystemPrompt);
            Assert.AreEqual("hi", provider.Last.Messages[0].Content);
            Assert.IsNull(provider.Last.Model);
            CollectionAssert.AreEqual(new[] { true, false }, talking);
            CollectionAssert.AreEqual(new[] { "He", "llo" }, spoken);
        }

        [Test]
        public void Exchange_SpeechSinkFailureDoesNotBreakTheReply()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            exchange.SpokenDelta = _ => throw new InvalidOperationException("xai-leak-from-speech");
            var provider = new ScriptedProvider { Deltas = new[] { "Hi" }, Result = ChatResult.Success("Hi") };

            var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            StringAssert.Contains("Hi", conversation.FormatTranscript());
            StringAssert.DoesNotContain("xai-leak-from-speech", conversation.FormatTranscript());
        }

        [Test]
        public void Exchange_EmptyTextDoesNotCallTheProviderOrStartTalking()
        {
            var exchange = new ChatExchange(new ChatConversation());
            var talking = new List<bool>();
            exchange.Talking = talking.Add;
            var provider = new ScriptedProvider();

            var result = exchange.Run(provider, "   ", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(0, provider.Calls);
            Assert.AreEqual(0, talking.Count);
            Assert.AreEqual(0, exchange.Conversation.Lines.Count);
        }

        [Test]
        public void Exchange_AlreadyCancelledTokenBecomesASystemLineWithoutTalking()
        {
            var exchange = new ChatExchange(new ChatConversation());
            var talking = new List<bool>();
            exchange.Talking = talking.Add;
            var provider = new ScriptedProvider();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, cancel.Token).GetAwaiter().GetResult();

                Assert.AreEqual(ChatErrorKind.Cancelled, result.Error);
                Assert.AreEqual("Request cancelled", exchange.Conversation.Lines[exchange.Conversation.Lines.Count - 1].Text);
                Assert.AreEqual(0, provider.Calls);
                Assert.AreEqual(0, talking.Count);
            }
        }

        [Test]
        public void Exchange_ProviderExceptionDoesNotShowTheExceptionText()
        {
            var exchange = new ChatExchange(new ChatConversation());
            var talking = new List<bool>();
            exchange.Talking = talking.Add;
            var provider = new ScriptedProvider { Throw = new InvalidOperationException("xai-secret-token") };

            var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(ChatErrorKind.Unreachable, result.Error);
            StringAssert.DoesNotContain("xai-secret-token", exchange.Conversation.FormatTranscript());
            StringAssert.Contains("Couldn't reach the chat provider", exchange.Conversation.FormatTranscript());
            CollectionAssert.AreEqual(new[] { true, false }, talking);
        }

        [Test]
        public void Placement_SitsLeftOfTheAvatarWithTheRequestedGap()
        {
            var avatar = new Rect(360f, 80f, 160f, 500f);
            var gap = 40f;
            var panel = ChatPanelPlacement.Place(800f, 720f, avatar, gap);

            Assert.AreEqual(ChatPanelPlacement.Width, panel.width);
            Assert.AreEqual(avatar.xMin - gap - panel.width, panel.x, 0.01f);
            Assert.LessOrEqual(panel.xMax, avatar.xMin - gap + 0.01f);
            Assert.GreaterOrEqual(panel.x, ChatPanelPlacement.Margin);
            Assert.AreEqual(720f - ChatPanelPlacement.Height - ChatPanelPlacement.Margin, panel.y, 0.01f);
            Assert.Greater(panel.y, ChatPanelPlacement.TopLimit);
            Assert.IsFalse(panel.Overlaps(avatar));
        }

        [Test]
        public void Placement_UsesTheRightSideWhenTheLeftGutterIsTooSmall()
        {
            var avatar = new Rect(16f, 40f, 184f, 600f);
            var gap = ChatPanelPlacement.DefaultGap;
            var panel = ChatPanelPlacement.Place(480f, 720f, avatar, gap);

            Assert.GreaterOrEqual(panel.x, avatar.xMax + gap - 0.01f);
            Assert.AreEqual(ChatPanelPlacement.Width, panel.width);
            Assert.LessOrEqual(panel.xMax, 480f - ChatPanelPlacement.Margin + 0.01f);
            Assert.IsFalse(panel.Overlaps(avatar));
        }

        [Test]
        public void Placement_ShrinksOnTheLeftInsteadOfCoveringACenteredModel()
        {
            var avatar = new Rect(170f, 40f, 140f, 600f);
            var gap = 40f;
            var panel = ChatPanelPlacement.Place(480f, 720f, avatar, gap);

            Assert.Less(panel.xMax, avatar.xMin);
            Assert.AreEqual(gap, avatar.xMin - panel.xMax, 0.01f);
            Assert.GreaterOrEqual(panel.x, ChatPanelPlacement.Margin - 0.01f);
            Assert.Less(panel.width, ChatPanelPlacement.Width);
            Assert.IsFalse(panel.Overlaps(avatar));
        }

        [Test]
        public void Placement_DropsTheGapOnlyWhenTheGutterCannotHoldIt()
        {
            var avatar = new Rect(20f, 0f, 160f, 100f);
            var panel = ChatPanelPlacement.Place(200f, 720f, avatar, 40f);

            Assert.LessOrEqual(panel.xMax, avatar.xMin + 0.01f);
            Assert.GreaterOrEqual(panel.x, ChatPanelPlacement.Margin - 0.01f);
            Assert.IsFalse(panel.Overlaps(avatar));
        }

        [Test]
        public void ScreenBounds_IgnorePointsBehindTheCamera()
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            Assert.IsFalse(ChatPanelPlacement.TryIncludeScreenPoint(720f, new Vector3(10f, 10f, -1f), ref min, ref max));
            Assert.IsTrue(ChatPanelPlacement.TryIncludeScreenPoint(720f, new Vector3(100f, 50f, 1f), ref min, ref max));
            Assert.IsTrue(ChatPanelPlacement.TryIncludeScreenPoint(720f, new Vector3(180f, 400f, 2f), ref min, ref max));

            Assert.AreEqual(100f, min.x, 0.01f);
            Assert.AreEqual(180f, max.x, 0.01f);
            Assert.AreEqual(720f - 400f, min.y, 0.01f);
            Assert.AreEqual(720f - 50f, max.y, 0.01f);
        }

        [Test]
        public void BoundsCorners_CoverTheBoxExtents()
        {
            var corners = new Vector3[8];
            ChatPanelPlacement.WriteBoundsCorners(new Bounds(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f)), corners);

            Assert.AreEqual(new Vector3(0f, 0f, 0f), corners[0]);
            Assert.AreEqual(new Vector3(2f, 4f, 6f), corners[7]);
        }

        [Test]
        public void Placement_ClampsBoundsThatProjectPastTheRightEdge()
        {
            var avatar = new Rect(900f, 200f, 500f, 800f);
            var panel = ChatPanelPlacement.Place(480f, 720f, avatar, 40f);

            Assert.AreEqual(252f, panel.x, 0.01f);
            Assert.AreEqual(ChatPanelPlacement.Width, panel.width, 0.01f);
            Assert.GreaterOrEqual(panel.x, ChatPanelPlacement.Margin - 0.01f);
            Assert.LessOrEqual(panel.xMax, 480f - ChatPanelPlacement.Margin + 0.01f);
            Assert.GreaterOrEqual(panel.y, ChatPanelPlacement.Margin - 0.01f);
            Assert.LessOrEqual(panel.yMax, 720f - ChatPanelPlacement.Margin + 0.01f);
        }

        [Test]
        public void Placement_BottomRightWindowStaysAboveTheTaskbarAndLeftOfHer()
        {
            const int windowLeft = 1392;
            const int windowTop = 264;
            const int windowRight = 1872;
            const int windowBottom = 1344;
            var visible = ChatPanelPlacement.VisibleGui(
                480f,
                720f,
                windowLeft,
                windowTop,
                windowRight,
                windowBottom,
                0,
                0,
                1920,
                1032);

            Assert.AreEqual(0f, visible.x, 0.01f);
            Assert.AreEqual(0f, visible.y, 0.01f);
            Assert.AreEqual(480f, visible.width, 0.01f);
            Assert.AreEqual(512f, visible.height, 0.5f);

            var anchor = visible.xMax - 40f;
            var column = ChatPanelPlacement.Column(anchor, ChatPanelPlacement.BodyWidth, 480f, 720f);
            var placed = ChatPanelPlacement.Place(480f, 720f, column, 40f);
            var panel = ChatPanelPlacement.ClampInside(placed, visible);

            Assert.LessOrEqual(panel.xMax, column.xMin + 0.01f);
            Assert.GreaterOrEqual(panel.x, visible.x + ChatPanelPlacement.Margin - 0.01f);
            Assert.LessOrEqual(panel.yMax, visible.yMax - ChatPanelPlacement.Margin + 0.01f);
            Assert.AreEqual(164f, panel.y, 0.5f);
            Assert.LessOrEqual(panel.yMax, 504.01f);
            Assert.AreEqual(ChatPanelPlacement.Width, panel.width, 0.01f);
        }

        [Test]
        public void SmoothAnchor_SnapsAtFirstSampleAndEasesAfter()
        {
            var snapped = ChatPanelPlacement.SmoothAnchor(0f, 400f, 0f, false, out var ready);
            Assert.IsTrue(ready);
            Assert.AreEqual(400f, snapped, 0.01f);

            var held = ChatPanelPlacement.SmoothAnchor(12f, 80f, 0f, true, out ready);
            Assert.AreEqual(80f, held, 0.01f);

            var eased = ChatPanelPlacement.SmoothAnchor(400f, 0f, 0.001f, true, out ready);
            Assert.Greater(eased, 390f);
            Assert.Less(eased, 400f);
        }

        [Test]
        public void VisibleGui_UsesTheFullScreenWhenTheWorkAreaMissesTheWindow()
        {
            var visible = ChatPanelPlacement.VisibleGui(480f, 720f, 0, 0, 480, 720, 800, 0, 900, 100);
            Assert.AreEqual(0f, visible.x, 0.01f);
            Assert.AreEqual(0f, visible.y, 0.01f);
            Assert.AreEqual(480f, visible.width, 0.01f);
            Assert.AreEqual(720f, visible.height, 0.01f);
        }

        [Test]
        public void DebugInputGuard_BlocksDebugKeysOnlyWhileATextFieldIsFocused()
        {
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.T));
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.F6));
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.F9));

            DebugInputGuard.SetTextFieldFocused(true);

            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.Alpha1));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.Keypad5));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.T));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.R));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.F6));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.F7));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.F8));
            Assert.IsTrue(DebugInputGuard.IsBlocked(KeyCode.F9));
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.F10));
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.Escape));

            DebugInputGuard.SetTextFieldFocused(false);
            Assert.IsFalse(DebugInputGuard.IsBlocked(KeyCode.F8));
        }

        static void Finish(ChatConversation conversation, string user, string reply)
        {
            Assert.IsTrue(conversation.TryBeginSend(user, "persona", 0.7f, 32, out _));
            conversation.PushDelta(reply);
            conversation.Complete(ChatResult.Success(reply));
        }

        static string TranscriptOf(ChatRequest request)
        {
            var text = string.Empty;
            for (var i = 0; i < request.Messages.Count; i++)
                text += request.Messages[i].Content + "\n";
            return text;
        }

        sealed class ScriptedProvider : IChatProvider
        {
            public int Calls;
            public ChatRequest Last;
            public string[] Deltas = new string[0];
            public ChatResult Result = ChatResult.Success("ok");
            public Exception Throw;

            public string ProviderId => "fake";
            public string DisplayName => "Fake";
            public bool IsConfigured => true;

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Calls++;
                Last = request;
                if (Throw != null)
                    throw Throw;
                if (onDelta != null)
                {
                    for (var i = 0; i < Deltas.Length; i++)
                        onDelta(Deltas[i]);
                }

                return Task.FromResult(Result);
            }
        }
    }
}
