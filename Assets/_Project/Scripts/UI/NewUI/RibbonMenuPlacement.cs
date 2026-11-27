using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Matches a pointer release to the flyout button that received its press.</summary>
    public struct RibbonFlyoutPress
    {
        int _heldTarget;

        public int Update(int target, bool pressed, bool released)
        {
            if (pressed)
                _heldTarget = target + 1;
            if (!released)
                return -1;

            var activate = target >= 0 && _heldTarget == target + 1;
            Clear();
            return activate ? target : -1;
        }

        public void Clear() => _heldTarget = 0;
    }

    /// <summary>Ribbon layout in overlay pixels, with the origin at the top left.</summary>
    public static class RibbonMenuPlacement
    {
        public const float PreferredFlyoutWidth = 1000f;
        public const float PreferredPersonaWidth = 980f;

        public static Rect MoodBadge(Rect area, float headX, Rect controls, float gap, float size)
        {
            var x = controls.center.x >= headX ? headX - gap - size : headX + gap;
            if (size <= 0f || x < area.xMin || x + size > area.xMax || size > area.height)
                return default;
            var y = Mathf.Clamp(controls.center.y - size * 0.5f, area.yMin, area.yMax - size);
            return new Rect(x, y, size, size);
        }

        public static float FitScale(Vector2 visibleSize, Vector2 menuSize, float dpiScale)
        {
            return Mathf.Min(dpiScale, Mathf.Min(visibleSize.x / menuSize.x, visibleSize.y / menuSize.y));
        }

        public static (float Trigger, float Menu) Stack(float headY, Rect area, float triggerHeight, float menuHeight)
        {
            var trigger = Mathf.Clamp(headY - triggerHeight, area.yMin, area.yMax - triggerHeight);
            var below = area.yMax - trigger - triggerHeight;
            var above = trigger - area.yMin;
            if (below >= menuHeight || below >= above)
            {
                trigger = Mathf.Min(trigger, area.yMax - triggerHeight - menuHeight);
                return (trigger, trigger + triggerHeight);
            }

            trigger = Mathf.Max(trigger, area.yMin + menuHeight);
            return (trigger, trigger - menuHeight);
        }

        public static float MenuX(Rect area, float triggerX, float width, float flyoutMinimum)
        {
            var x = Mathf.Clamp(triggerX, area.xMin, area.xMax - width);
            var right = area.xMax - x - width;
            var left = x - area.xMin;
            if (right >= flyoutMinimum || left >= flyoutMinimum)
                return x;

            return right >= left ? area.xMax - width - flyoutMinimum : area.xMin + flyoutMinimum;
        }

        public static bool OpensRight(Rect area, Rect menu, float preferredWidth)
        {
            var right = area.xMax - menu.xMax;
            return right >= preferredWidth || right >= menu.xMin - area.xMin;
        }

        public static float FlyoutWidth(Rect area, Rect menu, float preferredWidth, bool opensRight)
        {
            var room = opensRight ? area.xMax - menu.xMax : menu.xMin - area.xMin;
            return Mathf.Min(preferredWidth, room);
        }

        public static Rect Flyout(Rect area, Rect menu, Vector2 size, float rowOffset, bool opensRight)
        {
            var width = FlyoutWidth(area, menu, size.x, opensRight);
            var x = opensRight ? menu.xMax : menu.xMin - width;
            var y = Mathf.Clamp(menu.yMin + rowOffset, area.yMin, area.yMax - size.y);
            return new Rect(x, y, width, size.y);
        }

        public static (float Overlap, Vector2 PaperEdge) ScrollJoin(float curlWidth)
        {
            // The cutout leans toward the art at the top. Join the paper under its shaft
            // just inside the end caps, and let the cutout straddle the edge of the tabs.
            var overlap = curlWidth * 0.46f;
            var edge = new Vector2(curlWidth * 0.76f - overlap, curlWidth * 0.29f - overlap);
            return (overlap, edge);
        }

        public static Vector2 ScrollFlare(float width, float scale, float availableHeight)
        {
            var strength = Mathf.InverseLerp(240f * scale, PreferredFlyoutWidth * scale, width);
            var total = Mathf.Min(67f * scale * strength, Mathf.Max(0f, availableHeight));
            return new Vector2(total * (18f / 67f), total * (49f / 67f));
        }

        public static bool AimsAtFlyout(Vector2 start, Vector2 pointer, Rect flyout, bool opensRight, float padding)
        {
            var edgeX = opensRight ? flyout.xMin : flyout.xMax;
            var travel = pointer.x - start.x;
            var distance = edgeX - start.x;
            if (Mathf.Abs(travel) < 1f || Mathf.Abs(distance) < 1f)
                return false;

            var fraction = travel / distance;
            if (fraction <= 0f || fraction > 1f)
                return false;

            var crossingY = start.y + (pointer.y - start.y) / fraction;
            return crossingY >= flyout.yMin - padding && crossingY <= flyout.yMax + padding;
        }

        public static Rect IllustrationUv(float textureAspect, float frameAspect, float focalX, float sourceStart = 0f)
        {
            var width = Mathf.Min(1f - sourceStart, frameAspect / textureAspect);
            var height = Mathf.Min(1f, width * textureAspect / frameAspect);
            var x = Mathf.Clamp(focalX - width * 0.5f, sourceStart, 1f - width);
            return new Rect(x, (1f - height) * 0.5f, width, height);
        }

        public static (Rect Frame, Rect Uv) PersonaPainting(Rect area, float textureAspect)
        {
            const float trim = 0.26f;
            var poseAspect = textureAspect * (1f - trim);
            var width = Mathf.Min(area.width, area.height * poseAspect);
            var height = width / poseAspect;
            var frame = new Rect(area.xMax - width, area.center.y - height * 0.5f, width, height);
            return (frame, new Rect(trim, 0f, 1f - trim, 1f));
        }

        public static (Rect Bounds, bool OpensUp) PersonaBanner(Rect area, Rect header, Vector2 preferred, float minAbove)
        {
            var top = Mathf.Clamp(header.yMin, area.yMin, area.yMax);
            var bottom = Mathf.Clamp(header.yMax, area.yMin, area.yMax);
            var above = top - area.yMin;
            var below = area.yMax - bottom;
            var opensUp = above >= Mathf.Min(preferred.y, minAbove) || above >= below;
            var width = Mathf.Min(preferred.x, area.width);
            var height = Mathf.Min(preferred.y, opensUp ? above : below);
            var x = Mathf.Clamp(header.xMin, area.xMin, area.xMax - width);
            var y = opensUp ? top - height : bottom;
            return (new Rect(x, y, width, height), opensUp);
        }
    }
}
