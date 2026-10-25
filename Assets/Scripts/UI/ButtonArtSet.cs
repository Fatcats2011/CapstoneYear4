using UnityEngine;

/// <summary>
/// The confirm (A / cross / Space), back (B / circle / Backspace) and start (Start / Options / M) button art, for prompts built by code at runtime
/// (the results screen's rematch vote). Assets/Resources/ResultsButtons holds the game's (MenUI_Buttons and
/// Assets/Sprites/UI/Buttons)
/// </summary>
[CreateAssetMenu(menuName = "Dead on Arrival/Button Art Set")]
public class ButtonArtSet : ScriptableObject
{
    public Sprite confirmXbox, confirmPlayStation, confirmKeyboard;
    public Sprite backXbox, backPlayStation, backKeyboard;
    public Sprite startXbox, startPlayStation, startKeyboard; // Start / Options / M: MATCH RULES

    /// <summary>One button's art on each device</summary>
    [System.Serializable]
    public struct Glyph
    {
        public Sprite xbox, playStation, keyboard;
    }

    /// <summary>
    /// The drawn prompts' Xbox buttons (MenUI_Buttons: A, B, X, Y, LB, RB) with their PlayStation and keyboard art, as
    /// ButtonGlyphSetup maps them: the game gives each such Image a ButtonGlyph when it loads (ButtonGlyphs)
    /// </summary>
    public Glyph[] glyphs;

    /// <summary>The results screen's set, or null when it's missing (the prompts show words only)</summary>
    public static ButtonArtSet Results()
    {
        return Resources.Load<ButtonArtSet>("ResultsButtons");
    }
}
