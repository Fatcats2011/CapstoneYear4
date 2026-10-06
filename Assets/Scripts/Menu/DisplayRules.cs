using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A screen or window size in pixels
/// </summary>
public readonly struct ScreenSize : IEquatable<ScreenSize>
{
    public readonly int Width;
    public readonly int Height;

    public ScreenSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(ScreenSize other) { return Width == other.Width && Height == other.Height; }
    public override bool Equals(object obj) { return obj is ScreenSize other && Equals(other); }
    public override int GetHashCode() { return Width * 65536 + Height; }
    public override string ToString() { return Width + "x" + Height; }
}

/// <summary>
/// How the saved display settings (GameSettings) become Unity's: the fullscreen mode, a size this screen can show, VSync
/// and the frame cap. DisplaySettings applies them. See docs/controls.md
/// </summary>
public static class DisplayRules
{
    /// <summary>A window's size when none is saved (or the desktop's, if that's smaller)</summary>
    public static readonly ScreenSize WINDOWED_DEFAULT = new ScreenSize(1280, 720);

    /// <summary>
    /// The fullscreen row (0 windowed, 1 fullscreen) and whether fullscreen is exclusive (1) or borderless (0)
    /// </summary>
    public static FullScreenMode ModeFor(int fullscreenPosition, int exclusive)
    {
        if (fullscreenPosition == 0)
            return FullScreenMode.Windowed;

        return exclusive == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
    }

    /// <summary>
    /// The size to use. Fullscreen: the saved size if this screen lists it, otherwise (or with none saved) the desktop's,
    /// so a game moved to another monitor never opens at a size it can't show. Windowed: the saved size (or
    /// WINDOWED_DEFAULT), shrunk to fit inside the desktop
    /// </summary>
    public static ScreenSize SizeFor(FullScreenMode mode, int width, int height, ScreenSize desktop, IReadOnlyList<ScreenSize> listed)
    {
        bool saved = width > 0 && height > 0;

        if (mode != FullScreenMode.Windowed)
        {
            ScreenSize wanted = new ScreenSize(width, height);
            if (saved && listed != null && Contains(listed, wanted))
                return wanted;
            return desktop;
        }

        ScreenSize window = saved ? new ScreenSize(width, height) : WINDOWED_DEFAULT;
        return new ScreenSize(Mathf.Min(window.Width, desktop.Width), Mathf.Min(window.Height, desktop.Height));
    }

    /// <summary>Unity's vSyncCount: 1 with VSync on, 0 with it off</summary>
    public static int VSyncCount(int vsync)
    {
        return vsync == 1 ? 1 : 0;
    }

    /// <summary>Unity's targetFrameRate: the cap, or -1 for none (Unity ignores it while VSync is on)</summary>
    public static int TargetFrameRate(int frameCap)
    {
        return frameCap > 0 ? frameCap : -1;
    }

    static bool Contains(IReadOnlyList<ScreenSize> sizes, ScreenSize size)
    {
        for (int i = 0; i < sizes.Count; i++)
        {
            if (sizes[i].Equals(size))
                return true;
        }
        return false;
    }
}
