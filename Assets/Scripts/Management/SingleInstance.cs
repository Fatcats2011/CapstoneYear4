using System;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>
/// One copy of the game at a time: a build launched while another copy runs quits at once. The editor is never
/// stopped (ParrelSync clones run beside it), and -multiinstance lets two builds run on one PC to test online
/// </summary>
public static class SingleInstance
{
    public const string ALLOW = "-multiinstance";
    const string MUTEX_NAME = "Local\\DeadOnArrival.TheBooCrew";

    // Held for as long as the game runs: Windows lets it go when the process ends, however it ends
    static Mutex held;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ClaimOnLaunch()
    {
        if (!Checks(Application.isEditor, Environment.GetCommandLineArgs()))
            return;

        held = TryClaim(MUTEX_NAME);
        if (held == null)
        {
            Debug.Log("Another copy of the game is running: this one quits");
            Application.Quit();
        }
    }

    /// <summary>
    /// Whether this launch must be the only copy: a build, unless launched with -multiinstance
    /// </summary>
    public static bool Checks(bool editor, string[] args)
    {
        return !editor && (args == null || !args.Any(arg => string.Equals(arg, ALLOW, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// The named lock if nobody holds it yet, else null
    /// </summary>
    public static Mutex TryClaim(string name)
    {
        Mutex mutex = new Mutex(true, name, out bool createdNew);
        if (createdNew)
            return mutex;

        mutex.Dispose();
        return null;
    }
}
