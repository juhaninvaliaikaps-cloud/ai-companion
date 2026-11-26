using UnityEngine;

namespace AICompanion.NewUI
{
    /// <summary>Ribbon layout in overlay pixels, with the origin at the top left.</summary>
    public static class RibbonMenuPlacement
    {
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

        public static Rect Flyout(Rect area, Rect menu, Vector2 size, float rowOffset, bool opensRight)
        {
            var room = opensRight ? area.xMax - menu.xMax : menu.xMin - area.xMin;
            var width = Mathf.Min(size.x, room);
            var x = opensRight ? menu.xMax : menu.xMin - width;
            var y = Mathf.Clamp(menu.yMin + rowOffset, area.yMin, area.yMax - size.y);
            return new Rect(x, y, width, size.y);
        }

        public static Rect IllustrationUv(float textureAspect, float frameAspect, float focalX,
            float revealedFraction, bool opensRight)
        {
            var width = Mathf.Min(1f, frameAspect / textureAspect);
            var height = Mathf.Min(1f, textureAspect / frameAspect);
            var x = Mathf.Clamp(focalX - width * 0.5f, 0f, 1f - width);
            var visibleWidth = width * revealedFraction;
            return new Rect(opensRight ? x : x + width - visibleWidth, (1f - height) * 0.5f, visibleWidth, height);
        }
    }
}
