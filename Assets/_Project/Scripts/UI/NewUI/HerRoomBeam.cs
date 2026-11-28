using AICompanion.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-229: the sunbeam from her window, a soft four-cornered patch of light on the floor that
    /// fades with distance. Stretched over the whole room; the corners come from
    /// <see cref="RoomLighting.Beam"/> (room fractions, y down). The mesh is rebuilt only when
    /// the hour moves on, not every frame.
    /// </summary>
    public sealed class HerRoomBeam : MaskableGraphic
    {
        static readonly Color Warm = new Color(1f, 0.95f, 0.8f, 1f);

        Vector2 _nearLeft;
        Vector2 _nearRight;
        Vector2 _farRight;
        Vector2 _farLeft;
        float _strength;
        float _hour = -1f;

        /// <summary>Moves the beam for the hour; rebuilds only when it changed enough to see.</summary>
        public void Set(float hour)
        {
            var strength = RoomLighting.Sun(hour);
            if (Mathf.Abs(hour - _hour) < 0.02f && Mathf.Abs(strength - _strength) < 0.01f)
                return;
            _hour = hour;
            _strength = strength;
            RoomLighting.Beam(hour, out _nearLeft, out _nearRight, out _farRight, out _farLeft);
            enabled = strength > 0.001f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_strength <= 0.001f)
                return;
            var near = Warm;
            near.a = RoomLighting.BeamAlpha * _strength;
            var far = Warm;
            far.a = 0f;
            vh.AddVert(Point(_nearLeft), near, Vector2.zero);
            vh.AddVert(Point(_nearRight), near, Vector2.zero);
            vh.AddVert(Point(_farRight), far, Vector2.zero);
            vh.AddVert(Point(_farLeft), far, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }

        Vector3 Point(Vector2 fraction)
        {
            var r = rectTransform.rect;
            return new Vector3(r.xMin + fraction.x * r.width, r.yMax - fraction.y * r.height, 0f);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }
}
