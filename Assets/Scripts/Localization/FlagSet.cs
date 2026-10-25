using UnityEngine;

/// <summary>
/// The language flags (tools/flag-art.py, Assets/Sprites/UI/Flags): a locale code and its flag, side by side. The
/// game's set is Resources/Flags
/// </summary>
[CreateAssetMenu(menuName = "Dead on Arrival/Flag Set")]
public class FlagSet : ScriptableObject
{
    public const string RESOURCE = "Flags";

    public string[] codes = new string[0];
    public Sprite[] flags = new Sprite[0];

    static FlagSet game;

    public static FlagSet Game => game != null ? game : (game = Resources.Load<FlagSet>(RESOURCE));

    /// <summary>A language's flag, or null</summary>
    public Sprite Of(string code)
    {
        int index = System.Array.IndexOf(codes, code);
        return index >= 0 && index < flags.Length ? flags[index] : null;
    }
}
