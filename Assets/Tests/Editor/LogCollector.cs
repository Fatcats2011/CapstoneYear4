using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Records every error, exception and failed assert Unity logs while it's listening, so a long test can report
    /// all of them at once instead of stopping at the first
    /// </summary>
    public sealed class LogCollector : IDisposable
    {
        readonly List<string> problems = new List<string>();

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

        void OnLogMessage(string message, string stackTrace, LogType type)
        {
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
