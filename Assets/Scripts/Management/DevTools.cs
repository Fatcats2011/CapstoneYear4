using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gate for developer-only features: debug hotkeys and the F6 frame-rate overlay.
/// On in the editor. Off in every build, development builds too, unless the game is launched with -devtools
/// </summary>
public static class DevTools
{
    public const string OPT_IN = "-devtools";

    public static bool Enabled = OnFor(Application.isEditor, Environment.GetCommandLineArgs());

    /// <summary>
    /// Whether developer tools are on: in the editor, or launched with -devtools
    /// </summary>
    public static bool OnFor(bool editor, string[] args)
    {
        return editor || (args != null && args.Any(arg => string.Equals(arg, OPT_IN, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Input.GetKeyDown that only reports presses while developer tools are enabled
    /// </summary>
    public static bool GetKeyDown(KeyCode key)
    {
        return Enabled && Input.GetKeyDown(key);
    }
}
