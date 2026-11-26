using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>The cyan brushstroke beside Send, with a single sweep on hover or send.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class RibbonSendFlourish : MaskableGraphic
    {
        const int CurveSteps = 18;
        float _hoverAt = float.NegativeInfinity;
        float _sentAt = float.NegativeInfinity;
        bool _highlighted;

        public void PlaySend()
        {
            _sentAt = Time.unscaledTime;
        }

        public void Refresh(bool sendAvailable, bool highlighted)
        {
            var now = Time.unscaledTime;
            highlighted = sendAvailable && highlighted;
            if (highlighted && !_highlighted)
                _hoverAt = now;
            _highlighted = highlighted;

            var hover = highlighted ? Pulse(now - _hoverAt, 0.24f) : 0f;
            var sent = Pulse(now - _sentAt, 0.34f);
            var visible = sendAvailable || sent > 0f;
            if (enabled != visible)
                enabled = visible;
            if (!visible)
                return;

            var push = Mathf.Max(hover * 0.42f, sent);
            var opacity = sendAvailable ? (highlighted ? 1f : 0.73f) : sent;
            canvasRenderer.SetAlpha(Mathf.Clamp01(opacity + push * 0.22f));
            rectTransform.localScale = new Vector3(1f + push * 0.16f, 1f + push * 0.24f, 1f);
        }

        static float Pulse(float elapsed, float duration)
        {
            var progress = Mathf.Clamp01(elapsed / duration);
            return Mathf.Min(progress / 0.22f, (1f - progress) / 0.78f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Stroke(mesh, rect, 0.20f, new Color32(0, 198, 219, 51), new Color32(0, 213, 243, 68));
            Stroke(mesh, rect, 0.10f, new Color32(2, 208, 242, 255), new Color32(129, 255, 255, 255));
            Shard(mesh, rect, new Vector2(0.26f, 0.28f), new Vector2(0.64f, 0.71f), new Vector2(0.48f, 0.43f));
            Shard(mesh, rect, new Vector2(0.72f, 0.73f), new Vector2(0.90f, 0.90f), new Vector2(0.83f, 0.74f));
        }

        static void Stroke(VertexHelper mesh, Rect rect, float width, Color32 tail, Color32 tip)
        {
            var first = mesh.currentVertCount;
            for (var i = 0; i <= CurveSteps; i++)
            {
                var t = i / (float)CurveSteps;
                var point = Point(rect, Curve(t));
                var delta = Point(rect, Curve(Mathf.Min(1f, t + 0.01f)))
                    - Point(rect, Curve(Mathf.Max(0f, t - 0.01f)));
                var taper = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.04f, 1f, t);
                var normal = new Vector2(-delta.y, delta.x).normalized * (rect.height * width * taper);
                var color = Color32.Lerp(tail, tip, t);
                mesh.AddVert(point + normal, color, Vector2.zero);
                mesh.AddVert(point - normal, color, Vector2.zero);
                if (i == 0)
                    continue;
                var k = first + i * 2;
                mesh.AddTriangle(k - 2, k, k - 1);
                mesh.AddTriangle(k - 1, k, k + 1);
            }
        }

        static Vector2 Curve(float t)
        {
            if (t < 0.68f)
                return Bezier(new Vector2(0.03f, 0.22f), new Vector2(0.32f, 0.46f),
                    new Vector2(0.62f, 0.78f), new Vector2(0.82f, 0.71f), t / 0.68f);
            return Bezier(new Vector2(0.82f, 0.71f), new Vector2(1.03f, 0.72f),
                new Vector2(0.96f, 0.45f), new Vector2(0.77f, 0.04f), (t - 0.68f) / 0.32f);
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static void Shard(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c)
        {
            var i = mesh.currentVertCount;
            var ink = new Color32(28, 232, 250, 235);
            mesh.AddVert(Point(rect, a), ink, Vector2.zero);
            mesh.AddVert(Point(rect, b), ink, Vector2.zero);
            mesh.AddVert(Point(rect, c), ink, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
        }

        static Vector2 Point(Rect rect, Vector2 uv) =>
            new(rect.xMin + rect.width * uv.x, rect.yMin + rect.height * uv.y);
    }
}
