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
        return $"bgm={bgmPosition}\nsfx={sfxPosition}\nfullscreen={fullscreenPosition}\nquality={qualityLevel}\n";
    }

    ///<summary>
    /// Reads saved settings on top of the defaults. Unreadable lines are skipped and values are
    /// clamped, in case the file was edited or damaged
    ///</summary>
    public static GameSettings Parse(string text, GameSettings defaults)
    {
        GameSettings settings = new GameSettings(defaults.bgmPosition, defaults.sfxPosition, defaults.fullscreenPosition, defaults.qualityLevel);

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
            }
        }

        return settings;
    }
}
