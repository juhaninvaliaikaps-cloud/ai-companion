namespace AICompanion.NewUI
{
    /// <summary>Keyboard position in the Ribbon's main list or the open flyout.</summary>
    public sealed class RibbonKeyboard
    {
        public int Row { get; private set; } = -1;
        public int Destination { get; private set; } = -1;
        public bool Active => Row >= 0;

        public void Open()
        {
            Row = 0;
            Destination = -1;
        }

        public void Close()
        {
            Row = -1;
            Destination = -1;
        }

        public void Move(int direction, int rowCount, int destinationCount, bool canLook)
        {
            if (Destination >= 0 && destinationCount > 0)
            {
                Destination = Wrap(Destination + direction, destinationCount);
                return;
            }

            Row = Wrap(Row + direction, rowCount);
            // The Look now row is disabled until screen look is available.
            if (Row == 1 && !canLook)
                Row = Wrap(Row + direction, rowCount);
        }

        public void EnterFlyout()
        {
            Destination = 0;
        }

        public void LeaveFlyout()
        {
            Destination = -1;
        }

        static int Wrap(int index, int count) => (index + count) % count;
    }
}
