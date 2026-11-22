using AICompanion.AI;
using AICompanion.Core;
using AICompanion.NewUI;
using AICompanion.UI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-174: every floating change also stays as a readable line in the chat box; scene text is green.</summary>
    public sealed class ChatChangeLinesTests
    {
        [TearDown]
        public void CloseGate()
        {
            IntimateExpressions.Reset();
        }

        static FeedbackPiece Piece(FeedbackKind kind, string amount, string label)
        {
            return new FeedbackPiece { Kind = kind, Amount = amount, Label = label };
        }

        [Test]
        public void EachPiece_HasAReadableLine()
        {
            Assert.AreEqual("+1.5 affection · Compliment", RelationshipFeedback.ChatText(Piece(FeedbackKind.Up, "+1.5", "Compliment")));
            Assert.AreEqual("-0.9 affection · Flirt", RelationshipFeedback.ChatText(Piece(FeedbackKind.Down, "-0.9", "Flirt")));
            Assert.AreEqual("Critical! +3.0 affection · Compliment", RelationshipFeedback.ChatText(Piece(FeedbackKind.CritUp, "+3.0", "Compliment")));
            Assert.AreEqual("Secret found: praise for her work", RelationshipFeedback.ChatText(Piece(FeedbackKind.Secret, "", "Secret found: praise for her work")));
            Assert.AreEqual("+8 desire (now 48)", RelationshipFeedback.ChatText(Piece(FeedbackKind.DesireUp, "+8", "Desire 48")));
            Assert.AreEqual("Release · desire 7", RelationshipFeedback.ChatText(Piece(FeedbackKind.DesireDown, "", "Release · desire 7")));
        }

        [Test]
        public void DesireAndAdultSecrets_AreMarkedAdult()
        {
            var feedback = new RelationshipFeedback();
            feedback.AddDesire(new DesireNotice { Delta = 8f, Desire = 48f }, 0f, true);
            Assert.IsTrue(feedback.Piece(feedback.Count - 1).Adult);

            var change = RelationshipMath.Land(LandingEvent.Complimented, 0.5f, 10f, 30f, true, 1f);
            var adult = RelationshipNotice.From(change, 9, "a kink", true);
            Assert.IsTrue(adult.SecretAdult);
            var plain = RelationshipNotice.From(change, 9, "her design work");
            Assert.IsFalse(plain.SecretAdult);
            Assert.IsFalse(RelationshipNotice.From(change, 9, null, true).SecretAdult, "no secret, nothing adult");

            var pieces = new RelationshipFeedback();
            pieces.Add(adult, 0f, true);
            var foundAdult = false;
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces.Piece(i).Kind == FeedbackKind.Secret)
                    foundAdult = pieces.Piece(i).Adult;
            }

            Assert.IsTrue(foundAdult);
        }

        [Test]
        public void AnAdultChangeLine_ShowsOnlyWhileTheGateIsOpen_AndNeverGoesToTheModel()
        {
            var conversation = new ChatConversation();
            conversation.AddChange("+8 desire (now 48)", (int)FeedbackKind.DesireUp, true);
            conversation.AddChange("+1.5 affection · Compliment", (int)FeedbackKind.Up, false);
            var adult = conversation.Lines[0];
            Assert.AreEqual(ChatLineTag.Change, adult.Tag);
            Assert.IsTrue(adult.LocalOnly);
            Assert.IsFalse(ChatConversation.Shows(adult), "gate closed");
            Assert.IsTrue(ChatConversation.Shows(conversation.Lines[1]));
            StringAssert.DoesNotContain("desire", conversation.FormatTranscript());

            IntimateExpressions.Configure(true, true, true);
            Assert.IsTrue(ChatConversation.Shows(adult));

            ChatRequest request;
            Assert.IsTrue(conversation.TryBeginSend("hi", "persona", 0.7f, 64, out request));
            for (var i = 0; i < request.Messages.Count; i++)
                StringAssert.DoesNotContain("affection", request.Messages[i].Content, "change lines are notes: never sent");
        }

        [Test]
        public void TagsSurviveTheHistoryFile_AndOldSceneNotesAreKnown()
        {
            var conversation = new ChatConversation();
            conversation.AddScene("Scene: The tavern is loud tonight.");
            conversation.AddChange("+8 desire (now 48)", (int)FeedbackKind.DesireUp, true);
            conversation.AddSystem("API key saved on this PC.");
            var json = ChatHistoryCodec.Write(conversation.Lines);

            var back = new ChatConversation();
            ChatHistoryCodec.ReadInto(json, back);
            Assert.AreEqual(3, back.Lines.Count);
            Assert.AreEqual(ChatLineTag.Scene, back.Lines[0].Tag);
            Assert.AreEqual(ChatLineTag.Change, back.Lines[1].Tag);
            Assert.AreEqual((int)FeedbackKind.DesireUp, back.Lines[1].Change);
            Assert.IsTrue(back.Lines[1].Adult);
            Assert.AreEqual(ChatLineTag.None, back.Lines[2].Tag);

            var old = new ChatConversation();
            ChatHistoryCodec.ReadInto("{\"version\":1,\"lines\":[{\"role\":\"note\",\"text\":\"While you were away: she trained.\"}]}", old);
            Assert.AreEqual(ChatLineTag.Scene, old.Lines[0].Tag, "a history from before has its scene notes coloured too");
        }

        [Test]
        public void SceneTextIsGreen_AndChangesTakeTheirPiecesColour()
        {
            var conversation = new ChatConversation();
            conversation.AddScene("Scene: x");
            conversation.AddChange("+1 affection · Thanks", (int)FeedbackKind.Up, false);
            conversation.AddChange("-1 affection · Flirt", (int)FeedbackKind.Down, false);
            conversation.AddChange("Secret found: x", (int)FeedbackKind.Secret, false);
            conversation.AddSystem("plain");
            Assert.AreEqual(ChatLineColors.Scene, ChatLineColors.For(conversation.Lines[0]));
            Assert.AreEqual(ChatLineColors.Up, ChatLineColors.For(conversation.Lines[1]));
            Assert.AreEqual(ChatLineColors.Down, ChatLineColors.For(conversation.Lines[2]));
            Assert.AreEqual(ChatLineColors.Gold, ChatLineColors.For(conversation.Lines[3]));
            Assert.AreEqual(string.Empty, ChatLineColors.For(conversation.Lines[4]));
            Assert.AreEqual("<i><color=" + ChatLineColors.Scene + ">Scene: x</color></i>", ChatLineColors.Format(conversation.Lines[0], "Scene: x"));
            Assert.AreEqual("plain", ChatLineColors.Format(conversation.Lines[4], "plain"));
        }
    }
}
