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

    // Display (rows on the Options pages' DISPLAY page). See docs/controls.md
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

    // The Options pages' rows (docs/superpowers/specs/2026-10-24-options-pages-design.md)
    public const int MAX_MASTER_POSITION = 10;

    /// <summary>Overall volume in tenths (AudioVolume)</summary>
    public int masterPosition = MAX_MASTER_POSITION;
    /// <summary>1: silent while the window isn't focused; 0: keeps playing (as the game always has)</summary>
    public int backgroundMute = 0;
    /// <summary>How two players share the screen (SplitLayout)</summary>
    public int splitLayout = (int)SplitLayout.Boxed;
    /// <summary>1: no boost FOV kick and no speed lines (Comfort)</summary>
    public int reduceMotion = 0;

    // The match rules (HouseRules): normal is today's game
    /// <summary>MatchLength: 0 short, 1 normal, 2 long</summary>
    public int matchLength = (int)MatchLength.Normal;
    /// <summary>1: boosting into a rival steals</summary>
    public int steals = 1;
    /// <summary>1: the match ends with the golden round</summary>
    public int goldenOrder = 1;

    /// <summary>The language picked on Options → LANGUAGE (a locale code); "" until picked: Steam's then (LanguageRules)</summary>
    public string language = "";

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
            + $"exclusive={exclusiveFullscreen}\nwidth={width}\nheight={height}\nvsync={vsync}\nframecap={frameCap}\n"
            + $"master={masterPosition}\nbgmute={backgroundMute}\nsplit={splitLayout}\nreducemotion={reduceMotion}\n"
            + $"length={matchLength}\nsteals={steals}\ngolden={goldenOrder}\n"
            + $"language={language}\n";
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
        copy.masterPosition = masterPosition;
        copy.backgroundMute = backgroundMute;
        copy.splitLayout = splitLayout;
        copy.reduceMotion = reduceMotion;
        copy.matchLength = matchLength;
        copy.steals = steals;
        copy.goldenOrder = goldenOrder;
        copy.language = language;
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
            // The one key that isn't a number: kept as written (LanguageRules.Pick checks it)
            if (pair.Length == 2 && pair[0].Trim() == "language")
            {
                settings.language = pair[1].Trim();
                continue;
            }
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
                case "master":
                    settings.masterPosition = Mathf.Clamp(value, 0, MAX_MASTER_POSITION);
                    break;
                case "bgmute":
                    settings.backgroundMute = Mathf.Clamp(value, 0, 1);
                    break;
                case "split":
                    settings.splitLayout = Mathf.Clamp(value, 0, (int)SplitLayout.SideBySide);
                    break;
                case "reducemotion":
                    settings.reduceMotion = Mathf.Clamp(value, 0, 1);
                    break;
                case "length":
                    settings.matchLength = Mathf.Clamp(value, 0, (int)MatchLength.Long);
                    break;
                case "steals":
                    settings.steals = Mathf.Clamp(value, 0, 1);
                    break;
                case "golden":
                    settings.goldenOrder = Mathf.Clamp(value, 0, 1);
                    break;
            }
        }

        return settings;
    }
}
