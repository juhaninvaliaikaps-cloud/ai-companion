using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public class CueTranscriptTests
    {
        [Test]
        public void Exchange_SplitCueTagStaysOutOfSpeechAndHistory()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var spoken = new List<string>();
            exchange.SpokenDelta = spoken.Add;
            var provider = new ScriptedProvider
            {
                Deltas = new[] { "Hey <anim", "ation emotion=\"happy\" gesture=\"wave_hello\" />" },
                Result = ChatResult.Success("Hey <animation emotion=\"happy\" gesture=\"wave_hello\" />")
            };

            var result = exchange.Run(
                provider,
                "hi <animation emotion=\"sad\" gesture=\"wave\" />",
                "persona",
                0.5f,
                24,
                CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Hey", result.Text);
            Assert.AreEqual("Hey", conversation.Lines[1].Text);
            Assert.AreEqual(Emotion.Happy, exchange.LastCue.Emotion);
            Assert.AreEqual("wave_hello", exchange.LastCue.Gesture);
            StringAssert.DoesNotContain("<", string.Concat(spoken.ToArray()));
            StringAssert.Contains("<animation", conversation.Lines[0].Text);

            var followUp = conversation.BuildRequest("persona", 0.5f, 24);
            Assert.AreEqual("Hey", followUp.Messages[1].Content);
            StringAssert.DoesNotContain("<animation", followUp.Messages[1].Content);
        }

        [Test]
        public void Exchange_WholeReplyWithoutDeltasStillStripsTheCue()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var spoken = new List<string>();
            exchange.SpokenDelta = spoken.Add;
            var provider = new ScriptedProvider
            {
                Result = ChatResult.Success("Hello\n<animation emotion=\"thinking\" intensity=\"0.5\" />")
            };

            var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("Hello", result.Text);
            Assert.AreEqual("Hello", conversation.Lines[1].Text);
            Assert.AreEqual(Emotion.Thinking, exchange.LastCue.Emotion);
            Assert.AreEqual("Hello", string.Concat(spoken.ToArray()));
        }

        [Test]
        public void Exchange_FailedReplyReleasesAHeldComparisonWithoutSpeakingIt()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var spoken = new List<string>();
            exchange.SpokenDelta = spoken.Add;
            var provider = new ScriptedProvider
            {
                Deltas = new[] { "Score <" },
                Result = ChatResult.Fail(ChatErrorKind.Unreachable, "Couldn't reach Grok")
            };

            var result = exchange.Run(provider, "hi", "persona", 0.5f, 24, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("Score <", conversation.Lines[1].Text);
            Assert.AreEqual("Score ", string.Concat(spoken.ToArray()));
            Assert.AreEqual(string.Empty, exchange.LastCue.Gesture);
            Assert.AreEqual(Emotion.Neutral, exchange.LastCue.Emotion);
        }

        [Test]
        public void Exchange_TurnReminder_GoesToTheModelOnly_AndTheRawTagIsKeptForTheLog()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation) { TurnReminder = AnimationCueParser.TurnReminder };
            var provider = new ScriptedProvider
            {
                Result = ChatResult.Success("<Animation Emotion='sad' reaction=pout>\n*She looks away.* Fine.")
            };

            var result = exchange.Run(provider, "you're cute", "persona", 0.5f, 200, CancellationToken.None).GetAwaiter().GetResult();

            // The model got the reminder on the newest user message.
            var sent = provider.Seen.Messages[provider.Seen.Messages.Count - 1];
            Assert.AreEqual(ChatRole.User, sent.Role);
            StringAssert.StartsWith("you're cute", sent.Content);
            StringAssert.EndsWith(AnimationCueParser.TurnReminder, sent.Content);

            // Nothing else saw it: the transcript, the display, and the next request's history.
            Assert.AreEqual("you're cute", conversation.Lines[0].Text);
            var followUp = conversation.BuildRequest("persona", 0.5f, 200);
            Assert.AreEqual("you're cute", followUp.Messages[0].Content);

            // A sloppy tag at the start of the reply: read, stripped, and kept raw for the log.
            Assert.IsTrue(result.Ok);
            Assert.AreEqual("*She looks away.* Fine.", result.Text);
            Assert.AreEqual(Emotion.Sad, exchange.LastCue.Emotion);
            Assert.AreEqual("pout", exchange.LastCue.Reaction);
            Assert.AreEqual(CueParseStatus.Parsed, exchange.LastCueStatus);
            Assert.AreEqual("<Animation Emotion='sad' reaction=pout>", exchange.LastCueTag);
            Assert.AreEqual("Cue tag: <Animation Emotion='sad' reaction=pout>", AnimationCueParser.DescribeTag(exchange.LastCueTag, exchange.LastCueStatus));
        }

        [Test]
        public void Exchange_ReplyWithoutATag_LogsThatNoneWasFound()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation) { TurnReminder = AnimationCueParser.TurnReminder };
            var provider = new ScriptedProvider { Result = ChatResult.Success("*Her cheeks flush.* Ah... I suppose.") };

            exchange.Run(provider, "hi", "persona", 0.5f, 200, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(CueParseStatus.None, exchange.LastCueStatus);
            Assert.AreEqual("Cue tag: none found in the reply", AnimationCueParser.DescribeTag(exchange.LastCueTag, exchange.LastCueStatus));
        }

        [Test]
        public void Exchange_CueSerial_CountsEachFinishedReplyOnce()
        {
            // JUH-103: the mood takes each reply's cue once, keyed on this counter.
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var provider = new ScriptedProvider { Result = ChatResult.Success("<animation emotion=\"happy\" />\nHi.") };
            Assert.AreEqual(0, exchange.CueSerial);

            exchange.Run(provider, "hi", "persona", 0.5f, 200, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(1, exchange.CueSerial);

            // Clearing the transcript is not a new reply.
            conversation.ClearTranscript();
            Assert.AreEqual(1, exchange.CueSerial);

            exchange.Run(provider, "again", "persona", 0.5f, 200, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(2, exchange.CueSerial);
        }

        [Test]
        public void Exchange_WithoutAReminder_SendsTheMessageAsTyped()
        {
            var conversation = new ChatConversation();
            var exchange = new ChatExchange(conversation);
            var provider = new ScriptedProvider();

            exchange.Run(provider, "hi", "persona", 0.5f, 200, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("hi", provider.Seen.Messages[0].Content);
        }

        sealed class ScriptedProvider : IChatProvider
        {
            public string[] Deltas = new string[0];
            public ChatResult Result = ChatResult.Success("ok");
            public ChatRequest Seen;

            public string ProviderId => "fake";
            public string DisplayName => "Fake";
            public bool IsConfigured => true;

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
                Seen = request;
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
