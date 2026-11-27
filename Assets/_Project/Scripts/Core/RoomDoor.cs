using UnityEngine;

namespace AICompanion.Core
{
    public enum RoomDoorState
    {
        Closed = 0,
        Opening = 1,
        Open = 2,
        Closing = 3,
    }

    /// <summary>What a door tick finished, for the view to act on (show the room, hide her desktop avatar).</summary>
    public enum RoomDoorEvent
    {
        None = 0,
        Opened = 1,
        Closed = 2,
    }

    /// <summary>
    /// JUH-226: the door from the desktop into her room. It opens fast (Jussi: about a second;
    /// the opening also covers loading the room) and a close while opening turns it around
    /// from where it is. <see cref="Progress"/> is 0 shut to 1 open.
    /// </summary>
    public sealed class RoomDoor
    {
        public const float DefaultSeconds = 1.2f;

        /// <summary>Float sums of frame times never land exactly on 0 or 1.</summary>
        const float Epsilon = 0.0001f;

        public RoomDoorState State { get; private set; }
        public float Progress { get; private set; }
        public float Seconds { get; set; }

        public RoomDoor(float seconds = DefaultSeconds)
        {
            Seconds = seconds > 0f ? seconds : DefaultSeconds;
        }

        public bool IsOpen => State == RoomDoorState.Open;
        public bool IsClosed => State == RoomDoorState.Closed;
        public bool Moving => State == RoomDoorState.Opening || State == RoomDoorState.Closing;

        /// <summary>Starts opening. False when it is already open or opening.</summary>
        public bool Open()
        {
            if (State == RoomDoorState.Open || State == RoomDoorState.Opening)
                return false;
            State = RoomDoorState.Opening;
            return true;
        }

        /// <summary>Starts closing. False when it is already shut or closing.</summary>
        public bool Close()
        {
            if (State == RoomDoorState.Closed || State == RoomDoorState.Closing)
                return false;
            State = RoomDoorState.Closing;
            return true;
        }

        public RoomDoorEvent Tick(float deltaSeconds)
        {
            var step = Mathf.Max(0f, deltaSeconds) / Seconds;
            if (State == RoomDoorState.Opening)
            {
                Progress = Mathf.Min(1f, Progress + step);
                if (Progress >= 1f - Epsilon)
                {
                    Progress = 1f;
                    State = RoomDoorState.Open;
                    return RoomDoorEvent.Opened;
                }
            }
            else if (State == RoomDoorState.Closing)
            {
                Progress = Mathf.Max(0f, Progress - step);
                if (Progress <= Epsilon)
                {
                    Progress = 0f;
                    State = RoomDoorState.Closed;
                    return RoomDoorEvent.Closed;
                }
            }

            return RoomDoorEvent.None;
        }

        /// <summary>The door panel's swing in degrees (0 shut, 100 open), eased so it starts quick and settles.</summary>
        public float Angle => 100f * (1f - (1f - Progress) * (1f - Progress));
    }
}
