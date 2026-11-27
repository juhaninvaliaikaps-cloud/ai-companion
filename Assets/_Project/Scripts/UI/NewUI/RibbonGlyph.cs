using AICompanion.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>Decorative marks for the Ribbon UI. The surrounding control owns the hit area.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class RibbonGlyph : MaskableGraphic
    {
        internal enum Mark
        {
            Menu, Door, Ink, Voice, Muted, Screen, Profile, Model, Settings, Close, Chevron, Lock, OpenLock,
            Minimize, ChevronUp, Check, ChevronDown, Warmth, Volatility, Assertiveness, Playfulness, Openness,
            Energy, Directness, Confidence, Curiosity
        }

        internal static Mark TemperamentMark(int axis) => axis switch
        {
            TemperamentAxes.Warmth => Mark.Warmth,
            TemperamentAxes.Volatility => Mark.Volatility,
            TemperamentAxes.Assertiveness => Mark.Assertiveness,
            TemperamentAxes.Playfulness => Mark.Playfulness,
            TemperamentAxes.Openness => Mark.Openness,
            TemperamentAxes.Energy => Mark.Energy,
            TemperamentAxes.Directness => Mark.Directness,
            TemperamentAxes.Confidence => Mark.Confidence,
            _ => Mark.Curiosity
        };
        internal TMP_Text Label { get; set; }
        internal Mark Shape
        {
            get => _mark;
            set
            {
                if (_mark == value)
                    return;
                _mark = value;
                SetVerticesDirty();
            }
        }
        Mark _mark;

        public static RibbonGlyph Add(Transform parent, string name, Mark mark, Color color)
        {
            var glyph = HappiUiKit.Rect(parent, name).gameObject.AddComponent<RibbonGlyph>();
            glyph.Shape = mark;
            glyph.color = color;
            glyph.raycastTarget = false;
            glyph.SetVerticesDirty();
            return glyph;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (_mark)
            {
                case Mark.Menu:
                    Line(mesh, 0.17f, 0.25f, 0.71f, 0.25f);
                    Line(mesh, 0.17f, 0.50f, 0.83f, 0.50f);
                    Line(mesh, 0.17f, 0.75f, 0.83f, 0.75f);
                    break;
                case Mark.Door:
                    Line(mesh, 0.18f, 0.12f, 0.18f, 0.89f);
                    Line(mesh, 0.18f, 0.89f, 0.63f, 0.89f);
                    Line(mesh, 0.18f, 0.12f, 0.78f, 0.12f);
                    Line(mesh, 0.63f, 0.89f, 0.83f, 0.73f);
                    Line(mesh, 0.83f, 0.73f, 0.83f, 0.21f);
                    Line(mesh, 0.83f, 0.21f, 0.63f, 0.12f);
                    Line(mesh, 0.63f, 0.12f, 0.63f, 0.89f);
                    Line(mesh, 0.72f, 0.49f, 0.76f, 0.49f);
                    break;
                case Mark.Voice:
                case Mark.Muted:
                    Speaker(mesh, _mark == Mark.Muted);
                    break;
                case Mark.Screen:
                    Box(mesh, 0.12f, 0.29f, 0.88f, 0.85f);
                    Line(mesh, 0.50f, 0.29f, 0.50f, 0.14f);
                    Line(mesh, 0.32f, 0.14f, 0.68f, 0.14f);
                    break;
                case Mark.Profile:
                    Ring(mesh, new Vector2(0.50f, 0.70f), 0.16f);
                    Line(mesh, 0.15f, 0.16f, 0.21f, 0.33f);
                    Line(mesh, 0.21f, 0.33f, 0.37f, 0.41f);
                    Line(mesh, 0.37f, 0.41f, 0.63f, 0.41f);
                    Line(mesh, 0.63f, 0.41f, 0.79f, 0.33f);
                    Line(mesh, 0.79f, 0.33f, 0.85f, 0.16f);
                    Line(mesh, 0.15f, 0.16f, 0.85f, 0.16f);
                    break;
                case Mark.Model:
                    Box(mesh, 0.24f, 0.24f, 0.76f, 0.76f);
                    Box(mesh, 0.40f, 0.40f, 0.60f, 0.60f);
                    for (var i = 0; i < 3; i++)
                    {
                        var pin = 0.34f + i * 0.16f;
                        Line(mesh, pin, 0.76f, pin, 0.91f);
                        Line(mesh, pin, 0.09f, pin, 0.24f);
                        Line(mesh, 0.09f, pin, 0.24f, pin);
                        Line(mesh, 0.76f, pin, 0.91f, pin);
                    }
                    break;
                case Mark.Settings:
                    SettingsCog(mesh);
                    break;
                case Mark.Close:
                    Line(mesh, 0.25f, 0.25f, 0.75f, 0.75f);
                    Line(mesh, 0.25f, 0.75f, 0.75f, 0.25f);
                    break;
                case Mark.Chevron:
                    Line(mesh, 0.33f, 0.17f, 0.67f, 0.50f);
                    Line(mesh, 0.67f, 0.50f, 0.33f, 0.83f);
                    break;
                case Mark.ChevronUp:
                    Line(mesh, 0.18f, 0.35f, 0.50f, 0.67f);
                    Line(mesh, 0.50f, 0.67f, 0.82f, 0.35f);
                    break;
                case Mark.ChevronDown:
                    Line(mesh, 0.18f, 0.67f, 0.50f, 0.35f);
                    Line(mesh, 0.50f, 0.35f, 0.82f, 0.67f);
                    break;
                case Mark.Check:
                    Line(mesh, 0.16f, 0.46f, 0.40f, 0.22f);
                    Line(mesh, 0.40f, 0.22f, 0.84f, 0.78f);
                    break;
                case Mark.Lock:
                case Mark.OpenLock:
                    Box(mesh, 0.21f, 0.15f, 0.79f, 0.57f);
                    Line(mesh, 0.50f, 0.30f, 0.50f, 0.41f);
                    Line(mesh, 0.33f, 0.57f, 0.33f, 0.75f);
                    Line(mesh, 0.33f, 0.75f, 0.43f, 0.87f);
                    Line(mesh, 0.43f, 0.87f, 0.61f, 0.87f);
                    Line(mesh, 0.61f, 0.87f, 0.69f, 0.75f);
                    Line(mesh, 0.69f, 0.75f, 0.69f, _mark == Mark.Lock ? 0.57f : 0.68f);
                    break;
                case Mark.Minimize:
                    Line(mesh, 0.19f, 0.36f, 0.81f, 0.36f);
                    break;
                case Mark.Warmth:
                    Ring(mesh, new Vector2(0.5f, 0.5f), 0.20f);
                    TraitPath(mesh, 9, 1, 9, 3); TraitPath(mesh, 9, 15, 9, 17);
                    TraitPath(mesh, 1, 9, 3, 9); TraitPath(mesh, 15, 9, 17, 9);
                    TraitPath(mesh, 3, 3, 4.5f, 4.5f); TraitPath(mesh, 13.5f, 13.5f, 15, 15);
                    TraitPath(mesh, 13.5f, 4.5f, 15, 3); TraitPath(mesh, 3, 15, 4.5f, 13.5f);
                    break;
                case Mark.Volatility:
                    TraitPath(mesh, 10, 1, 3, 10, 8, 10, 7, 17, 15, 7, 10, 7, 10, 1);
                    break;
                case Mark.Assertiveness:
                    TraitPath(mesh, 4, 17, 4, 2, 14, 2, 12, 5, 14, 8, 4, 8);
                    break;
                case Mark.Playfulness:
                    TraitPath(mesh, 8, 2, 10, 7, 15, 9, 10, 11, 8, 16, 6, 11, 1, 9, 6, 7, 8, 2);
                    TraitPath(mesh, 15, 2, 15, 6); TraitPath(mesh, 13, 4, 17, 4);
                    break;
                case Mark.Openness:
                    TraitPath(mesh, 3, 16, 3, 2, 14, 2, 14, 16);
                    TraitPath(mesh, 3, 2, 10, 5, 10, 17, 3, 16); TraitPath(mesh, 7, 10, 8, 10);
                    break;
                case Mark.Energy:
                    TraitPath(mesh, 2, 4.5f, 14.5f, 4.5f, 14.5f, 13.5f, 2, 13.5f, 2, 4.5f);
                    TraitPath(mesh, 15, 7, 17, 7, 17, 11, 15, 11);
                    TraitPath(mesh, 5.5f, 7, 5.5f, 11); TraitPath(mesh, 8.5f, 7, 8.5f, 11);
                    break;
                case Mark.Directness:
                    TraitPath(mesh, 2, 9, 15, 9); TraitPath(mesh, 10, 4, 15, 9, 10, 14);
                    break;
                case Mark.Confidence:
                    TraitPath(mesh, 9, 1, 15, 4, 14.5f, 10, 12, 14, 9, 17, 6, 14, 3.5f, 10, 3, 4, 9, 1);
                    TraitPath(mesh, 6, 9, 8, 11.5f, 12, 7);
                    break;
                case Mark.Curiosity:
                    Ring(mesh, new Vector2(7.5f / 18f, 10.5f / 18f), 5f / 18f);
                    TraitPath(mesh, 11, 11, 16, 16);
                    break;
                case Mark.Ink:
                    Stamp(mesh);
                    break;
            }
        }

        void LateUpdate()
        {
            if (Label != null && color != Label.color)
                color = Label.color;
        }

        void Speaker(VertexHelper mesh, bool muted)
        {
            Line(mesh, 0.10f, 0.36f, 0.28f, 0.36f);
            Line(mesh, 0.28f, 0.36f, 0.53f, 0.16f);
            Line(mesh, 0.53f, 0.16f, 0.53f, 0.84f);
            Line(mesh, 0.53f, 0.84f, 0.28f, 0.64f);
            Line(mesh, 0.28f, 0.64f, 0.10f, 0.64f);
            Line(mesh, 0.10f, 0.64f, 0.10f, 0.36f);
            if (muted)
            {
                Line(mesh, 0.68f, 0.34f, 0.94f, 0.66f);
                Line(mesh, 0.68f, 0.66f, 0.94f, 0.34f);
                return;
            }

            Line(mesh, 0.68f, 0.31f, 0.78f, 0.50f);
            Line(mesh, 0.78f, 0.50f, 0.68f, 0.69f);
            Line(mesh, 0.81f, 0.20f, 0.95f, 0.50f);
            Line(mesh, 0.95f, 0.50f, 0.81f, 0.80f);
        }

        void Box(VertexHelper mesh, float left, float bottom, float right, float top)
        {
            Line(mesh, left, bottom, right, bottom);
            Line(mesh, right, bottom, right, top);
            Line(mesh, right, top, left, top);
            Line(mesh, left, top, left, bottom);
        }

        void Ring(VertexHelper mesh, Vector2 center, float radius)
        {
            var from = center + Vector2.right * radius;
            for (var i = 1; i <= 12; i++)
            {
                var angle = i * (2f * Mathf.PI / 12f);
                var to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(mesh, from.x, from.y, to.x, to.y);
                from = to;
            }
        }

        void SettingsCog(VertexHelper mesh)
        {
            var center = new Vector2(0.5f, 0.5f);
            Ring(mesh, center, 0.13f);
            var from = center + Vector2.right * 0.33f;
            for (var i = 1; i <= 24; i++)
            {
                var angle = i * (2f * Mathf.PI / 24f);
                var radius = i % 4 == 0 || i % 4 == 3 ? 0.33f : 0.42f;
                var to = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Line(mesh, from.x, from.y, to.x, to.y);
                from = to;
            }
        }

        void Line(VertexHelper mesh, float x0, float y0, float x1, float y1)
        {
            var offset = new Vector2(y0 - y1, x1 - x0).normalized * 0.035f;
            var index = mesh.currentVertCount;
            Vertex(mesh, x0 + offset.x, y0 + offset.y);
            Vertex(mesh, x1 + offset.x, y1 + offset.y);
            Vertex(mesh, x1 - offset.x, y1 - offset.y);
            Vertex(mesh, x0 - offset.x, y0 - offset.y);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        void TraitPath(VertexHelper mesh, params float[] coordinates)
        {
            const float basis = 18f;
            for (var i = 0; i + 3 < coordinates.Length; i += 2)
                Line(mesh, coordinates[i] / basis, 1f - coordinates[i + 1] / basis,
                    coordinates[i + 2] / basis, 1f - coordinates[i + 3] / basis);
        }

        void Stamp(VertexHelper mesh)
        {
            // Broad pink paint holds the label, with irregular yellow marks at the edges.
            Vertex(mesh, 0.00f, 0.08f);
            Vertex(mesh, 0.05f, 0.55f);
            Vertex(mesh, 0.02f, 0.77f);
            Vertex(mesh, 0.15f, 0.98f);
            Vertex(mesh, 0.87f, 0.89f);
            Vertex(mesh, 1.00f, 0.96f);
            Vertex(mesh, 0.97f, 0.62f);
            Vertex(mesh, 1.00f, 0.42f);
            Vertex(mesh, 0.93f, 0.08f);
            Vertex(mesh, 0.12f, 0.14f);
            for (var i = 1; i <= 8; i++)
                mesh.AddTriangle(0, i, i + 1);

            Color32 yellow = new Color32(255, 215, 59, 255);
            Shard(mesh, yellow, 0.01f, 0.14f, 0.34f, 0.22f, 0.08f, 0.43f);
            Shard(mesh, yellow, 0.72f, 0.77f, 1.02f, 1.03f, 0.91f, 0.75f);
            Shard(mesh, yellow, 0.80f, 0.12f, 0.95f, 0.04f, 1.02f, 0.18f);
        }

        void Shard(VertexHelper mesh, Color32 ink, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            var index = mesh.currentVertCount;
            Vertex(mesh, x0, y0, ink);
            Vertex(mesh, x1, y1, ink);
            Vertex(mesh, x2, y2, ink);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        void Vertex(VertexHelper mesh, float x, float y) => Vertex(mesh, x, y, color);

        void Vertex(VertexHelper mesh, float x, float y, Color32 ink)
        {
            var rect = rectTransform.rect;
            var position = new Vector3(rect.xMin + x * rect.width, rect.yMin + y * rect.height, 0f);
            mesh.AddVert(position, ink, Vector2.zero);
        }
    }
}
