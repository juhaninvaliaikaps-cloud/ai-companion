using System.Collections.Generic;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-111: the chat panel shows one entry per message and never builds a Text near 65000 vertices.</summary>
    public class ChatTranscriptViewTests
    {
        static ChatConversation Conversation(int pairs, string reply = "fine")
        {
            var conversation = new ChatConversation(1000, int.MaxValue);
            for (var i = 0; i < pairs; i++)
            {
                Assert.IsTrue(conversation.TryBeginSend("message " + i, "persona", 0.5f, 32, out _));
                conversation.PushDelta(reply + " " + i);
                conversation.Complete(AICompanion.AI.ChatResult.Success(reply + " " + i));
            }

            return conversation;
        }

        [Test]
        public void ShortConversation_OneEntryPerMessage_LabelledLikeTheOldTranscript()
        {
            var conversation = Conversation(2);
            var view = new ChatTranscriptView();

            Assert.IsTrue(view.Update(conversation.Lines));

            Assert.AreEqual(4, view.Count);
            Assert.AreEqual(0, view.HiddenMessages);
            Assert.AreEqual("You\nmessage 0", view.Entry(0));
            Assert.AreEqual("Companion\nfine 0", view.Entry(1));
            Assert.AreEqual("Companion\nfine 1", view.Entry(3));
        }

        [Test]
        public void LongConversation_KeepsTheLast100_AndSaysOlderOnesAreHidden()
        {
            var conversation = Conversation(400);
            var view = new ChatTranscriptView();
            view.Update(conversation.Lines);

            Assert.AreEqual(ChatTranscriptView.MaxShownMessages + 1, view.Count);
            Assert.AreEqual(700, view.HiddenMessages);
            Assert.AreEqual(string.Format(ChatTranscriptView.HiddenNoteFormat, 700), view.Entry(0));
            Assert.AreEqual("You\nmessage 350", view.Entry(1));
            Assert.AreEqual("Companion\nfine 399", view.Entry(view.Count - 1));

            // The conversation itself is untouched: everything is still there for history and the export.
            Assert.AreEqual(800, conversation.Lines.Count);
            StringAssert.Contains("message 0", conversation.FormatTranscript());
        }

        [Test]
        public void NoEntry_CanReachTheVertexLimit_EvenForOneHugeMessage()
        {
            var huge = new string('x', 100000) + " the end";
            var conversation = Conversation(3, huge);
            var view = new ChatTranscriptView();
            view.Update(conversation.Lines);

            for (var i = 0; i < view.Count; i++)
            {
                // A UI Text needs 4 vertices per character; Unity stops at 65000.
                Assert.Less(view.Entry(i).Length * 4, 65000, "entry " + i);
            }

            StringAssert.StartsWith("Companion\n" + ChatTranscriptView.CutPrefix, view.Entry(1));
            StringAssert.EndsWith("the end 0", view.Entry(1), "the end of a long reply stays visible");
        }

        [Test]
        public void Streaming_ChangesOnlyTheReplyBeingWritten()
        {
            var conversation = Conversation(3);
            var view = new ChatTranscriptView();
            view.Update(conversation.Lines);

            Assert.IsTrue(conversation.TryBeginSend("next", "persona", 0.5f, 32, out _));
            view.Update(conversation.Lines);
            // The empty reply that is still streaming is not shown yet.
            Assert.AreEqual(7, view.Count);

            conversation.PushDelta("Hel");
            Assert.IsTrue(view.Update(conversation.Lines));
            Assert.AreEqual(8, view.Count);
            for (var i = 0; i < 7; i++)
                Assert.IsFalse(view.Changed(i), "entry " + i);
            Assert.IsTrue(view.Changed(7));

            conversation.PushDelta("lo");
            Assert.IsTrue(view.Update(conversation.Lines));
            Assert.AreEqual("Companion\nHello", view.Entry(7));
            Assert.IsFalse(view.Changed(6));

            // Nothing new: nothing changed.
            Assert.IsFalse(view.Update(conversation.Lines));
        }

        [Test]
        public void ClearingTheConversation_EmptiesTheView()
        {
            var conversation = Conversation(150);
            var view = new ChatTranscriptView();
            view.Update(conversation.Lines);

            conversation.ClearTranscript();
            Assert.IsTrue(view.Update(conversation.Lines));
            Assert.AreEqual(0, view.Count);
            Assert.AreEqual(0, view.HiddenMessages);

            // And it fills again from the start, without a stale note.
            Assert.IsTrue(conversation.TryBeginSend("hi", "persona", 0.5f, 32, out _));
            view.Update(conversation.Lines);
            Assert.AreEqual("You\nhi", view.Entry(0));
        }

        [Test]
        public void SystemNotes_AreShownLikeBefore()
        {
            var conversation = new ChatConversation();
            conversation.AddSystem("Voice is off.");
            var view = new ChatTranscriptView();
            view.Update(conversation.Lines);

            Assert.AreEqual("Note\nVoice is off.", view.Entry(0));
        }

        [Test]
        public void ANullList_IsAnEmptyView()
        {
            var view = new ChatTranscriptView();
            view.Update(null);
            Assert.AreEqual(0, view.Count);
            view.Update(new List<ChatLine>());
            Assert.AreEqual(0, view.Count);
        }
    }
}
