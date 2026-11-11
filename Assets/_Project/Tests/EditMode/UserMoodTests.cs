using System.Diagnostics;
using System.Text;
using AICompanion.AnimationDirector;
using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>
    /// JUH-21. The detector, the avatar's quick reaction, and the prompt hint are plain
    /// logic, so none of these tests needs a scene or a network.
    /// </summary>
    public class UserMoodTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n ")]
        [TestCase("What time is it?")]
        [TestCase("Can you open the settings window")]
        [TestCase("never mind, it is fine")]
        public void Detect_NoEmotionWords_IsNeutral(string text)
        {
            var mood = UserMoodDetector.Detect(text);

            Assert.IsTrue(mood.IsNeutral);
            Assert.AreEqual(UserEmotion.Neutral, mood.Emotion);
            Assert.AreEqual(0f, mood.Intensity, 0.0001f);
        }

        [TestCase("I'm so happy today!", UserEmotion.Happy)]
        [TestCase("That is awesome", UserEmotion.Happy)]
        [TestCase("I can't wait for the weekend", UserEmotion.Happy)]
        [TestCase("I feel really sad and lonely", UserEmotion.Sad)]
        [TestCase("I'm depressed", UserEmotion.Sad)]
        [TestCase("I lost again... I give up", UserEmotion.Sad)]
        [TestCase("This is so frustrating, I hate it", UserEmotion.Angry)]
        [TestCase("ugh, stupid bug", UserEmotion.Angry)]
        [TestCase("I'm scared about tomorrow", UserEmotion.Anxious)]
        [TestCase("I am really worried and stressed", UserEmotion.Anxious)]
        [TestCase("Wow, I did not expect that", UserEmotion.Surprised)]
        [TestCase("you are so cute", UserEmotion.Affectionate)]
        [TestCase("I miss you", UserEmotion.Affectionate)]
        [TestCase("I don't understand this at all", UserEmotion.Confused)]
        [TestCase("huh? what do you mean??", UserEmotion.Confused)]
        public void Detect_EnglishMessage_FindsTheEmotion(string text, UserEmotion expected)
        {
            Assert.AreEqual(expected, UserMoodDetector.Detect(text).Emotion);
        }

        [TestCase("Olen tosi iloinen", UserEmotion.Happy)]
        [TestCase("Kiitos!", UserEmotion.Happy)]
        [TestCase("Olen väsynyt ja surullinen", UserEmotion.Sad)]
        [TestCase("Minua vituttaa", UserEmotion.Angry)]
        [TestCase("Minua pelottaa", UserEmotion.Anxious)]
        [TestCase("Rakastan sinua", UserEmotion.Affectionate)]
        [TestCase("En ymmärrä", UserEmotion.Confused)]
        public void Detect_FinnishMessage_FindsTheEmotion(string text, UserEmotion expected)
        {
            Assert.AreEqual(expected, UserMoodDetector.Detect(text).Emotion);
        }

        [TestCase("great :)", UserEmotion.Happy)]
        [TestCase("that hurt :(", UserEmotion.Sad)]
        [TestCase("<3", UserEmotion.Affectionate)]
        [TestCase("I think I failed the exam \U0001F62D", UserEmotion.Sad)]
        [TestCase("\U0001F602\U0001F602 that was hilarious", UserEmotion.Happy)]
        public void Detect_EmoticonsAndEmoji_Count(string text, UserEmotion expected)
        {
            Assert.AreEqual(expected, UserMoodDetector.Detect(text).Emotion);
        }

        [Test]
        public void Detect_UrlIsNotAnEmoticon()
        {
            Assert.IsTrue(UserMoodDetector.Detect("check https://example.com/page for details").IsNeutral);
        }

        [Test]
        public void Detect_NegatedHappy_IsMildlySad()
        {
            var mood = UserMoodDetector.Detect("I'm not happy");

            Assert.AreEqual(UserEmotion.Sad, mood.Emotion);
            Assert.Less(mood.Intensity, UserMoodDetector.Detect("I'm sad").Intensity);
        }

        [TestCase("I'm not sad")]
        [TestCase("not bad at all")]
        [TestCase("I am not angry")]
        [TestCase("I'm not scared")]
        public void Detect_NegatedDistress_IsDropped(string text)
        {
            Assert.IsTrue(UserMoodDetector.Detect(text).IsNeutral);
        }

        [Test]
        public void Detect_NegationStopsAtAComma()
        {
            Assert.AreEqual(UserEmotion.Happy, UserMoodDetector.Detect("no, I'm happy").Emotion);
        }

        [Test]
        public void Detect_NoAsAReply_DoesNotNegateALaterWord()
        {
            // "no" only negates the very next word.
            Assert.AreEqual(UserEmotion.Happy, UserMoodDetector.Detect("No I'm happy").Emotion);
            Assert.IsTrue(UserMoodDetector.Detect("that was no fun").IsNeutral);
        }

        [Test]
        public void Detect_WithoutIsNotANegation()
        {
            Assert.AreEqual(UserEmotion.Sad, UserMoodDetector.Detect("without you I'm sad").Emotion);
        }

        [Test]
        public void Detect_KindAsAnAdjective_DoesNotWeakenTheNextWord()
        {
            Assert.AreEqual(UserEmotion.Affectionate, UserMoodDetector.Detect("you are kind and sweet").Emotion);
            Assert.Less(UserMoodDetector.Detect("I'm kinda sad").Intensity, UserMoodDetector.Detect("I'm sad").Intensity);
        }

        [Test]
        public void Detect_LikeYou_IsAffectionOnlyAsAWholePhrase()
        {
            Assert.IsTrue(UserMoodDetector.Detect("like you said, it is at five").IsNeutral);
            Assert.IsTrue(UserMoodDetector.Detect("that sounds like you").IsNeutral);
            Assert.AreEqual(UserEmotion.Affectionate, UserMoodDetector.Detect("I like you").Emotion);
        }

        [Test]
        public void Detect_NegatorInsideAPhrase_DoesNotNegateTheNextWord()
        {
            // "no way" is surprise. Its "no" must not turn "amazing" into sadness.
            Assert.AreEqual(UserEmotion.Happy, UserMoodDetector.Detect("No way that's amazing!").Emotion);
        }

        [Test]
        public void Detect_PhraseWinsOverItsWords()
        {
            // "love" alone is happy. "I love you" is affection.
            Assert.AreEqual(UserEmotion.Happy, UserMoodDetector.Detect("I love this song").Emotion);
            Assert.AreEqual(UserEmotion.Affectionate, UserMoodDetector.Detect("I love you").Emotion);
        }

        [Test]
        public void Detect_NegatedAffection_IsSad()
        {
            Assert.AreEqual(UserEmotion.Sad, UserMoodDetector.Detect("I don't love you").Emotion);
        }

        [Test]
        public void Detect_IntensifierAndDampener_MoveTheIntensity()
        {
            var plain = UserMoodDetector.Detect("I am sad").Intensity;
            var strong = UserMoodDetector.Detect("I am very sad").Intensity;
            var soft = UserMoodDetector.Detect("I am a bit sad").Intensity;

            Assert.Greater(strong, plain);
            Assert.Less(soft, plain);
            Assert.Greater(soft, 0f);
        }

        [Test]
        public void Detect_ExclamationMarks_RaiseTheIntensityUpToACap()
        {
            var plain = UserMoodDetector.Detect("I'm glad").Intensity;
            var one = UserMoodDetector.Detect("I'm glad!").Intensity;
            var three = UserMoodDetector.Detect("I'm glad!!!").Intensity;
            var ten = UserMoodDetector.Detect("I'm glad!!!!!!!!!!").Intensity;

            Assert.AreEqual(plain + UserMoodDetector.ExclamationBoost, one, 0.0001f);
            Assert.Greater(three, one);
            Assert.AreEqual(three, ten, 0.0001f);
        }

        [Test]
        public void Detect_ShoutedWords_RaiseTheIntensity()
        {
            var quiet = UserMoodDetector.Detect("this is annoying me").Intensity;
            var loud = UserMoodDetector.Detect("THIS IS ANNOYING ME").Intensity;

            Assert.AreEqual(quiet + UserMoodDetector.CapsBoost, loud, 0.0001f);
        }

        [Test]
        public void Detect_Interrobang_IsSurprise()
        {
            Assert.AreEqual(UserEmotion.Surprised, UserMoodDetector.Detect("you did what?!").Emotion);
        }

        [Test]
        public void Detect_IntensityStaysInRange()
        {
            var mood = UserMoodDetector.Detect("I AM SO ANGRY AND FURIOUS, I HATE THIS STUPID THING!!!");

            Assert.AreEqual(UserEmotion.Angry, mood.Emotion);
            Assert.AreEqual(1f, mood.Intensity, 0.0001f);
        }

        [Test]
        public void Detect_ReadsOnlyTheFirstCharacters()
        {
            var filler = new StringBuilder();
            while (filler.Length < UserMoodDetector.MaxCharacters + 10)
                filler.Append("word ");

            Assert.IsTrue(UserMoodDetector.Detect(filler + "sad").IsNeutral);
            Assert.AreEqual(UserEmotion.Sad, UserMoodDetector.Detect("sad " + filler).Emotion);
        }

        [Test]
        public void Detect_LongMessage_IsFastEnoughForALiveReaction()
        {
            var text = new StringBuilder();
            while (text.Length < 20000)
                text.Append("I am really happy but a bit worried, and I don't understand why. ");
            var message = text.ToString();

            UserMoodDetector.Detect(message);
            var watch = Stopwatch.StartNew();
            UserMoodDetector.Detect(message);
            watch.Stop();

            // The issue asks for under about 100 ms. A chat line takes far less.
            Assert.Less(watch.ElapsedMilliseconds, 100L);
        }

        [Test]
        public void Mood_NeutralAlwaysHasZeroIntensity()
        {
            Assert.AreEqual(0f, new UserMood(UserEmotion.Neutral, 0.9f).Intensity, 0.0001f);
            Assert.AreEqual(1f, new UserMood(UserEmotion.Happy, 4f).Intensity, 0.0001f);
        }

        [Test]
        public void Mood_LogLine_NamesTheEmotionAndNotTheMessage()
        {
            Assert.AreEqual("User mood: neutral", UserMood.Neutral.ToLogString());
            Assert.AreEqual("User mood: sad 0.50", new UserMood(UserEmotion.Sad, 0.5f).ToLogString());
        }

        [TestCase(UserEmotion.Happy, Emotion.Happy)]
        [TestCase(UserEmotion.Sad, Emotion.Concerned)]
        [TestCase(UserEmotion.Angry, Emotion.Concerned)]
        [TestCase(UserEmotion.Anxious, Emotion.Concerned)]
        [TestCase(UserEmotion.Surprised, Emotion.Surprised)]
        [TestCase(UserEmotion.Affectionate, Emotion.Shy)]
        [TestCase(UserEmotion.Confused, Emotion.Thinking)]
        public void Reaction_MapsTheUsersMoodToHerFace(UserEmotion user, Emotion face)
        {
            AnimationCue cue;
            var found = UserMoodReaction.TryGetCue(new UserMood(user, 1f), out cue);

            Assert.IsTrue(found);
            Assert.AreEqual(face, cue.Emotion);
            Assert.AreEqual(string.Empty, cue.Gesture);
            Assert.AreEqual(UserMoodReaction.MaxIntensity, cue.Intensity, 0.0001f);
        }

        [Test]
        public void Reaction_NeutralMood_GivesNoCue()
        {
            AnimationCue cue;

            Assert.IsFalse(UserMoodReaction.TryGetCue(UserMood.Neutral, out cue));
        }

        [Test]
        public void Reaction_IsSofterForAWeakMood()
        {
            AnimationCue weak;
            AnimationCue strong;
            UserMoodReaction.TryGetCue(new UserMood(UserEmotion.Sad, 0.1f), out weak);
            UserMoodReaction.TryGetCue(new UserMood(UserEmotion.Sad, 0.9f), out strong);

            Assert.Less(weak.Intensity, strong.Intensity);
            Assert.GreaterOrEqual(weak.Intensity, UserMoodReaction.MinIntensity);
            Assert.LessOrEqual(strong.Intensity, UserMoodReaction.MaxIntensity);
        }

        [Test]
        public void Reaction_ReachesTheDirectorAsAnEmotionOnlyDecision()
        {
            AnimationCue cue;
            UserMoodReaction.TryGetCue(UserMoodDetector.Detect("I'm scared about tomorrow"), out cue);

            // No gesture, so the model never looks at the catalog and the rulebook never blocks it.
            var decision = new AnimationDirectorModel(null).Decide(cue);

            Assert.AreEqual(DirectorAction.Emotion, decision.Action);
            Assert.AreEqual(Emotion.Concerned, decision.Emotion);
            Assert.AreEqual(string.Empty, decision.StateName);
        }

        [Test]
        public void Prompt_NeutralMood_AddsNothing()
        {
            Assert.AreEqual(string.Empty, UserMoodPrompt.Line(UserMood.Neutral));
            Assert.AreEqual("You are Leia.", UserMoodPrompt.Append("You are Leia.", UserMood.Neutral));
        }

        [Test]
        public void Prompt_AddsOneHintLineAfterThePrompt()
        {
            var prompt = UserMoodPrompt.Append("You are Leia.\n", new UserMood(UserEmotion.Anxious, 0.8f));

            StringAssert.StartsWith("You are Leia.\n\nMood hint:", prompt);
            StringAssert.Contains("anxious or worried", prompt);
            StringAssert.Contains("(strong)", prompt);
        }

        [Test]
        public void Prompt_EmptyPrompt_StaysEmptySoTheDefaultStillApplies()
        {
            var mood = new UserMood(UserEmotion.Happy, 0.6f);

            Assert.IsNull(UserMoodPrompt.Append(null, mood));
            Assert.AreEqual("  ", UserMoodPrompt.Append("  ", mood));
        }

        [TestCase(0.2f, "mild")]
        [TestCase(0.45f, "clear")]
        [TestCase(0.74f, "clear")]
        [TestCase(0.75f, "strong")]
        [TestCase(1f, "strong")]
        public void Prompt_StrengthWords(float intensity, string expected)
        {
            Assert.AreEqual(expected, UserMoodPrompt.Strength(intensity));
        }
    }
}
