using System;
using AICompanion.Core;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// JUH-189: a CS2, Dota or League event may name the game the companion is watching,
    /// but it must not replace an open gacha title. Replacing it would wipe that title's
    /// primer and observations, and the next gacha scan would wipe the feed game in turn.
    /// Feed games may still replace each other when no gacha window is open.
    /// </summary>
    public static class GameContextOwnership
    {
        public static void NoteFeed(GameTitle feed, GameTitle openGacha)
        {
            if (!FeedMayName(feed, openGacha))
                return;
            GameContextNow.SetGame(CommentaryPrompt.GameName(feed));
        }

        public static bool FeedMayName(GameTitle feed, GameTitle openGacha)
        {
            if (feed == GameTitle.Unknown || GachaGames.IsGacha(feed))
                return false;
            if (openGacha != GameTitle.Unknown && openGacha != feed)
                return false;

            var current = GameContextNow.Game;
            if (string.IsNullOrEmpty(current))
                return true;
            if (string.Equals(current, CommentaryPrompt.GameName(feed), StringComparison.Ordinal))
                return true;
            return !IsGachaName(current);
        }

        static bool IsGachaName(string name)
        {
            var titles = GachaGames.All;
            for (var i = 0; i < titles.Length; i++)
            {
                if (string.Equals(name, CommentaryPrompt.GameName(titles[i]), StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
