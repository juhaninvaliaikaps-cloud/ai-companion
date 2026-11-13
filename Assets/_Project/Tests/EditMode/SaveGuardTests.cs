using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AICompanion.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AICompanion.Tests.EditMode
{
    public class SaveGuardTests
    {
        readonly System.Collections.Generic.List<string> _cleanup = new System.Collections.Generic.List<string>();

        [TearDown]
        public void TearDown()
        {
            InstallKeyFile.KeyForPath = null;
            SingleInstanceGuard.SetExitingForTests(false);
            for (var i = 0; i < _cleanup.Count; i++)
            {
                try
                {
                    if (Directory.Exists(_cleanup[i]))
                        Directory.Delete(_cleanup[i], true);
                }
                catch (IOException)
                {
                }
            }

            _cleanup.Clear();
        }

        [Test]
        public void DifferentPidHeld_IsRestoredWhenDestinationIsMissing()
        {
            var key = new byte[32];
            InstallKeyFile.KeyForPath = _ => key;
            SingleInstanceGuard.SetExitingForTests(false);
            var path = Path.Combine(NewFolder(), "memory.json");
            var held = ProtectedLocalFile.HeldPath(path, 424242);
            File.WriteAllBytes(held, Envelope(path, key, "BACKUP"));

            var read = ProtectedLocalFile.Read(path);

            Assert.AreEqual(LocalReadStatus.Ready, read.Status);
            Assert.AreEqual("BACKUP", read.Text);
            Assert.IsFalse(File.Exists(held));
        }

        [Test]
        public void LegacyUnsuffixedHeld_IsRestoredWhenDestinationIsMissing()
        {
            var key = new byte[32];
            InstallKeyFile.KeyForPath = _ => key;
            SingleInstanceGuard.SetExitingForTests(false);
            var path = Path.Combine(NewFolder(), "memory.json");
            var held = path + ".held";
            File.WriteAllBytes(held, Envelope(path, key, "LEGACY"));

            var read = ProtectedLocalFile.Read(path);

            Assert.AreEqual(LocalReadStatus.Ready, read.Status);
            Assert.AreEqual("LEGACY", read.Text);
            Assert.IsFalse(File.Exists(held));
        }

        [Test]
        public void UnreadableDestination_IsReplacedFromHeld()
        {
            var key = new byte[32];
            InstallKeyFile.KeyForPath = _ => key;
            SingleInstanceGuard.SetExitingForTests(false);
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllBytes(path, new byte[] { 0xFF, 0xFE });
            var held = ProtectedLocalFile.HeldPath(path, 424243);
            File.WriteAllBytes(held, Envelope(path, key, "RECOVERED"));

            var read = ProtectedLocalFile.Read(path);

            Assert.AreEqual(LocalReadStatus.Ready, read.Status);
            Assert.AreEqual("RECOVERED", read.Text);
            Assert.IsFalse(File.Exists(held));
        }

        [Test]
        public void NewWrite_DoesNotDeleteAnotherProcessesHeldFile()
        {
            var key = new byte[32];
            InstallKeyFile.KeyForPath = _ => key;
            SingleInstanceGuard.SetExitingForTests(false);
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            var other = ProtectedLocalFile.HeldPath(path, 424244);
            File.WriteAllText(other, "OTHER");

            Assert.IsTrue(ProtectedLocalFile.Write(path, "NEW"));
            Assert.IsTrue(File.Exists(other), "another process's held file must not be deleted by a new write");
        }

        [Test]
        public void ExitingInstance_DoesNotReadOrWriteSaves()
        {
            var path = Path.Combine(NewFolder(), "settings.json");
            File.WriteAllText(path, "SENTINEL");
            SingleInstanceGuard.SetExitingForTests(true);
            try
            {
                Assert.AreEqual(LocalReadStatus.Missing, ProtectedLocalFile.Read(path).Status);
                Assert.IsFalse(ProtectedLocalFile.Write(path, "NEW"));
                Assert.AreEqual("SENTINEL", File.ReadAllText(path));
            }
            finally
            {
                SingleInstanceGuard.SetExitingForTests(false);
            }
        }

        [Test]
        public void ExitingInstance_DoesNotMigratePlaintext()
        {
            var root = Path.Combine(NewFolder(), "ProjectLeia");
            Directory.CreateDirectory(root);
            var plain = Path.Combine(root, "memory.json");
            File.WriteAllText(plain, "PLAINTEXT");
            SingleInstanceGuard.SetExitingForTests(true);
            try
            {
                var result = LocalDataActions.MigratePlaintext(root);
                Assert.IsTrue(result.Ok);
                Assert.AreEqual(0, result.Files);
                Assert.AreEqual("PLAINTEXT", File.ReadAllText(plain));
            }
            finally
            {
                SingleInstanceGuard.SetExitingForTests(false);
            }
        }

        [Test]
        public void WriteFailure_LogsTheStepTypeMessageAndFileName_NotAStaleVerifyMessage()
        {
            var path = Path.Combine(NewFolder(), "missing-directory", "memory.json");
            var bytes = Encoding.UTF8.GetBytes("content");

            LogAssert.Expect(LogType.Warning, new Regex("Local file write failed at write \\.writing for memory\\.json: DirectoryNotFoundException"));

            var outcome = ProtectedLocalFile.CommitIfVerified(path, bytes, bytes, b => b, 101);

            Assert.AreEqual(LocalCommitOutcome.WriteFailed, outcome);
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 101)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.HeldPath(path, 101)));
        }

        [Test]
        public void ReadBackFailure_LogsTheStepAndKeepsTheOriginal()
        {
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            var bytes = Encoding.UTF8.GetBytes("NEXT");

            LogAssert.Expect(LogType.Warning, new Regex("Local file write failed at read-back verify for memory\\.json: IOException"));

            var outcome = ProtectedLocalFile.CommitIfVerified(
                path, bytes, bytes, _ => throw new IOException("simulated read-back failure"), 102);

            Assert.AreEqual(LocalCommitOutcome.ReadBackFailed, outcome);
            Assert.AreEqual("ORIGINAL", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 102)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.HeldPath(path, 102)));
        }

        [Test]
        public void BackupFailure_LogsTheHeldStep()
        {
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            Directory.CreateDirectory(ProtectedLocalFile.HeldPath(path, 103));
            var bytes = Encoding.UTF8.GetBytes("NEXT");

            LogAssert.Expect(LogType.Warning, new Regex("Local file write failed at \\.held backup for memory\\.json: "));

            var outcome = ProtectedLocalFile.CommitIfVerified(path, bytes, bytes, b => b, 103);

            Assert.AreEqual(LocalCommitOutcome.BackupFailed, outcome);
            Assert.AreEqual("ORIGINAL", File.ReadAllText(path));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 103)));
        }

        [Test]
        public void CommitFailure_LogsTheCommitStepAndKeepsTheTempClean()
        {
            var path = Path.Combine(NewFolder(), "memory.json");
            Directory.CreateDirectory(path);
            var bytes = Encoding.UTF8.GetBytes("NEXT");

            LogAssert.Expect(LogType.Warning, new Regex("Local file write failed at commit for memory\\.json: "));

            var outcome = ProtectedLocalFile.CommitIfVerified(path, bytes, bytes, b => b, 104);

            Assert.AreEqual(LocalCommitOutcome.CommitFailed, outcome);
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 104)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.HeldPath(path, 104)));
        }

        [Test]
        public void TwoWritersWithDifferentProcessIds_DoNotClobberEachOthersTempFiles()
        {
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            var first = Encoding.UTF8.GetBytes("FIRST");
            var second = Encoding.UTF8.GetBytes("SECOND");
            Assert.AreNotEqual(ProtectedLocalFile.WritingPath(path, 201), ProtectedLocalFile.WritingPath(path, 202));
            Assert.AreNotEqual(ProtectedLocalFile.HeldPath(path, 201), ProtectedLocalFile.HeldPath(path, 202));

            var nested = false;
            var outer = ProtectedLocalFile.CommitIfVerified(path, first, first, bytes =>
            {
                if (!nested)
                {
                    nested = true;
                    var other = ProtectedLocalFile.CommitIfVerified(path, second, second, b => b, 202);
                    Assert.AreEqual(LocalCommitOutcome.Verified, other, "the second writer must not clobber the first writer's temp file");
                }

                return bytes;
            }, 201);

            Assert.AreEqual(LocalCommitOutcome.Verified, outer);
            Assert.AreEqual("FIRST", File.ReadAllText(path), "the interleaved writer must keep its own file");
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 201)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.HeldPath(path, 201)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.WritingPath(path, 202)));
            Assert.IsFalse(File.Exists(ProtectedLocalFile.HeldPath(path, 202)));
        }

        [Test]
        public void VerifyMismatch_IsNotReportedAsAnIoFailure()
        {
            var path = Path.Combine(NewFolder(), "memory.json");
            File.WriteAllText(path, "ORIGINAL");
            var bytes = Encoding.UTF8.GetBytes("NEXT");

            var outcome = ProtectedLocalFile.CommitIfVerified(path, bytes, bytes, _ => Encoding.UTF8.GetBytes("WRONG"), 105);

            Assert.AreEqual(LocalCommitOutcome.VerifyMismatch, outcome);
            Assert.AreEqual("ORIGINAL", File.ReadAllText(path));
        }

        static byte[] Envelope(string path, byte[] key, string text)
        {
            return LocalAesGcm.Seal(key, null, ProtectedLocalFile.AssociatedData(path), Encoding.UTF8.GetBytes(text));
        }

        string NewFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "leia-save-guard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _cleanup.Add(folder);
            return folder;
        }
    }
}
