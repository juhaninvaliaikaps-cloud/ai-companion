using System;

namespace AICompanion.Core
{
    /// <summary>JUH-231: the hobbies her room's hobby slot can hold (JUH-175: a fixed list).</summary>
    public enum RoomHobby
    {
        Piano = 0,
        SquatRack = 1,
        Easel = 2,
        Guitar = 3,
        Plants = 4,
        Telescope = 5,
    }

    /// <summary>
    /// JUH-231: her room hobby. Pisara's reading names it (<see cref="CharacterSheet.roomHobby"/>);
    /// an older sheet, or a reading without it, falls back to a guess from her description, and
    /// then to the piano.
    /// </summary>
    public static class RoomHobbies
    {
        /// <summary>The keys in the reading, in enum order.</summary>
        public static readonly string[] Keys = { "piano", "squat_rack", "easel", "guitar", "plants", "telescope" };

        static readonly string[] Pieces =
        {
            "room_hobby_piano", "room_hobby_squat_rack", "room_hobby_easel",
            "room_hobby_guitar_stand", "room_hobby_plant_table", "room_hobby_telescope",
        };

        // Words in her description that point at a hobby. A word ending in * matches any word
        // starting with it; the others must match a whole word ("star" is not "start").
        static readonly string[][] Hints =
        {
            new[] { "piano", "pianist", "keyboard", "classical" },
            new[] { "gym", "workout*", "weightlift*", "lifting", "fitness", "athlet*", "squat*", "muscle*", "crossfit", "bodybuild*", "powerlift*" },
            new[] { "paint*", "artist*", "drawing*", "draws", "sketch*", "illustrat*", "canvas*", "art" },
            new[] { "guitar*", "band", "musician*", "singer*", "songwrit*", "rock" },
            new[] { "plant*", "garden*", "flower*", "botan*", "succulent*" },
            new[] { "star", "stars", "stargaz*", "astronom*", "telescope*", "cosmos", "galax*", "constellation*", "planets", "space" },
        };

        /// <summary>The key for a reading's value, or empty when it is not one of the list.</summary>
        public static string Normalize(string value)
        {
            RoomHobby hobby;
            return TryParse(value, out hobby) ? Keys[(int)hobby] : string.Empty;
        }

        public static bool TryParse(string value, out RoomHobby hobby)
        {
            hobby = RoomHobby.Piano;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var key = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (key == "squat" || key == "rack" || key == "weights")
                key = "squat_rack";
            else if (key == "plant" || key == "plant_table")
                key = "plants";
            else if (key == "guitar_stand")
                key = "guitar";
            for (var i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] == key)
                {
                    hobby = (RoomHobby)i;
                    return true;
                }
            }

            return false;
        }

        public static string Key(RoomHobby hobby)
        {
            return Keys[Index(hobby)];
        }

        /// <summary>The catalogue id of the piece that stands in the hobby slot.</summary>
        public static string PieceId(RoomHobby hobby)
        {
            return Pieces[Index(hobby)];
        }

        /// <summary>Her pose at the hobby (the art pack's activity name), or empty: she stands and faces it.</summary>
        public static string Pose(RoomHobby hobby)
        {
            switch (hobby)
            {
                case RoomHobby.Piano:
                    return "hobby_piano";
                case RoomHobby.SquatRack:
                    return "hobby_squat_rack";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Her hobby: the reading's when it has one, else a guess from her description.</summary>
        public static RoomHobby For(CharacterSheet sheet, string personaPrompt)
        {
            RoomHobby hobby;
            if (sheet != null && TryParse(sheet.roomHobby, out hobby))
                return hobby;
            return Guess(personaPrompt);
        }

        /// <summary>The hobby her description hints at most; the piano when it says nothing.</summary>
        public static RoomHobby Guess(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return RoomHobby.Piano;
            var words = description.ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var best = RoomHobby.Piano;
            var bestScore = 0;
            for (var h = 0; h < Hints.Length; h++)
            {
                var score = 0;
                foreach (var word in words)
                {
                    foreach (var hint in Hints[h])
                    {
                        if (Matches(word, hint))
                        {
                            score++;
                            break;
                        }
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (RoomHobby)h;
                }
            }

            return best;
        }

        static readonly char[] Separators =
        {
            ' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\'', '/', '-', '_', '*', '#',
        };

        static bool Matches(string word, string hint)
        {
            if (hint.EndsWith("*", StringComparison.Ordinal))
                return word.StartsWith(hint.Substring(0, hint.Length - 1), StringComparison.Ordinal);
            return word == hint;
        }

        static int Index(RoomHobby hobby)
        {
            var i = (int)hobby;
            return i >= 0 && i < Keys.Length ? i : 0;
        }
    }
}
