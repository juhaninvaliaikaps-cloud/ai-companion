using AICompanion.Core;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-228: what she does in her room before activities exist.</summary>
    public sealed class RoomWanderTests
    {
        static readonly RoomTile Far = new RoomTile(11, 8);

        [Test]
        public void SheWaits_ThenStrollsToAFreeTile()
        {
            var layout = RoomLayout.V1();
            var wander = new RoomWander(layout, 7, 0f);
            var her = layout.HerEntry;
            RoomTile target;
            Assert.IsFalse(wander.TryNext(1f, her, false, Far, false, out target), "not before the first pause");
            Assert.IsTrue(wander.TryNext(RoomWander.MinPause, her, false, Far, false, out target));
            Assert.IsTrue(layout.IsFree(target));
            Assert.AreNotEqual(her, target);
            Assert.IsFalse(wander.TryNext(RoomWander.MinPause + 0.1f, target, false, Far, false, out target), "then she pauses again");
        }

        [Test]
        public void NotWhileWalking()
        {
            var layout = RoomLayout.V1();
            var wander = new RoomWander(layout, 1, 0f);
            RoomTile target;
            Assert.IsFalse(wander.TryNext(100f, layout.HerEntry, true, Far, false, out target));
        }

        [Test]
        public void SheStays_WhileThePlayerIsClose()
        {
            var layout = RoomLayout.V1();
            var wander = new RoomWander(layout, 3, 0f);
            var her = new RoomTile(5, 5);
            RoomTile target;
            Assert.IsFalse(wander.TryNext(100f, her, false, new RoomTile(6, 6), false, out target));
            Assert.IsFalse(wander.TryNext(100f + RoomWander.MinPause - 0.5f, her, false, Far, false, out target),
                "after he leaves, she waits a moment before strolling off");
            Assert.IsTrue(wander.TryNext(100f + RoomWander.MinPause, her, false, Far, false, out target));
        }

        [Test]
        public void AtNight_SheGoesToBed_AndStays()
        {
            var layout = RoomLayout.V1();
            var wander = new RoomWander(layout, 5, 0f);
            RoomTile target;
            Assert.IsTrue(wander.TryNext(1f, layout.HerEntry, false, Far, true, out target));
            Assert.AreEqual(RoomWander.BedSide, target);
            Assert.IsTrue(layout.IsFree(RoomWander.BedSide));
            Assert.IsFalse(wander.TryNext(500f, RoomWander.BedSide, false, Far, true, out target), "she stays by the bed");
        }

        [Test]
        public void Close_IsTwoSteps()
        {
            Assert.IsTrue(RoomWander.Close(new RoomTile(5, 5), new RoomTile(6, 6)));
            Assert.IsFalse(RoomWander.Close(new RoomTile(5, 5), new RoomTile(7, 6)));
        }
    }
}
