using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AICompanion.Settings;
using NUnit.Framework;
using UnityEngine;

namespace AICompanion.Tests.EditMode
{
    // JUH-104 PR E: the new settings view must cover every setting (INVENTORY.md section 4).
    public sealed class HappiSettingsCoverageTests
    {
        /// <summary>Fields the settings view does not edit directly, and where they are reached instead.</summary>
        static readonly Dictionary<string, string> Elsewhere = new Dictionary<string, string>
        {
            { "Version", "file format number, not a user setting" },
            { "CompanionName", "setup wizard (Privacy > Run setup again)" },
            { "Relationship", "setup wizard (Privacy > Run setup again)" },
            { "ScreenMonitorDevice", "set by picking a display on the Screen tab (ScreenLookPresenter.SelectMonitor)" },
        };

        static string ViewSource()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Scripts/UI/NewUI/HappiSettingsView.cs");
            Assert.IsTrue(File.Exists(path), path);
            return File.ReadAllText(path);
        }

        [Test]
        public void EverySettingHasAControlInTheNewSettingsView()
        {
            var source = ViewSource();
            var missing = new List<string>();
            foreach (var field in typeof(CompanionSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (Elsewhere.ContainsKey(field.Name))
                    continue;
                if (source.IndexOf("." + field.Name, System.StringComparison.Ordinal) < 0)
                    missing.Add(field.Name);
            }

            CollectionAssert.IsEmpty(missing, "settings without a control in HappiSettingsView: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryOldTabHasANewTab()
        {
            var source = ViewSource();
            foreach (var tab in new[] { "\"AI\"", "\"Avatar\"", "\"Voice\"", "\"Controls\"", "\"Overlay\"", "\"Privacy\"", "\"Persona\"", "\"Screen\"" })
                StringAssert.Contains(tab, source);
        }

        [Test]
        public void AdultToggleKeepsTheSameWordingAndCondition()
        {
            var source = ViewSource();
            StringAssert.Contains("Intimate expressions (18+)", source);
            StringAssert.Contains("Works only in Private mode (local model)", source);
            StringAssert.Contains("portraits-adult", source);
        }

        [Test]
        public void ClassicSettingsStayReachable()
        {
            StringAssert.Contains("OpenClassic", ViewSource());
        }
    }
}
