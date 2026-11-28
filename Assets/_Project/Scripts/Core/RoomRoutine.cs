using System;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-231: what she does in her room, replacing the random stroll of <see cref="RoomWander"/>.
    /// Now and then she picks an activity (by the time of day, her mood and what she just did),
    /// walks to its slot, does it for a while and gets up. At night she sleeps in her bed until
    /// morning. The player can ask her to do something. Plain logic: the view ticks it every
    /// frame and walks her when it hands back a tile.
    /// 18+ stays out of the room: the Intimate mood counts as Content here.
    /// </summary>
    public sealed class RoomRoutine
    {
        public const int ActivityCount = 8;

        /// <summary>How much less likely she is to do the same thing twice in a row.</summary>
        public const float RepeatFactor = 0.2f;

        readonly RoomLayout _layout;
        readonly RoomSlots _slots;
        readonly Random _random;
        readonly float[] _weights = new float[ActivityCount];

        RoomActivity _heading;
        RoomActivity _doing;
        RoomActivity _last;
        float _until;
        float _next;

        public RoomRoutine(RoomLayout layout, RoomSlots slots, int seed, float now)
        {
            _layout = layout;
            _slots = slots;
            _random = new Random(seed);
            _next = now + Pause();
        }

        /// <summary>The activity she is doing now (the view draws its pose). None while walking or standing.</summary>
        public RoomActivity Doing => _doing;

        /// <summary>The activity she is walking to, or None.</summary>
        public RoomActivity Heading => _heading;

        public RoomSlots Slots => _slots;

        /// <summary>
        /// Called every frame. Returns true with a tile when she should start walking. Pass where
        /// the player is and what he is doing, so she leaves his spot alone.
        /// </summary>
        public bool TryNext(float now, RoomTile her, bool walking, RoomTile player, RoomActivity playerDoing,
            float hour, bool night, CompanionMood mood, out RoomTile target)
        {
            target = her;
            RoomSlot slot;

            // Bedtime wins over everything: she goes to bed and stays until morning.
            if (night)
            {
                if (_doing == RoomActivity.Sleep || _heading == RoomActivity.Sleep)
                    return Arrive(now, her, walking);
                if (!_slots.TryGet(RoomActivity.Sleep, out slot))
                    return false;
                _doing = RoomActivity.None;
                _heading = RoomActivity.Sleep;
                target = slot.Stand;
                return true;
            }

            if (_doing == RoomActivity.Sleep)
            {
                // Morning: she gets up.
                Finish(now);
                return false;
            }

            if (_heading != RoomActivity.None)
                return Arrive(now, her, walking);

            if (_doing != RoomActivity.None)
            {
                if (now >= _until)
                    Finish(now);
                return false;
            }

            if (walking || now < _next)
                return false;

            // With the player beside her she stays and talks instead of wandering off.
            if (RoomWander.Close(her, player))
            {
                _next = now + 2f;
                return false;
            }

            Weights(hour, mood, _last, playerDoing, _weights);
            var pick = Pick(_weights, _random.NextDouble());
            if (pick != RoomActivity.None && _slots.TryGet(pick, out slot) && !Taken(slot, player))
            {
                _heading = pick;
                target = slot.Stand;
                return true;
            }

            // Otherwise a stroll to another free tile.
            _next = now + Pause();
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var tile = new RoomTile(_random.Next(_layout.Columns), _random.Next(_layout.Rows));
                if (_layout.IsFree(tile) && tile != her && tile != player)
                {
                    target = tile;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The player asks her to do something. She drops what she is doing and walks there.
        /// Refused at night (she stays in bed), for an activity this room has no slot for, or
        /// when the player is using that spot.
        /// </summary>
        public bool Request(RoomActivity activity, RoomTile player, bool night, out RoomTile target)
        {
            target = default(RoomTile);
            RoomSlot slot;
            if (night || activity == RoomActivity.None || activity == RoomActivity.Sleep
                || !_slots.TryGet(activity, out slot) || Taken(slot, player))
                return false;
            _doing = RoomActivity.None;
            _heading = activity;
            target = slot.Stand;
            return true;
        }

        /// <summary>She stops what she is doing (the player walked into her, the room closed).</summary>
        public void Stop(float now)
        {
            if (_doing != RoomActivity.None || _heading != RoomActivity.None)
                Finish(now);
        }

        bool Arrive(float now, RoomTile her, bool walking)
        {
            if (walking)
                return false;
            RoomSlot slot;
            if (!_slots.TryGet(_heading, out slot) && !_slots.TryGet(_doing, out slot))
            {
                _heading = RoomActivity.None;
                return false;
            }

            if (_doing == RoomActivity.None)
            {
                if (her != slot.Stand)
                {
                    // The walk ended short (the spot was taken or cut off): give up on it.
                    _heading = RoomActivity.None;
                    _next = now + Pause();
                    return false;
                }

                _doing = _heading;
                _heading = RoomActivity.None;
                _until = now + Duration(slot);
            }

            return false;
        }

        void Finish(float now)
        {
            if (_doing != RoomActivity.None)
                _last = _doing;
            _doing = RoomActivity.None;
            _heading = RoomActivity.None;
            _next = now + Pause();
        }

        float Pause()
        {
            return RoomWander.MinPause + (float)_random.NextDouble() * (RoomWander.MaxPause - RoomWander.MinPause);
        }

        float Duration(RoomSlot slot)
        {
            if (slot.MaxSeconds <= 0f)
                return float.MaxValue;
            return slot.MinSeconds + (float)_random.NextDouble() * (slot.MaxSeconds - slot.MinSeconds);
        }

        static bool Taken(RoomSlot slot, RoomTile player)
        {
            return player == slot.Stand;
        }

        /// <summary>
        /// How likely each activity is now (index = <see cref="RoomActivity"/>; None is a stroll).
        /// Sleep is never picked here: bedtime handles it.
        /// </summary>
        public static void Weights(float hour, CompanionMood mood, RoomActivity last, RoomActivity playerDoing, float[] weights)
        {
            for (var i = 0; i < weights.Length; i++)
                weights[i] = 0f;

            if (hour >= 6f && hour < 10f)
            {
                Set(weights, stroll: 2f, sit: 1f, read: 1f, pc: 1f, window: 3f, wardrobe: 2f, hobby: 1f);
            }
            else if (hour >= 10f && hour < 17f)
            {
                Set(weights, stroll: 2f, sit: 1f, read: 2f, pc: 3f, window: 1f, wardrobe: 0.5f, hobby: 3f);
            }
            else
            {
                Set(weights, stroll: 1f, sit: 2f, read: 3f, pc: 2f, window: 1.5f, wardrobe: 1f, hobby: 1.5f);
            }

            switch (mood)
            {
                case CompanionMood.Happy:
                    Scale(weights, RoomActivity.Hobby, 2f);
                    Scale(weights, RoomActivity.None, 1.5f);
                    break;
                case CompanionMood.Affectionate:
                    Scale(weights, RoomActivity.Sit, 2f);
                    break;
                case CompanionMood.Flustered:
                    Scale(weights, RoomActivity.LookWindow, 2f);
                    Scale(weights, RoomActivity.Wardrobe, 1.5f);
                    break;
                case CompanionMood.Annoyed:
                    Scale(weights, RoomActivity.UsePc, 2f);
                    Scale(weights, RoomActivity.Hobby, 1.5f);
                    Scale(weights, RoomActivity.Sit, 0.5f);
                    break;
                case CompanionMood.Sad:
                    Scale(weights, RoomActivity.LookWindow, 2.5f);
                    Scale(weights, RoomActivity.Sit, 1.5f);
                    Scale(weights, RoomActivity.Hobby, 0.5f);
                    break;
                case CompanionMood.Anxious:
                    Scale(weights, RoomActivity.None, 2f);
                    Scale(weights, RoomActivity.Read, 1.5f);
                    break;
                case CompanionMood.Sleepy:
                    Scale(weights, RoomActivity.Sit, 2f);
                    Scale(weights, RoomActivity.Read, 1.5f);
                    Scale(weights, RoomActivity.Hobby, 0.3f);
                    Scale(weights, RoomActivity.None, 0.5f);
                    break;
                // Content, and Intimate: 18+ stays out of the room.
            }

            if (last != RoomActivity.None)
                Scale(weights, last, RepeatFactor);
            if (playerDoing != RoomActivity.None)
                weights[(int)playerDoing] = 0f;
            weights[(int)RoomActivity.Sleep] = 0f;
        }

        static void Set(float[] w, float stroll, float sit, float read, float pc, float window, float wardrobe, float hobby)
        {
            w[(int)RoomActivity.None] = stroll;
            w[(int)RoomActivity.Sit] = sit;
            w[(int)RoomActivity.Read] = read;
            w[(int)RoomActivity.UsePc] = pc;
            w[(int)RoomActivity.LookWindow] = window;
            w[(int)RoomActivity.Wardrobe] = wardrobe;
            w[(int)RoomActivity.Hobby] = hobby;
        }

        static void Scale(float[] w, RoomActivity activity, float factor)
        {
            w[(int)activity] *= factor;
        }

        /// <summary>A weighted pick: <paramref name="roll"/> from 0 to 1 lands in one activity's share.</summary>
        public static RoomActivity Pick(float[] weights, double roll)
        {
            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
                total += Math.Max(0f, weights[i]);
            if (total <= 0f)
                return RoomActivity.None;
            var at = (float)(roll * total);
            for (var i = 0; i < weights.Length; i++)
            {
                var w = Math.Max(0f, weights[i]);
                if (at < w)
                    return (RoomActivity)i;
                at -= w;
            }

            for (var i = weights.Length - 1; i >= 0; i--)
            {
                if (weights[i] > 0f)
                    return (RoomActivity)i;
            }

            return RoomActivity.None;
        }
    }
}
