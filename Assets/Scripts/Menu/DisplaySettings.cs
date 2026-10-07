using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the saved display settings: the fullscreen mode and size (DisplayRules), VSync and the frame cap. OptionsMenu
/// calls it at launch and whenever the fullscreen row changes. It does nothing in the editor: the Game view ignores
/// these, and a VSync change there could end up in the project's quality settings. See docs/controls.md
/// </summary>
public static class DisplaySettings
{
    public static void Apply(GameSettings settings)
    {
        if (Application.isEditor)
            return;

        Resolution current = Screen.currentResolution; // the desktop's
        ScreenSize desktop = new ScreenSize(current.width, current.height);

        List<ScreenSize> listed = new List<ScreenSize>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            ScreenSize size = new ScreenSize(resolution.width, resolution.height);
            if (!listed.Contains(size))
                listed.Add(size);
        }

        FullScreenMode mode = DisplayRules.ModeFor(settings.fullscreenPosition, settings.exclusiveFullscreen);
        ScreenSize chosen = DisplayRules.SizeFor(mode, settings.width, settings.height, desktop, listed);

        // Only a real change: each return to the menu applies the settings, and setting exclusive fullscreen again flickers
        if (DisplayRules.NeedsResize(Screen.fullScreenMode, new ScreenSize(Screen.width, Screen.height), mode, chosen))
            Screen.SetResolution(chosen.Width, chosen.Height, mode);
        QualitySettings.vSyncCount = DisplayRules.VSyncCount(settings.vsync);
        Application.targetFrameRate = DisplayRules.TargetFrameRate(settings.frameCap);
    }
}
