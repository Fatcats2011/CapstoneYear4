using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The opt-in session logs (SessionLog): only with the -sessionlog launch option, keeping problems and the online
    /// lines, one file per launch named by its start, the newest 10 kept, and no log at all (not a crash) when the folder
    /// can't be written
    /// </summary>
    public class SessionLogTests
    {
        static readonly DateTime START = new DateTime(2026, 10, 6, 9, 5, 3);
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "doa-sessionlog-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            if (File.Exists(folder))
                File.Delete(folder);
        }

        static string Session(int day)
        {
            return SessionLog.FileName(new DateTime(2026, 9, day, 12, 0, 0));
        }

        [Test]
        public void OptedIn_OnlyWithTheLaunchOption()
        {
            Assert.IsTrue(SessionLog.OptedIn(new[] { "DoA.exe", "-sessionlog" }));
            Assert.IsTrue(SessionLog.OptedIn(new[] { "x", "-SessionLog" }));
            Assert.IsFalse(SessionLog.OptedIn(new string[0]));
            Assert.IsFalse(SessionLog.OptedIn(new[] { "x" }));
            Assert.IsFalse(SessionLog.OptedIn(new[] { "x", "-sessionlogs" }));
        }

        [Test]
        public void Keeps_ProblemsAndOnlineLines()
        {
            Assert.IsTrue(SessionLog.Keeps(LogType.Error, "anything"));
            Assert.IsTrue(SessionLog.Keeps(LogType.Exception, "anything"));
            Assert.IsTrue(SessionLog.Keeps(LogType.Assert, "anything"));
            Assert.IsTrue(SessionLog.Keeps(LogType.Log, "Online: session ended: x"));
            Assert.IsTrue(SessionLog.Keeps(LogType.Warning, "Online: no Steam"));
            Assert.IsFalse(SessionLog.Keeps(LogType.Log, "Loaded"));
            Assert.IsFalse(SessionLog.Keeps(LogType.Warning, "x"));
            Assert.IsFalse(SessionLog.Keeps(LogType.Log, null));
        }

        [Test]
        public void FileName_SortsByTime_InAnyCulture()
        {
            CultureInfo was = Thread.CurrentThread.CurrentCulture;
            try
            {
                Assert.AreEqual("session-20261006-090503.log", SessionLog.FileName(START));
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("session-20261006-090503.log", SessionLog.FileName(START));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = was;
            }
        }

        [Test]
        public void Line_AddsTheStack_ForProblemsOnly()
        {
            Assert.AreEqual("[09:05:03] Exception: boom\n  A()\n  B()\n", SessionLog.Line(START, LogType.Exception, "boom", "A()\nB()\n"));
            Assert.AreEqual("[09:05:03] Log: Online: joined the host\n", SessionLog.Line(START, LogType.Log, "Online: joined the host", "C()\n"));
        }

        [Test]
        public void ToDelete_KeepsTheNewest_AndIgnoresOtherFiles()
        {
            List<string> names = new List<string> { "Player.log" };
            for (int day = 12; day >= 1; day--)
                names.Add(Session(day));

            List<string> deleted = SessionLog.ToDelete(names, 10);

            CollectionAssert.AreEquivalent(new[] { Session(1), Session(2), Session(3) }, deleted,
                "the 9 newest stay, leaving room for this launch's");
        }

        [Test]
        public void Open_WritesAFreshFile_AndPrunes()
        {
            Directory.CreateDirectory(folder);
            for (int day = 1; day <= 10; day++)
                File.WriteAllText(Path.Combine(folder, Session(day)), "old");

            using (StreamWriter writer = SessionLog.Open(folder, START))
                writer.Write("x");

            string[] sessions = Directory.GetFiles(folder, "session-*.log").Select(Path.GetFileName).ToArray();
            Assert.AreEqual(10, sessions.Length);
            CollectionAssert.DoesNotContain(sessions, Session(1), "the oldest went");
            Assert.AreEqual("x", File.ReadAllText(Path.Combine(folder, SessionLog.FileName(START))));
        }

        [Test]
        public void Open_WhenTheFolderCantBeMade_ReturnsNull()
        {
            File.WriteAllText(folder, "a file where the folder's parent should be");

            Assert.IsNull(SessionLog.Open(Path.Combine(folder, "logs"), START));
        }

        [Test]
        public void Keeps_PerfLines()
        {
            Assert.IsTrue(SessionLog.Keeps(LogType.Log, "Perf: 4 players, 118 fps average, 92 fps 1% low, 12 cameras"));
        }

        [Test]
        public void Full_At5MB()
        {
            Assert.IsFalse(SessionLog.Full(SessionLog.MAX_BYTES - 1));
            Assert.IsTrue(SessionLog.Full(SessionLog.MAX_BYTES));
        }

        [Test]
        public void Header_NamesTheBuild()
        {
            Assert.AreEqual("Dead on Arrival 1.0.0 on Windows 11, started 2026-10-06 09:05:03\n",
                SessionLog.Header("1.0.0", "Windows 11", START));
        }
    }
}
