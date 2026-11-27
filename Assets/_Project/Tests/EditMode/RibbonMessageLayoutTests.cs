using AICompanion.NewUI;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    public sealed class RibbonMessageLayoutTests
    {
        [Test]
        public void WideRowsLeaveRoomForTheLargerHappiPortraitAndOffsetHerReply()
        {
            var layout = new RibbonMessageLayout(530f, 1f);
            var user = layout.Card(false, true, 0f, 18f);
            var reply = layout.Card(true, true, user.yMax + layout.Gap, 45f);
            var speaker = layout.Badge(true, reply);

            Assert.That(layout.ShowSpeakers, Is.True);
            Assert.That(reply.xMin, Is.GreaterThan(user.xMin));
            Assert.That(reply.xMax, Is.EqualTo(user.xMax));
            Assert.That(speaker.xMax, Is.LessThan(reply.xMin));
            Assert.That(speaker.width, Is.GreaterThan(layout.Badge(false, user).width));
            Assert.That(layout.TextWidth(true, true), Is.EqualTo(reply.width - 2f * layout.TextPadding(true)));
        }

        [Test]
        public void LongMessagesKeepThePortraitNearTheStart()
        {
            var layout = new RibbonMessageLayout(500f, 1f);
            var row = layout.Card(true, true, 20f, 200f);
            var badge = layout.Badge(true, row);

            Assert.That(badge.yMin - row.yMin, Is.EqualTo(6f));
            Assert.That(badge.yMax, Is.LessThan(row.center.y));
        }

        [Test]
        public void CompactMessagesAndSystemNotesUseTheUnoccupiedGutter()
        {
            var compact = new RibbonMessageLayout(145f, 1f);
            var wide = new RibbonMessageLayout(500f, 1f);

            Assert.That(compact.ShowSpeakers, Is.False);
            Assert.That(compact.Card(true, false, 0f, 32f).xMin, Is.EqualTo(2f));
            Assert.That(wide.TextWidth(false, false), Is.GreaterThan(wide.TextWidth(false, true)));
        }
    }
}
