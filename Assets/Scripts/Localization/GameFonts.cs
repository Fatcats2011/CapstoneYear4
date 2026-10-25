using TMPro;
using UnityEngine;

/// <summary>
/// The fonts code needs that don't live in Resources: drawnWords, Jack's Candlestick ("jcandlestick norm"), the font the
/// menu's hand-lettered words were drawn with (DrawnWords). The game's set is Resources/GameFonts
/// </summary>
[CreateAssetMenu(menuName = "Dead on Arrival/Game Fonts")]
public class GameFonts : ScriptableObject
{
    public const string RESOURCE = "GameFonts";

    public TMP_FontAsset drawnWords;

    static GameFonts game;

    public static GameFonts Game => game != null ? game : (game = Resources.Load<GameFonts>(RESOURCE));
}
