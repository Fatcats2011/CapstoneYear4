using System;
using UnityEngine;

/// <summary>
/// The timed modes' little pictures, drawn once in code: the crown, the infected ghost and the red X. On the compass and
/// the scoreboard. Each is gold, red or white shapes in a dark edge, so it reads on anything
/// </summary>
public static class ModeIcons
{
    const int SIZE = 128;
    const float EDGE = 0.1f; // the dark edge, in the picture's -1 to 1
    static readonly Color INK = new Color(0.06f, 0.05f, 0.08f, 1f);
    static readonly Color GOLD = new Color(1f, 0.8f, 0.15f, 1f);
    internal static readonly Color RED = new Color(0.9f, 0.08f, 0.1f, 1f);

    static Sprite crown, infected, cross;

    public static Sprite Crown() => crown != null ? crown : crown = Draw("Crown Icon", CrownDistance, (u, v) => GOLD);

    public static Sprite Infected() => infected != null ? infected : infected = Draw("Infected Icon", GhostDistance, GhostColour);

    public static Sprite Cross() => cross != null ? cross : cross = Draw("Out Cross", CrossDistance, (u, v) => RED);

    /// <summary>How far a point (-1 to 1 each way, up is +v) is inside the crown: negative inside</summary>
    internal static float CrownDistance(float u, float v)
    {
        // A band with three points: the top edge zigzags up from the band
        float zig = Mathf.Abs(Mathf.Repeat((u + 0.8f) / 0.8f, 1f) - 0.5f) * 2f;
        float top = -0.05f + 0.75f * zig;
        return Mathf.Max(Mathf.Abs(u) - 0.8f, Mathf.Max(v - top, -0.65f - v));
    }

    /// <summary>...inside the ghost: a round head over a straight body</summary>
    internal static float GhostDistance(float u, float v)
    {
        float head = Mathf.Sqrt(u * u + (v - 0.15f) * (v - 0.15f)) - 0.65f;
        float body = v < 0.15f ? Mathf.Max(Mathf.Abs(u) - 0.65f, -0.8f - v) : 1f;
        return Mathf.Min(head, body);
    }

    static Color GhostColour(float u, float v)
    {
        // Two white eyes in the red
        float eye = Mathf.Min(Mathf.Sqrt((u - 0.27f) * (u - 0.27f) + (v - 0.2f) * (v - 0.2f)), Mathf.Sqrt((u + 0.27f) * (u + 0.27f) + (v - 0.2f) * (v - 0.2f)));
        return eye < 0.17f ? Color.white : RED;
    }

    /// <summary>...inside the X</summary>
    internal static float CrossDistance(float u, float v)
    {
        float along = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
        float across = Mathf.Abs(Mathf.Abs(u) - Mathf.Abs(v)) * 0.7071f;
        return Mathf.Max(across - 0.2f, along - 0.85f);
    }

    static Sprite Draw(string name, Func<float, float, float> distance, Func<float, float, Color> colour)
    {
        Color[] pixels = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                float u = (x + 0.5f) / SIZE * 2f - 1f, v = (y + 0.5f) / SIZE * 2f - 1f;
                float inside = -distance(u, v);
                float alpha = Mathf.Clamp01((inside + EDGE) * SIZE / 2f + 0.5f); // the edge sits outside the shape
                if (alpha <= 0f)
                {
                    pixels[y * SIZE + x] = Color.clear;
                    continue;
                }
                Color shown = Color.Lerp(INK, colour(u, v), Mathf.Clamp01(inside * SIZE / 2f + 0.5f));
                shown.a = alpha;
                pixels[y * SIZE + x] = shown;
            }
        }
        Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }
}
