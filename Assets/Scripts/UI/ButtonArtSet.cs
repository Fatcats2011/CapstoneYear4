using UnityEngine;

/// <summary>
/// The confirm (A / cross / Space) and back (B / circle / Backspace) button art, for prompts built by code at runtime
/// (the results screen's rematch vote). Assets/Resources/ResultsButtons holds the game's (MenUI_Buttons and
/// Assets/Sprites/UI/Buttons)
/// </summary>
[CreateAssetMenu(menuName = "Dead on Arrival/Button Art Set")]
public class ButtonArtSet : ScriptableObject
{
    public Sprite confirmXbox, confirmPlayStation, confirmKeyboard;
    public Sprite backXbox, backPlayStation, backKeyboard;

    /// <summary>The results screen's set, or null when it's missing (the prompts show words only)</summary>
    public static ButtonArtSet Results()
    {
        return Resources.Load<ButtonArtSet>("ResultsButtons");
    }
}
