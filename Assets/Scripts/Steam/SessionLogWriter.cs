using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Writes this launch's session log (SessionLog), when the game was launched with -sessionlog: made once the first
/// scene has loaded, it writes every kept message as it comes, from any thread. Without the option it's never made.
/// See docs/steam/in-game-features.md
/// </summary>
public class SessionLogWriter : MonoBehaviour
{
    readonly object writing = new object();
    StreamWriter writer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (!SessionLog.OptedIn(Environment.GetCommandLineArgs()))
            return;

        GameObject holder = new GameObject(nameof(SessionLogWriter));
        DontDestroyOnLoad(holder);
        holder.AddComponent<SessionLogWriter>();
    }

    void Awake()
    {
        string folder = Path.Combine(Application.persistentDataPath, "logs");
        DateTime start = DateTime.Now;
        writer = SessionLog.Open(folder, start);
        if (writer == null)
        {
            Debug.LogWarning("Session log: couldn't write to " + folder);
            return;
        }

        writer.Write(SessionLog.Header(Application.version, SystemInfo.operatingSystem, start));
        Application.logMessageReceivedThreaded += OnLog;
    }

    void OnLog(string message, string stack, LogType type)
    {
        if (!SessionLog.Keeps(type, message))
            return;

        lock (writing)
        {
            if (writer == null)
                return;

            try
            {
                writer.Write(SessionLog.Line(DateTime.Now, type, message, stack));
            }
            catch (IOException)
            {
                // A full disk: the log stops, the game goes on
            }
        }
    }

    void OnDestroy()
    {
        Application.logMessageReceivedThreaded -= OnLog;
        lock (writing)
        {
            if (writer != null)
                writer.Dispose();
            writer = null;
        }
    }
}
