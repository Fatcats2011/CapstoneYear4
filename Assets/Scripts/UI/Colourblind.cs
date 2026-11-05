using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Options → Accessibility → Colourblind mode: players are told apart by more than their ghost's colour. Each seat has
/// its own shape badge with its number (P1 circle, P2 triangle, P3 square, P4 diamond), drawn white and black so it
/// reads without colour, over its scooter (ColourblindView) and on the scoreboard; a stolen package gets a "!" badge
/// as well as its purple flame; and in a match each seat's ghost wears a colourblind-safe colour (Okabe-Ito) instead
/// of the one picked. Saved in settings.cfg
/// </summary>
public static class Colourblind
{
    /// <summary>Badge shapes, by seat</summary>
    public enum Shape { Circle, Triangle, Square, Diamond }

    /// <summary>Each seat's ghost colour with the mode on: orange, sky blue, yellow, reddish purple</summary>
    public static readonly Color[] SAFE = {
        new Color(0.902f, 0.624f, 0f),
        new Color(0.337f, 0.706f, 0.914f),
        new Color(0.941f, 0.894f, 0.259f),
        new Color(0.8f, 0.475f, 0.655f),
    };

    internal static readonly Color INK = new Color(0.06f, 0.05f, 0.08f, 1f);

    const int SIZE = 128;
    const float OUTLINE = 0.16f; // the shapes' black edge, in the sprite's -1 to 1

    static bool on;
    static Sprite[] shapes;
    static Sprite alarm;

    /// <summary>Whether the mode is on</summary>
    public static bool On
    {
        get { return on; }
        set
        {
            if (on == value)
                return;
            on = value;
            Changed?.Invoke();
        }
    }

    /// <summary>The mode was turned on or off</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        on = false;
        Changed = null;
    }

    /// <summary>A seat's shape</summary>
    public static Shape ShapeOf(int seat)
    {
        return (Shape)(((seat % 4) + 4) % 4);
    }

    /// <summary>A seat's badge: its shape, white edged in black (its number goes on top)</summary>
    public static Sprite BadgeOf(int seat)
    {
        if (shapes == null)
        {
            shapes = new Sprite[4];
            for (int i = 0; i < shapes.Length; i++)
                shapes[i] = Draw("Badge " + (Shape)i, (u, v) => Distance((Shape)i, u, v));
        }
        return shapes[(int)ShapeOf(seat)];
    }

    /// <summary>The stolen package's badge: a starburst (its "!" goes on top)</summary>
    public static Sprite AlarmBadge()
    {
        if (alarm == null)
            alarm = Draw("Stolen Badge", Star);
        return alarm;
    }

    /// <summary>
    /// How far a point (-1 to 1 each way, up is +v) is inside a shape: negative inside, 0 on its edge. Roughly in the
    /// sprite's units, so the edge is the same thickness all round
    /// </summary>
    internal static float Distance(Shape shape, float u, float v)
    {
        switch (shape)
        {
            case Shape.Circle:
                return Mathf.Sqrt(u * u + v * v) - 0.82f;
            case Shape.Triangle:
                // Point up, its base low: the three edges' distances
                float side = (Mathf.Abs(u) * 0.866f + (v - 0.86f) * 0.5f);
                return Mathf.Max(side, -v - 0.6f);
            case Shape.Square:
                return Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.7f;
            default:
                return (Mathf.Abs(u) + Mathf.Abs(v)) * 0.7071f - 0.66f;
        }
    }

    // A twelve-pointed burst
    static float Star(float u, float v)
    {
        float angle = Mathf.Atan2(v, u);
        float reach = 0.72f + 0.18f * Mathf.Cos(angle * 12f);
        return Mathf.Sqrt(u * u + v * v) - reach;
    }

    /// <summary>A shape's colour at a point: white inside, black on its edge, clear outside</summary>
    internal static Color Pixel(float distance)
    {
        float alpha = Mathf.Clamp01(-distance * SIZE / 2f + 0.5f);
        if (alpha <= 0f)
            return Color.clear;
        Color colour = Color.Lerp(INK, Color.white, Mathf.Clamp01((-distance - OUTLINE) * SIZE / 2f + 0.5f));
        colour.a = alpha;
        return colour;
    }

    static Sprite Draw(string name, Func<float, float, float> distance)
    {
        Color[] pixels = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
                pixels[y * SIZE + x] = Pixel(distance((x + 0.5f) / SIZE * 2f - 1f, (y + 0.5f) / SIZE * 2f - 1f));
        }
        Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }

    /// <summary>Whether a ghost wears its seat's safe colour in a state: in a match and its results, not while picking one</summary>
    internal static bool SafeColoursIn(GameState state)
    {
        return state != GameState.Default && state != GameState.Menu && state != GameState.PlayerSelect;
    }

    static readonly Dictionary<Material, Material[]> safeCopies = new Dictionary<Material, Material[]>();

    /// <summary>A ghost material in a seat's safe colour: a copy, its tints all moved to that colour</summary>
    internal static Material SafeCopy(Material original, int seat)
    {
        seat = ((seat % 4) + 4) % 4;
        if (!safeCopies.TryGetValue(original, out Material[] bySeat))
        {
            bySeat = new Material[4];
            safeCopies[original] = bySeat;
        }
        if (bySeat[seat] == null)
        {
            Color safe = SAFE[seat];
            Material copy = new Material(original) { name = original.name + " (Colourblind " + (seat + 1) + ")" };
            Tint(copy, "_BaseColor", safe);
            Tint(copy, "_MainColor", Color.Lerp(safe, Color.white, 0.3f));
            Tint(copy, "_HalfToneColor", Color.Lerp(safe, Color.white, 0.4f));
            Tint(copy, "_RimColor", safe * 0.8f);
            Tint(copy, "_ShadowColor", safe * 0.7f);
            bySeat[seat] = copy;
        }
        return bySeat[seat];
    }

    /// <summary>Whether a material is one of the safe copies</summary>
    internal static bool IsSafeCopy(Material material)
    {
        if (material == null)
            return false;
        foreach (Material[] bySeat in safeCopies.Values)
        {
            if (Array.IndexOf(bySeat, material) >= 0)
                return true;
        }
        return false;
    }

    // A colour property moved to a colour, keeping its alpha (the shader reads some alphas as strengths)
    internal static void Tint(Material material, string property, Color colour)
    {
        if (!material.HasProperty(property))
            return;
        colour.a = material.GetColor(property).a;
        material.SetColor(property, colour);
    }
}
