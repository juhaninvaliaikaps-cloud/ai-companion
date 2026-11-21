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
            { "CompanionName", "setup wizard (Data > Run setup again)" },
            { "Relationship", "setup wizard (Data > Run setup again)" },
            { "ScreenMonitorDevice", "set by picking a display on the Screen tab (ScreenLookPresenter.SelectMonitor)" },
            { "DesktopFilesPermission", "Tools tab uses DesktopToolPermissions.SetState" },
            { "DesktopAppsPermission", "Tools tab uses DesktopToolPermissions.SetState" },
            { "DesktopClipboardPermission", "Tools tab uses DesktopToolPermissions.SetState" },
            { "DesktopCalendarPermission", "Tools tab uses DesktopToolPermissions.SetState" },
            { "DesktopWebPermission", "Tools tab uses DesktopToolPermissions.SetState" },
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
        public void TheTabsAreTheJuh121Layout()
        {
            var source = ViewSource();
            StringAssert.Contains("{ \"Persona\", \"AI\", \"Voice\", \"Look\", \"Chat\", \"Tools\", \"Screen\", \"Window\", \"Data\", \"18+\", \"Keys\", \"Developer\" }", source);
        }

        [Test]
        public void DeveloperControlsAreOnlyOnTheDeveloperTab()
        {
            var source = ViewSource();
            var developer = source.IndexOf("void BuildDeveloper()", System.StringComparison.Ordinal);
            Assert.Greater(developer, 0);
            foreach (var call in new[] { "BuildVideoTests();", "S.ShowDebugInfo", "S.ShowMoodDebug", "S.TimeoutSeconds" })
            {
                var at = source.IndexOf(call, System.StringComparison.Ordinal);
                Assert.Greater(at, developer, call + " belongs on the Developer tab");
            }
        }

        [Test]
        public void ShowDeveloperSettings_DefaultsOff_AndRoundTrips()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.ShowDeveloperSettings);
            fresh.ShowDeveloperSettings = true;
            Assert.IsTrue(CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None).ShowDeveloperSettings);
        }

        [Test]
        public void PisaraCloud_DefaultsOff_HasItsOwnFields_AndRoundTrips()
        {
            var fresh = CompanionSettings.CreateDefault();
            Assert.IsFalse(fresh.PisaraCloud);
            Assert.AreEqual(string.Empty, fresh.PisaraCloudBaseUrl);
            Assert.AreEqual(string.Empty, fresh.PisaraCloudModel);

            fresh.PisaraCloud = true;
            fresh.PisaraCloudBaseUrl = "https://api.anthropic.com/v1/";
            fresh.PisaraCloudModel = "claude-opus-5-5";
            var parsed = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(fresh), SettingsLegacyPrefs.None);
            Assert.IsTrue(parsed.PisaraCloud);
            Assert.AreEqual("https://api.anthropic.com/v1/", parsed.PisaraCloudBaseUrl);
            Assert.AreEqual("claude-opus-5-5", parsed.PisaraCloudModel);

            var source = ViewSource();
            StringAssert.Contains("Pisara model", source);
            StringAssert.Contains("Use a cloud model for Pisara readings", source);
            StringAssert.Contains("Pisara base URL", source);
            StringAssert.Contains("Pisara API key", source);
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
        public void ClassicSettingsButtonIsGone()
        {
            var source = ViewSource();
            StringAssert.DoesNotContain("Classic settings", source);
            StringAssert.DoesNotContain("OpenClassic", source);
        }
    }
}
