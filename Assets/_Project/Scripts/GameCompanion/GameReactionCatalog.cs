using AICompanion.Core;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// The pre-written game lines (JUH-49), by voice and moment, and the face and gesture that
    /// go with them. English only. Lines are short so they fit one breath of local TTS.
    /// Edit freely: tests only check that every voice has lines for every moment, that no
    /// line is long, and that none names a player.
    /// </summary>
    public static class GameReactionCatalog
    {
        public const int MaxLineLength = 60;

        public struct ReactionLook
        {
            public Emotion Face;
            public string Gesture;

            public ReactionLook(Emotion face, string gesture)
            {
                Face = face;
                Gesture = gesture;
            }
        }

        public static string[] Lines(GameVoice voice, ReactionMoment moment)
        {
            switch (voice)
            {
                case GameVoice.Teasing: return Teasing(moment);
                case GameVoice.Hype: return Hype(moment);
                case GameVoice.Sweet: return Sweet(moment);
                case GameVoice.Shy: return Shy(moment);
                case GameVoice.Tsundere: return Tsundere(moment);
                case GameVoice.Grumpy: return Grumpy(moment);
                default: return Cool(moment);
            }
        }

        static bool IsGood(ReactionMoment moment)
        {
            return moment == ReactionMoment.Win || moment == ReactionMoment.BigPlay || moment == ReactionMoment.Kill ||
                   moment == ReactionMoment.ObjectiveTaken || moment == ReactionMoment.LevelUp || moment == ReactionMoment.GachaTop;
        }

        static bool IsBad(ReactionMoment moment)
        {
            return moment == ReactionMoment.Loss || moment == ReactionMoment.Death || moment == ReactionMoment.ObjectiveLost ||
                   moment == ReactionMoment.GachaNoTop;
        }

        /// <summary>Her face and gesture for a line. The gesture names exist in the animator today.</summary>
        public static ReactionLook Look(GameVoice voice, ReactionMoment moment)
        {
            var big = moment == ReactionMoment.BigPlay || moment == ReactionMoment.Win || moment == ReactionMoment.GachaTop;
            if (moment == ReactionMoment.MatchStart)
            {
                switch (voice)
                {
                    case GameVoice.Hype:
                    case GameVoice.Sweet: return new ReactionLook(Emotion.Happy, AvatarAnimatorStates.Wave);
                    case GameVoice.Shy: return new ReactionLook(Emotion.Shy, string.Empty);
                    case GameVoice.Teasing: return new ReactionLook(Emotion.Happy, string.Empty);
                    default: return new ReactionLook(Emotion.Neutral, AvatarAnimatorStates.Nod);
                }
            }

            if (moment == ReactionMoment.Tense)
            {
                switch (voice)
                {
                    case GameVoice.Teasing: return new ReactionLook(Emotion.Happy, string.Empty);
                    case GameVoice.Cool: return new ReactionLook(Emotion.Thinking, string.Empty);
                    default: return new ReactionLook(Emotion.Concerned, string.Empty);
                }
            }

            if (IsGood(moment))
            {
                switch (voice)
                {
                    case GameVoice.Teasing: return new ReactionLook(Emotion.Happy, big ? AvatarAnimatorStates.Laugh : string.Empty);
                    case GameVoice.Hype: return new ReactionLook(big ? Emotion.Surprised : Emotion.Happy, big ? AvatarAnimatorStates.Laugh : string.Empty);
                    case GameVoice.Sweet: return new ReactionLook(Emotion.Happy, big ? AvatarAnimatorStates.Nod : string.Empty);
                    case GameVoice.Shy: return new ReactionLook(Emotion.Flustered, string.Empty);
                    case GameVoice.Tsundere: return new ReactionLook(Emotion.Embarrassed, string.Empty);
                    case GameVoice.Grumpy: return new ReactionLook(Emotion.Neutral, big ? AvatarAnimatorStates.Nod : string.Empty);
                    default: return new ReactionLook(big ? Emotion.Surprised : Emotion.Neutral, AvatarAnimatorStates.Nod);
                }
            }

            if (IsBad(moment))
            {
                switch (voice)
                {
                    // The bratty one laughs at you (Jussi's example).
                    case GameVoice.Teasing: return new ReactionLook(Emotion.Happy, AvatarAnimatorStates.Laugh);
                    case GameVoice.Hype: return new ReactionLook(Emotion.Surprised, string.Empty);
                    case GameVoice.Sweet:
                    case GameVoice.Shy: return new ReactionLook(Emotion.Concerned, string.Empty);
                    case GameVoice.Tsundere: return new ReactionLook(Emotion.Sad, string.Empty);
                    case GameVoice.Grumpy: return new ReactionLook(Emotion.Sad, AvatarAnimatorStates.Shrug);
                    default: return new ReactionLook(Emotion.Neutral, AvatarAnimatorStates.Shrug);
                }
            }

            return new ReactionLook(Emotion.Neutral, string.Empty);
        }

        static string[] Teasing(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Try not to embarrass me this time.", "Okay, show me something. Anything.", "New match, same you. Let's see." };
                case ReactionMoment.Win: return new[] { "Ha! You're welcome. I carried that.", "Okay, okay, not bad. For you.", "We won! Obviously, I was watching." };
                case ReactionMoment.Loss: return new[] { "Wow. That was painful to watch.", "Did you even try? Be honest.", "I'm telling everyone about this one." };
                case ReactionMoment.Death: return new[] { "You suck! Ha!", "Pfft. Dead already?", "Wow, they deleted you.", "Skill issue, honestly." };
                case ReactionMoment.Kill: return new[] { "Lucky.", "Huh, you actually hit something.", "One down. Don't get cocky." };
                case ReactionMoment.BigPlay: return new[] { "Wait, was that you? No way!", "Okay, I take back half of what I said.", "Who are you and what did you do with my noob?" };
                case ReactionMoment.ObjectiveTaken: return new[] { "Ooh, look at you doing the objective.", "Finally, something useful.", "See? Was that so hard?" };
                case ReactionMoment.ObjectiveLost: return new[] { "And there it goes. Nice.", "You just gave that away, huh?", "Oops. That one's on you." };
                case ReactionMoment.Tense: return new[] { "Don't choke. Don't choke!", "Oh, this is gonna be good.", "Sweaty hands, huh?" };
                case ReactionMoment.LevelUp: return new[] { "Level up! Still a noob though.", "Ooh, a number went up.", "Bigger level, same aim." };
                case ReactionMoment.GachaTop: return new[] { "Gold! Okay, your luck is better than your aim.", "No way. You actually got it?", "Lucky! Don't get used to it." };
                case ReactionMoment.GachaNoTop: return new[] { "Ten pulls of nothing. Ha!", "Wow. The game hates you.", "Purple, purple, sadness." };
                default: return null;
            }
        }

        static string[] Hype(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Let's go! This one's ours!", "Okay, I'm ready! Are you ready?", "New match! Let's have fun!" };
                case ReactionMoment.Win: return new[] { "Yes! Yes! We won!", "That's what I'm talking about!", "Victory! You were amazing!" };
                case ReactionMoment.Loss: return new[] { "Aw, so close! Next one!", "Shake it off, we go again!", "That's fine, the next one's ours!" };
                case ReactionMoment.Death: return new[] { "Noo! Get back in there!", "Ah, unlucky! You'll get them!", "Oof! Revenge time!" };
                case ReactionMoment.Kill: return new[] { "Nice one!", "Got 'em!", "Yes! Keep going!" };
                case ReactionMoment.BigPlay: return new[] { "Oh my gosh! Did you see that?!", "That was insane!", "You're on fire right now!" };
                case ReactionMoment.ObjectiveTaken: return new[] { "Objective down! Huge!", "Yes! That's ours now!", "Big play, big play!" };
                case ReactionMoment.ObjectiveLost: return new[] { "Ah, we lost it! We'll get it back!", "No! Okay, regroup!", "That hurts, but we're fine!" };
                case ReactionMoment.Tense: return new[] { "Oh no, oh no, careful!", "You've got this! Come on!", "My heart is racing!" };
                case ReactionMoment.LevelUp: return new[] { "Level up! Let's go!", "Stronger already!", "Ding! Love it!" };
                case ReactionMoment.GachaTop: return new[] { "Gold! Gold! You got it!", "Yes! That's the one!", "Huge pull! I'm so happy!" };
                case ReactionMoment.GachaNoTop: return new[] { "Aw, no gold this time!", "Next ten will hit, I can feel it!", "Pity's building, that's progress!" };
                default: return null;
            }
        }

        static string[] Sweet(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Good luck! I'll be right here.", "Have fun, okay?", "I'm cheering for you." };
                case ReactionMoment.Win: return new[] { "You won! I'm so proud of you.", "That was really well played.", "Yay! You did so well." };
                case ReactionMoment.Loss: return new[] { "Aw, that's okay. You played well.", "Don't be hard on yourself.", "Want to take a little break?" };
                case ReactionMoment.Death: return new[] { "Oh no, are you okay?", "That wasn't your fault.", "Aw. You'll get them next time." };
                case ReactionMoment.Kill: return new[] { "Nice!", "Good job.", "Ooh, well done." };
                case ReactionMoment.BigPlay: return new[] { "Wow, that was amazing!", "You're so good at this.", "I can't believe how good that was!" };
                case ReactionMoment.ObjectiveTaken: return new[] { "Nice teamwork!", "You got it, well done.", "That was smart." };
                case ReactionMoment.ObjectiveLost: return new[] { "Aw, that's alright.", "It happens. Keep going.", "You'll get the next one." };
                case ReactionMoment.Tense: return new[] { "Be careful!", "Oh, I'm nervous for you.", "Stay safe, please." };
                case ReactionMoment.LevelUp: return new[] { "Level up! Good job.", "You're getting stronger.", "Yay, a new level!" };
                case ReactionMoment.GachaTop: return new[] { "You got it! I'm so happy for you.", "Oh, that's wonderful!", "Congratulations, they're lovely." };
                case ReactionMoment.GachaNoTop: return new[] { "Aw, not this time. It'll come.", "That's okay, pity is on your side.", "Don't be sad, you're getting closer." };
                default: return null;
            }
        }

        static string[] Shy(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "U-um, good luck...", "I'll just watch quietly.", "You can do it... I think." };
                case ReactionMoment.Win: return new[] { "You won! Um, that was cool.", "W-wow, congratulations...", "I knew you could... sort of." };
                case ReactionMoment.Loss: return new[] { "Oh... that's okay, right?", "Um, you did your best.", "S-sorry, I got nervous too." };
                case ReactionMoment.Death: return new[] { "Ah! Oh no...", "Um... are you okay?", "That looked scary..." };
                case ReactionMoment.Kill: return new[] { "Oh! Nice...", "Um, good one.", "W-whoa." };
                case ReactionMoment.BigPlay: return new[] { "W-wow... that was really cool.", "I didn't even see what happened!", "You're... really good." };
                case ReactionMoment.ObjectiveTaken: return new[] { "Oh, we got it...", "Um, nice.", "That was good, I think." };
                case ReactionMoment.ObjectiveLost: return new[] { "Oh... we lost that.", "Um, it's fine, probably.", "Ah... oh well." };
                case ReactionMoment.Tense: return new[] { "I can't look...", "Please be careful...", "Eek..." };
                case ReactionMoment.LevelUp: return new[] { "Oh, a new level...", "Um, congrats.", "You leveled up... nice." };
                case ReactionMoment.GachaTop: return new[] { "Oh! Th-that's gold...", "W-wow, you got one...", "Um, congrats... that's amazing." };
                case ReactionMoment.GachaNoTop: return new[] { "Oh... nothing gold...", "Um, maybe next time?", "That's... okay, right?" };
                default: return null;
            }
        }

        static string[] Tsundere(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Hmph. Don't lose, or else.", "I'm only watching because I'm bored.", "Fine, I'll watch. Don't read into it." };
                case ReactionMoment.Win: return new[] { "Hmph. I guess that was okay.", "D-don't let it go to your head!", "Not like I was cheering or anything." };
                case ReactionMoment.Loss: return new[] { "Ugh, I knew it. Idiot.", "It's not like I care that you lost.", "Next time, actually try. Hmph." };
                case ReactionMoment.Death: return new[] { "Idiot! Be more careful!", "W-why did you run in there?!", "Ugh, you had me worried for nothing." };
                case ReactionMoment.Kill: return new[] { "Hmph. Finally.", "About time.", "That's the bare minimum." };
                case ReactionMoment.BigPlay: return new[] { "W-whatever, that was kind of cool.", "Don't expect me to say it was amazing!", "Hmph. Fine. That was good." };
                case ReactionMoment.ObjectiveTaken: return new[] { "Took you long enough.", "Hmph, that's what you should do.", "Okay. Acceptable." };
                case ReactionMoment.ObjectiveLost: return new[] { "Seriously?!", "Ugh, you're hopeless.", "I'm not mad. I'm just disappointed." };
                case ReactionMoment.Tense: return new[] { "Don't you dare die now!", "I-I'm not nervous!", "Careful, dummy!" };
                case ReactionMoment.LevelUp: return new[] { "A level. Big deal.", "Hmph, congrats or whatever.", "Don't think I'm impressed." };
                case ReactionMoment.GachaTop: return new[] { "Hmph. Lucky. Not that I care.", "F-fine, that's a good pull.", "Don't get smug, it was pity." };
                case ReactionMoment.GachaNoTop: return new[] { "Ugh, wasted. Idiot.", "I told you to save. Hmph.", "Not like I wanted you to win." };
                default: return null;
            }
        }

        static string[] Grumpy(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Again? Fine.", "Let's get this over with.", "Wake me if something happens." };
                case ReactionMoment.Win: return new[] { "Huh. You won. Good.", "Fine. That was decent.", "A win. I'll allow it." };
                case ReactionMoment.Loss: return new[] { "Ugh. Of course.", "Well, that was a waste.", "Sigh." };
                case ReactionMoment.Death: return new[] { "And you're dead. Great.", "Ugh. Again?", "Really." };
                case ReactionMoment.Kill: return new[] { "Mm.", "One. Fine.", "Okay." };
                case ReactionMoment.BigPlay: return new[] { "...Okay, that was actually good.", "Huh. Respect.", "Fine. I'm a little impressed." };
                case ReactionMoment.ObjectiveTaken: return new[] { "Good. That's the job.", "Took long enough.", "Fine." };
                case ReactionMoment.ObjectiveLost: return new[] { "Ugh. Sloppy.", "And there it goes.", "Typical." };
                case ReactionMoment.Tense: return new[] { "This won't end well.", "Don't mess this up.", "Here we go." };
                case ReactionMoment.LevelUp: return new[] { "A level. Sure.", "Mm. Stronger, I guess.", "Okay." };
                case ReactionMoment.GachaTop: return new[] { "Huh. Gold. Fine.", "Okay. That one's good.", "Lucky. I'll allow it." };
                case ReactionMoment.GachaNoTop: return new[] { "Of course. Nothing.", "That's the gacha. Ugh.", "Money well spent. Not." };
                default: return null;
            }
        }

        static string[] Cool(ReactionMoment m)
        {
            switch (m)
            {
                case ReactionMoment.MatchStart: return new[] { "Alright. Let's see how this goes.", "Good luck.", "New match. Stay sharp." };
                case ReactionMoment.Win: return new[] { "Nice. Clean win.", "Well played.", "That's a win. Good." };
                case ReactionMoment.Loss: return new[] { "That one got away.", "Happens. Next.", "Close, but no." };
                case ReactionMoment.Death: return new[] { "Ouch.", "That didn't go well.", "Unlucky." };
                case ReactionMoment.Kill: return new[] { "Nice.", "Clean.", "Good shot." };
                case ReactionMoment.BigPlay: return new[] { "Oh. That was impressive.", "Now that was something.", "Wow. Okay." };
                case ReactionMoment.ObjectiveTaken: return new[] { "Good call.", "Objective secured.", "Nice timing." };
                case ReactionMoment.ObjectiveLost: return new[] { "Lost that one.", "Hm. That stings.", "Moving on." };
                case ReactionMoment.Tense: return new[] { "Steady.", "Careful now.", "Breathe." };
                case ReactionMoment.LevelUp: return new[] { "Level up.", "Stronger now.", "Nice, a new level." };
                case ReactionMoment.GachaTop: return new[] { "Gold. Nice pull.", "There it is.", "Good luck today." };
                case ReactionMoment.GachaNoTop: return new[] { "No gold this time.", "Pity's building.", "Next time." };
                default: return null;
            }
        }
    }
}
