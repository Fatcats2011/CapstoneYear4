using System;
using UnityEngine;

/// <summary>
/// The results cards' shapes, drawn once in code (as ModeIcons): the medal's disc and the cards' rounded panel, both
/// white, to be tinted
/// </summary>
public static class StatIcons
{
    const int SIZE = 96;
    const int PANEL = 48;       // the panel's picture, in pixels: its corners stay round at any size (9-sliced)
    const float CORNER = 14f;   // the panel's corner radius, in pixels

    static Sprite disc, panel;

    /// <summary>A white disc (the medal behind a finishing place)</summary>
    public static Sprite Disc() => disc != null ? disc : disc = Draw("Medal Disc", (u, v) => Mathf.Sqrt(u * u + v * v) - 0.94f, (u, v) => Color.white);

    /// <summary>A white rounded rectangle whose corners stay round at any size (an Image of type Sliced)</summary>
    public static Sprite Panel()
    {
        if (panel != null)
            return panel;
        Color[] pixels = new Color[PANEL * PANEL];
        float half = PANEL / 2f;
        for (int y = 0; y < PANEL; y++)
        {
            for (int x = 0; x < PANEL; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) - (half - CORNER), dy = Mathf.Abs(y + 0.5f - half) - (half - CORNER);
                float distance = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - CORNER;
                pixels[y * PANEL + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
        }
        Texture2D texture = new Texture2D(PANEL, PANEL, TextureFormat.RGBA32, false) { name = "Card Panel", wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        float border = CORNER + 2f;
        panel = Sprite.Create(texture, new Rect(0, 0, PANEL, PANEL), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        return panel;
    }

    static Sprite Draw(string name, Func<float, float, float> distance, Func<float, float, Color> colour)
    {
        Color[] pixels = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                float u = (x + 0.5f) / SIZE * 2f - 1f, v = (y + 0.5f) / SIZE * 2f - 1f;
                Color shown = colour(u, v);
                shown.a = Mathf.Clamp01(-distance(u, v) * SIZE / 2f + 0.5f);
                pixels[y * SIZE + x] = shown.a <= 0f ? Color.clear : shown;
            }
        }
        Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), SIZE);
    }
}
