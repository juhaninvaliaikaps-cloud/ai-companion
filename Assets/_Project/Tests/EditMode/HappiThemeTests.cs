using AICompanion.NewUI;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    public sealed class HappiThemeTests
    {
        [Test]
        public void Palette_MatchesTheApprovedHexValues()
        {
            var theme = new HappiTheme();
            Assert.AreEqual("#3B6FB6", HappiTheme.ToHex(theme.WheelBackground));
            Assert.AreEqual("#7FA8E0", HappiTheme.ToHex(theme.Border));
            Assert.AreEqual("#9FD4FF", HappiTheme.ToHex(theme.Hover));
            Assert.AreEqual("#0B1530", HappiTheme.ToHex(theme.HoverText));
            Assert.AreEqual("#EAF2FF", HappiTheme.ToHex(theme.Text));
            Assert.AreEqual("#B4C3DC", HappiTheme.ToHex(theme.Disabled));
            Assert.AreEqual("#FF7EB6", HappiTheme.ToHex(theme.Intimate));
        }

        [Test]
        public void WheelBackground_IsSlightlyTransparent()
        {
            var theme = new HappiTheme();
            Assert.That(theme.WheelBackground.a, Is.InRange(0.85f, 0.95f));
        }

        [Test]
        public void Hex_ParsesWithAndWithoutHashAndAlpha()
        {
            Assert.AreEqual("#3B6FB6", HappiTheme.ToHex(HappiTheme.Hex("3B6FB6")));
            var withAlpha = HappiTheme.Hex("#3B6FB680");
            Assert.AreEqual(128, ((Color32)withAlpha).a);
        }

        [Test]
        public void Hex_BadInputIsMagentaSoMistakesAreVisible()
        {
            Assert.AreEqual(Color.magenta, HappiTheme.Hex("nope"));
            Assert.AreEqual(Color.magenta, HappiTheme.Hex(null));
            Assert.AreEqual(Color.magenta, HappiTheme.Hex("#12345"));
        }

        [Test]
        public void Text_IsReadableOnTheWheelBlue()
        {
            var theme = new HappiTheme();
            var background = HappiTheme.WithAlpha(theme.WheelBackground, 1f);
            Assert.That(HappiTheme.Contrast(theme.Text, background), Is.GreaterThanOrEqualTo(4.0f));
        }

        [Test]
        public void HoverText_IsReadableOnTheHoverColour()
        {
            var theme = new HappiTheme();
            Assert.That(HappiTheme.Contrast(theme.HoverText, theme.Hover), Is.GreaterThanOrEqualTo(7f));
        }

        [Test]
        public void TextOn_PicksDarkTextOnHoverAndLightTextOnTheWheel()
        {
            var theme = new HappiTheme();
            Assert.AreEqual(theme.HoverText, theme.TextOn(theme.Hover));
            Assert.AreEqual(theme.Text, theme.TextOn(theme.WheelBackground));
        }

        [TestCase(96f, 1f)]
        [TestCase(144f, 1.5f)]
        [TestCase(192f, 2f)]
        [TestCase(72f, 1f)]
        [TestCase(0f, 1f)]
        [TestCase(1000f, 2.5f)]
        public void UiScale_FollowsWindowsScalingWithinLimits(float dpi, float expected)
        {
            Assert.AreEqual(expected, HappiTheme.ScaleForDpi(dpi), 0.001f);
        }

        [Test]
        public void UiScale_OverrideWins()
        {
            var theme = new HappiTheme { UiScaleOverride = 1.75f };
            Assert.AreEqual(1.75f, theme.UiScale(96f), 0.001f);
        }

        [Test]
        public void Current_CanBeReplacedInOnePlace()
        {
            var before = HappiTheme.Current;
            try
            {
                var custom = new HappiTheme { CornerRadius = 3f };
                HappiTheme.Current = custom;
                Assert.AreSame(custom, HappiTheme.Current);
            }
            finally
            {
                HappiTheme.Current = before;
            }
        }
    }
}
