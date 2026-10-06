using System.Globalization;
using UnityEngine;

/// <summary>
/// Values saved by the options menu, stored as plain "key=value" lines so a damaged
/// or outdated file only loses the lines that can't be read.
/// </summary>
public class GameSettings
{
    public const int MAX_VOLUME_POSITION = 11;

    public int bgmPosition;
    public int sfxPosition;
    public int fullscreenPosition;
    public int qualityLevel;

    // Display (no Options rows yet: they wait for art, so these change in settings.cfg). See docs/controls.md
    public const int MIN_WIDTH = 640;
    public const int MIN_HEIGHT = 360;
    public const int MAX_SIZE = 16384;
    public const int MIN_FRAME_CAP = 30;
    public const int MAX_FRAME_CAP = 500;

    /// <summary>1: fullscreen is exclusive; 0: borderless (the fullscreen row picks windowed or fullscreen)</summary>
    public int exclusiveFullscreen = 0;
    /// <summary>The window or screen size; 0 means the desktop's</summary>
    public int width = 0;
    public int height = 0;
    public int vsync = 1;
    /// <summary>Frames per second at most; 0 means no cap</summary>
    public int frameCap = 0;

    public GameSettings(int bgmPosition, int sfxPosition, int fullscreenPosition, int qualityLevel = GraphicsQuality.HIGH)
    {
        this.bgmPosition = bgmPosition;
        this.sfxPosition = sfxPosition;
        this.fullscreenPosition = fullscreenPosition;
        this.qualityLevel = qualityLevel;
    }

    ///<summary>
    /// Converts the settings to the text saved on disk
    ///</summary>
    public string ToText()
    {
        return $"bgm={bgmPosition}\nsfx={sfxPosition}\nfullscreen={fullscreenPosition}\nquality={qualityLevel}\n"
            + $"exclusive={exclusiveFullscreen}\nwidth={width}\nheight={height}\nvsync={vsync}\nframecap={frameCap}\n";
    }

    ///<summary>
    /// A copy with the Options rows' positions replaced, keeping the display keys the menu has no rows for
    ///</summary>
    public GameSettings WithMenuPositions(int bgm, int sfx, int fullscreen, int quality)
    {
        GameSettings copy = Copy();
        copy.bgmPosition = bgm;
        copy.sfxPosition = sfx;
        copy.fullscreenPosition = fullscreen;
        copy.qualityLevel = quality;
        return copy;
    }

    GameSettings Copy()
    {
        GameSettings copy = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, qualityLevel);
        copy.exclusiveFullscreen = exclusiveFullscreen;
        copy.width = width;
        copy.height = height;
        copy.vsync = vsync;
        copy.frameCap = frameCap;
        return copy;
    }

    // 0 stays 0 (the desktop's size); any other size is clamped into its range
    static int ClampSize(int value, int min)
    {
        return value == 0 ? 0 : Mathf.Clamp(value, min, MAX_SIZE);
    }

    // 0 or less is no cap; any other cap is clamped into its range
    static int ClampFrameCap(int value)
    {
        return value <= 0 ? 0 : Mathf.Clamp(value, MIN_FRAME_CAP, MAX_FRAME_CAP);
    }

    ///<summary>
    /// Reads saved settings on top of the defaults. Unreadable lines are skipped and values are
    /// clamped, in case the file was edited or damaged
    ///</summary>
    public static GameSettings Parse(string text, GameSettings defaults)
    {
        GameSettings settings = defaults.Copy();

        foreach (string line in (text ?? "").Split('\n'))
        {
            string[] pair = line.Split('=');
            if (pair.Length != 2 || !int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                continue;

            switch (pair[0].Trim())
            {
                case "bgm":
                    settings.bgmPosition = Mathf.Clamp(value, 0, MAX_VOLUME_POSITION);
                    break;
                case "sfx":
                    settings.sfxPosition = Mathf.Clamp(value, 0, MAX_VOLUME_POSITION);
                    break;
                case "fullscreen":
                    settings.fullscreenPosition = Mathf.Clamp(value, 0, 1);
                    break;
                case "quality":
                    settings.qualityLevel = Mathf.Clamp(value, 0, GraphicsQuality.LEVEL_COUNT - 1);
                    break;
                case "exclusive":
                    settings.exclusiveFullscreen = Mathf.Clamp(value, 0, 1);
                    break;
                case "width":
                    settings.width = ClampSize(value, MIN_WIDTH);
                    break;
                case "height":
                    settings.height = ClampSize(value, MIN_HEIGHT);
                    break;
                case "vsync":
                    settings.vsync = Mathf.Clamp(value, 0, 1);
                    break;
                case "framecap":
                    settings.frameCap = ClampFrameCap(value);
                    break;
            }
        }

        return settings;
    }
}
