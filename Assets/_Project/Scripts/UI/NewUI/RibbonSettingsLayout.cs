using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Positions Ribbon Settings navigation and content in the visible overlay area.</summary>
    public readonly struct RibbonSettingsLayout
    {
        public readonly Rect Frame;
        public readonly Rect Close;
        public readonly Rect Title;
        public readonly Rect Body;
        public readonly Rect Status;
        public readonly Rect Pages;
        public readonly int PageColumns;
        public readonly float Scale;
        public readonly bool Sidebar;
        readonly float _pad;
        readonly float _gap;
        readonly float _navTop;
        readonly float _tabHeight;
        readonly float _navWidth;
        readonly int _columns;

        public RibbonSettingsLayout(Rect visibleArea, float dpiScale)
        {
            Scale = Mathf.Max(.1f, Mathf.Min(dpiScale, visibleArea.width / 350f, visibleArea.height / 360f));
            var s = Scale;
            var margin = 8f * s;
            var width = Mathf.Max(1f, Mathf.Min(790f * s, visibleArea.width - 2f * margin));
            var height = Mathf.Max(1f, Mathf.Min(680f * s, visibleArea.height - 2f * margin));
            Frame = new Rect(visibleArea.center - new Vector2(width, height) * .5f, new Vector2(width, height));
            _pad = 16f * s;
            _gap = 5f * s;
            _tabHeight = 28f * s;
            _navTop = _pad + (height < 430f * s ? 38f : 43f) * s;
            Sidebar = width >= 660f * s;
            _columns = Sidebar ? 1 : width >= 532f * s ? 8 : 4;
            _navWidth = Sidebar ? 120f * s : width - 2f * _pad;
            var bodyX = Sidebar ? _pad + _navWidth + 12f * s : _pad;
            var bodyW = width - bodyX - _pad;
            var pagesY = Sidebar
                ? _navTop
                : _navTop + (SettingsMenu.TabCount / _columns) * (_tabHeight + _gap);
            PageColumns = bodyW >= 495f * s ? SettingsMenu.MaxPages : 3;
            var pageRows = Mathf.CeilToInt(SettingsMenu.MaxPages / (float)PageColumns);
            var pageHeight = 26f * s;
            Pages = new Rect(bodyX, pagesY, bodyW, pageHeight);
            var bodyY = pagesY + pageRows * (pageHeight + _gap) + 4f * s;
            var statusHeight = 20f * s;
            Status = new Rect(bodyX, height - _pad - statusHeight, bodyW, statusHeight);
            Body = new Rect(bodyX, bodyY, bodyW, Mathf.Max(1f, Status.y - bodyY - _gap));
            Close = new Rect(width - _pad - _tabHeight, _pad, _tabHeight, _tabHeight);
            Title = new Rect(_pad + 13f * s, _pad - 2f * s, Close.x - _pad - 23f * s, 34f * s);
        }

        public Rect Tab(int i)
        {
            var width = (_navWidth - (_columns - 1) * _gap) / _columns;
            return new Rect(_pad + i % _columns * (width + _gap),
                _navTop + i / _columns * (_tabHeight + _gap), width, _tabHeight);
        }
    }
}
