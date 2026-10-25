using UnityEngine;

/// <summary>
/// The drawn accent marks (tools/accent-art.py, Assets/Sprites/UI/Accents), one sprite per AccentMark, and how tall a
/// capital is in the sprites' own units (capUnits), so AccentOverlay sizes a mark against the letter it sits on.
/// The game's set is Resources/AccentMarks
/// </summary>
[CreateAssetMenu(menuName = "Dead on Arrival/Accent Mark Set")]
public class AccentMarkSet : ScriptableObject
{
    public const string RESOURCE = "AccentMarks";

    public Sprite caron, breve, dot, doubleAcute, ring, ogonek, commaBelow, acute, lStroke;

    [Tooltip("A capital's height in the 128-unit squares the marks are drawn in (tools/accent-art.py prints it)")]
    public float capUnits = 179.69f;

    static AccentMarkSet game;

    /// <summary>The game's set, or null when it's missing</summary>
    public static AccentMarkSet Game => game != null ? game : (game = Resources.Load<AccentMarkSet>(RESOURCE));

    public Sprite Of(AccentMark mark)
    {
        switch (mark)
        {
            case AccentMark.Caron: return caron;
            case AccentMark.Breve: return breve;
            case AccentMark.Dot: return dot;
            case AccentMark.DoubleAcute: return doubleAcute;
            case AccentMark.Ring: return ring;
            case AccentMark.Ogonek: return ogonek;
            case AccentMark.CommaBelow: return commaBelow;
            case AccentMark.Acute: return acute;
            default: return lStroke;
        }
    }
}
