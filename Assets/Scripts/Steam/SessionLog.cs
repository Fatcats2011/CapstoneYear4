using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// The rules of the opt-in session logs (SessionLogWriter writes them): only with the -sessionlog launch option, one
/// file per launch in the logs folder, the newest KEEP kept. They hold every problem (with its stack) and every online
/// line (sessions, joins and leaves, ends and their reasons, load times). See docs/steam/in-game-features.md
/// </summary>
public static class SessionLog
{
    /// <summary>The launch option that turns the logs on (Steam: Properties, Launch Options)</summary>
    public const string OPT_IN = "-sessionlog";

    /// <summary>How many session files stay in the folder, this launch's included</summary>
    public const int KEEP = 10;

    /// <summary>A session's file stops growing here (a flood of errors mustn't fill the disk)</summary>
    public const long MAX_BYTES = 5 * 1024 * 1024;

    /// <summary>The line written when a file is full; nothing follows it</summary>
    public const string FULL_LINE = "Session log: 5 MB reached, logging stopped.\n";

    /// <summary>
    /// Whether a session's file has reached MAX_BYTES
    /// </summary>
    public static bool Full(long written)
    {
        return written >= MAX_BYTES;
    }

    const string ONLINE = "Online:";
    const string PERF = "Perf:"; // the frame rate, every 30 s (PerfOverlay)
    const string PREFIX = "session-";
    const string EXTENSION = ".log";

    /// <summary>
    /// Whether the game was launched with the logs on
    /// </summary>
    public static bool OptedIn(string[] args)
    {
        return args != null && args.Any(arg => string.Equals(arg, OPT_IN, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a log message goes in the file: every problem, and the online lines
    /// </summary>
    public static bool Keeps(LogType type, string message)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            return true;

        return message != null && (message.StartsWith(ONLINE, StringComparison.Ordinal) || message.StartsWith(PERF, StringComparison.Ordinal));
    }

    /// <summary>
    /// A launch's file name, which sorts by its start time
    /// </summary>
    public static string FileName(DateTime start)
    {
        return PREFIX + start.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + EXTENSION;
    }

    /// <summary>
    /// A message as the file shows it: its time and type, then a problem's stack, indented
    /// </summary>
    public static string Line(DateTime time, LogType type, string message, string stack)
    {
        StringBuilder line = new StringBuilder();
        line.Append('[').Append(time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append("] ")
            .Append(type).Append(": ").Append(message).Append('\n');

        if (type != LogType.Log && type != LogType.Warning && !string.IsNullOrEmpty(stack))
        {
            foreach (string frame in stack.Split('\n'))
            {
                string trimmed = frame.TrimEnd('\r');
                if (trimmed.Length > 0)
                    line.Append("  ").Append(trimmed).Append('\n');
            }
        }
        return line.ToString();
    }

    /// <summary>
    /// The session files to delete before a new launch's, so the newest keep - 1 stay beside it. Other files stay
    /// </summary>
    public static List<string> ToDelete(IEnumerable<string> fileNames, int keep)
    {
        List<string> sessions = fileNames
            .Where(name => name.StartsWith(PREFIX, StringComparison.Ordinal) && name.EndsWith(EXTENSION, StringComparison.Ordinal))
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToList();

        return sessions.Skip(Math.Max(keep - 1, 0)).ToList();
    }

    /// <summary>
    /// A launch's first line: the build and where it runs
    /// </summary>
    public static string Header(string version, string os, DateTime start)
    {
        return "Dead on Arrival " + version + " on " + os + ", started "
            + start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n";
    }

    /// <summary>
    /// Makes the folder, deletes the oldest sessions, and opens this launch's file, written through on every line. Null
    /// when the folder can't be written (the game then runs without a log)
    /// </summary>
    public static StreamWriter Open(string folder, DateTime start)
    {
        try
        {
            Directory.CreateDirectory(folder);
            foreach (string old in ToDelete(Directory.GetFiles(folder).Select(Path.GetFileName), KEEP))
            {
                try
                {
                    File.Delete(Path.Combine(folder, old));
                }
                catch (IOException)
                {
                    // Open elsewhere: it goes next time
                }
            }

            StreamWriter writer = new StreamWriter(Path.Combine(folder, FileName(start)), false, new UTF8Encoding(false));
            writer.AutoFlush = true;
            return writer;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
