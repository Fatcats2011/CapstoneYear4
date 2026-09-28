using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Records every error, exception and failed assert Unity logs while it's listening, so a long test can report
    /// all of them at once instead of stopping at the first
    /// </summary>
    public sealed class LogCollector : IDisposable
    {
        /// <summary>
        /// How Unity Transport reports a closed port: on one computer, when a machine leaves an online session, Windows tells
        /// the machines still running that its port closed, and the transport logs that as an error (docs/online.md)
        /// </summary>
        public const string CLOSED_PORT = "All socket receive requests were marked as failed";

        readonly List<string> problems = new List<string>();
        bool machinesLeaving;

        public LogCollector()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        /// <summary>
        /// Everything that went wrong so far, oldest first: the log type, the message and where it came from
        /// </summary>
        public IReadOnlyList<string> Problems => problems;

        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        /// <summary>
        /// From now on, machines leave the online session: the transport's closed-port report (CLOSED_PORT) isn't a
        /// problem, as the session was ending anyway. Every other problem still is, and this collector reports them, not
        /// the test framework (which can't tell them apart)
        /// </summary>
        public void MachinesLeave()
        {
            machinesLeaving = true;
            LogAssert.ignoreFailingMessages = true;
        }

        void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (machinesLeaving && message.StartsWith(CLOSED_PORT))
                return;

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                problems.Add(type + ": " + message + "\n" + FirstLines(stackTrace, 4));
        }

        static string FirstLines(string text, int count)
        {
            string[] lines = (text ?? "").Split('\n');
            return string.Join("\n", lines, 0, Math.Min(count, lines.Length)).TrimEnd();
        }
    }
}
