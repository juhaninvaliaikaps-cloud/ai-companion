using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Positions the Ribbon chat options under the header in top-left chatbox coordinates.</summary>
    public readonly struct RibbonChatOptionsLayout
    {
        public const int OptionCount = 5;
        public readonly Rect Bounds;
        public readonly int Columns;
        readonly float _gap;
        readonly float _rowHeight;

        public RibbonChatOptionsLayout(Vector2 box, float headerBottom, float gearRight, float scale)
        {
            var margin = 9f * scale;
            var top = headerBottom + 5f * scale;
            _gap = 5f * scale;
            var preferredRowHeight = 28f * scale;
            var available = box.y - top - margin;
            Columns = available >= OptionCount * preferredRowHeight + (OptionCount + 1) * _gap ? 1 : 2;
            var rows = (OptionCount + Columns - 1) / Columns;
            var minimumHeight = rows * 19f * scale + (rows + 1) * _gap;
            // Very short work areas can compress the box below its normal resize limit.
            top = Mathf.Max(margin, Mathf.Min(top, box.y - margin - minimumHeight));
            available = box.y - top - margin;
            _rowHeight = Mathf.Clamp((available - (rows + 1) * _gap) / rows, 1f, preferredRowHeight);
            var width = Mathf.Min((Columns == 1 ? 195f : 278f) * scale, Mathf.Max(1f, box.x - 2f * margin));
            var x = Mathf.Clamp(gearRight - width, margin, Mathf.Max(margin, box.x - width - margin));
            Bounds = new Rect(x, top, width, rows * _rowHeight + (rows + 1) * _gap);
        }

        public Rect Option(int index)
        {
            var width = (Bounds.width - (Columns + 1) * _gap) / Columns;
            return new Rect(_gap + index % Columns * (width + _gap),
                _gap + index / Columns * (_rowHeight + _gap), width, _rowHeight);
        }

        public int Neighbor(int index, int horizontal, int vertical)
        {
            var column = index % Columns + horizontal;
            var row = index / Columns + vertical;
            if (column < 0 || column >= Columns || row < 0)
                return -1;
            var target = row * Columns + column;
            if (target >= OptionCount && vertical != 0 && column > 0)
                target -= column;
            return target < OptionCount ? target : -1;
        }
    }
}
