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

        sealed class ScriptedProvider : IChatProvider
        {
            public string[] Deltas = new string[0];
            public ChatResult Result = ChatResult.Success("ok");

            public string ProviderId => "fake";
            public string DisplayName => "Fake";
            public bool IsConfigured => true;

            public Task<ChatResult> StreamChat(ChatRequest request, Action<string> onDelta, CancellationToken cancellationToken)
            {
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
