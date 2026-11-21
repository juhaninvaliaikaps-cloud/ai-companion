using System;
using System.IO;
using AICompanion.Core;
using AICompanion.Settings;
using NUnit.Framework;

namespace AICompanion.Tests.EditMode
{
    /// <summary>JUH-27: every desktop capability is ask-by-default, permissioned per tool and audited.</summary>
    public sealed class DesktopToolPermissionTests
    {
        string _auditPath;
        string _oldOverride;

        [SetUp]
        public void SetUp()
        {
            _oldOverride = DesktopToolAuditLog.PathOverride;
            _auditPath = Path.Combine(Path.GetTempPath(), "happi-tool-audit-" + Guid.NewGuid().ToString("N") + ".json");
            DesktopToolAuditLog.PathOverride = _auditPath;
            DesktopToolPermissions.ConsentHandler = null;
        }

        [TearDown]
        public void TearDown()
        {
            DesktopToolPermissions.ConsentHandler = null;
            DesktopToolAuditLog.PathOverride = _oldOverride;
            try
            {
                if (File.Exists(_auditPath))
                    File.Delete(_auditPath);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void EveryTool_StartsAsAsk()
        {
            var settings = CompanionSettings.CreateDefault();
            for (var i = 0; i < DesktopToolCatalog.All.Length; i++)
                Assert.AreEqual(DesktopToolPermissions.Ask, DesktopToolPermissions.State(settings, DesktopToolCatalog.All[i]));
        }

        [Test]
        public void AllowedTool_RunsImmediately()
        {
            var settings = CompanionSettings.CreateDefault();
            DesktopToolPermissions.SetState(settings, DesktopToolKind.Web, DesktopToolPermissions.Allowed);
            var called = false;

            DesktopToolPermissions.Request(settings, DesktopToolKind.Web, "search web", allow =>
            {
                called = allow;
            });

            Assert.IsTrue(called);
        }

        [Test]
        public void DeniedTool_IsRefusedWithoutPrompting()
        {
            var settings = CompanionSettings.CreateDefault();
            DesktopToolPermissions.SetState(settings, DesktopToolKind.Files, DesktopToolPermissions.Denied);
            var prompted = false;
            DesktopToolPermissions.ConsentHandler = (kind, answer) => prompted = true;
            var called = true;

            DesktopToolPermissions.Request(settings, DesktopToolKind.Files, "find files", allow => called = allow);

            Assert.IsFalse(prompted);
            Assert.IsFalse(called);
        }

        [Test]
        public void FirstUse_AsksAndRemembersTheAnswer()
        {
            var settings = CompanionSettings.CreateDefault();
            var asked = false;
            DesktopToolPermissions.ConsentHandler = (kind, answer) =>
            {
                asked = kind == DesktopToolKind.Clipboard;
                answer(true);
            };
            var called = false;

            DesktopToolPermissions.Request(settings, DesktopToolKind.Clipboard, "read clipboard", allow => called = allow);

            Assert.IsTrue(asked);
            Assert.IsTrue(called);
            Assert.AreEqual(DesktopToolPermissions.Allowed, DesktopToolPermissions.State(settings, DesktopToolKind.Clipboard));
        }

        [Test]
        public void ActionLog_RecordsAnAttempt()
        {
            var settings = CompanionSettings.CreateDefault();
            DesktopToolPermissions.SetState(settings, DesktopToolKind.Apps, DesktopToolPermissions.Allowed);

            DesktopToolPermissions.Request(settings, DesktopToolKind.Apps, "launch app", null);

            var entries = DesktopToolAuditLog.Recent(5);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Apps", entries[0].tool);
            Assert.IsTrue(entries[0].allowed);
        }

        [Test]
        public void PermissionStates_RoundTripThroughSettings()
        {
            var settings = CompanionSettings.CreateDefault();
            DesktopToolPermissions.SetState(settings, DesktopToolKind.Calendar, DesktopToolPermissions.Allowed);
            var parsed = CompanionSettingsJson.Parse(CompanionSettingsJson.Write(settings), SettingsLegacyPrefs.None);
            Assert.AreEqual(DesktopToolPermissions.Allowed, DesktopToolPermissions.State(parsed, DesktopToolKind.Calendar));
        }
    }
}
