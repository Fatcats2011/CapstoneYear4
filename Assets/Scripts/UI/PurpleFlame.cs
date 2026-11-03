using UnityEngine;

/// <summary>
/// The purple flame round a stolen package on the scoreboard (it's worth more: StealValueRules): a looping flipbook,
/// drawn once in code. Each frame is a ball of fire glowing hottest behind the package (its icon sits on
/// top), its tongues licking up more than down, drifting upwards with the frames
/// </summary>
public static class PurpleFlame
{
    internal const int FRAMES = 12;
    internal const int SIZE = 96;

    /// <summary>The frame's size against its package's</summary>
    internal const float SPREAD = 1.7f;

    /// <summary>How far the flame reaches sideways, in package half-widths (past it, it's clear)</summary>
    internal const float SIDE = 1.25f;

    // The package's half-width in the frame (-1 to 1)
    const float BOX = 1f / SPREAD;

    static readonly Color DEEP = new Color(0.45f, 0.05f, 0.95f, 1f);
    static readonly Color HOT = new Color(0.97f, 0.7f, 1f, 1f);

    static Sprite[] frames;

    /// <summary>The flipbook's frames, made the first time they're asked for</summary>
    public static Sprite[] Frames
    {
        get
        {
            if (frames == null)
            {
                frames = new Sprite[FRAMES];
                for (int f = 0; f < FRAMES; f++)
                {
                    Texture2D texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = "Purple Flame " + f, wrapMode = TextureWrapMode.Clamp };
                    texture.SetPixels(Pixels((float)f / FRAMES));
                    texture.Apply();
                    frames[f] = Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
                }
            }
            return frames;
        }
    }

    /// <summary>One frame's pixels at a point of the loop (0 up to 1)</summary>
    internal static Color[] Pixels(float loop)
    {
        Color[] pixels = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
        {
            for (int x = 0; x < SIZE; x++)
            {
                float u = (x + 0.5f) / SIZE * 2f - 1f;
                float v = (y + 0.5f) / SIZE * 2f - 1f;
                pixels[y * SIZE + x] = At(u, v, loop);
            }
        }
        return pixels;
    }

    /// <summary>The flame's colour at a point (-1 to 1 each way, up is +v) at a point of the loop</summary>
    internal static Color At(float u, float v, float loop)
    {
        // How far outside a round core behind the package, reaching further up than down
        float dy = v > 0f ? v * 0.55f : v * 1.25f;
        float distance = Mathf.Max(Mathf.Sqrt(u * u + dy * dy) - BOX * 0.9f, 0f); // 0 in the core: the flame's hottest

        // Noise drifting upwards; the loop blends one cycle into the next so the last frame meets the first
        float noise = Mathf.Lerp(Noise(u, v, loop), Noise(u, v, loop - 1f), loop);
        float reach = 0.12f + 0.62f * noise * noise + 0.1f * noise;
        // Never past SIDE, narrowing towards the top like a flame's tip, and fading out before the frame's top edge
        float side = SIDE * BOX * (1f - 0.45f * Mathf.Clamp01(v));
        float alpha = Mathf.Clamp01((reach - distance) / 0.05f) * Mathf.Clamp01((side - Mathf.Abs(u)) / 0.2f)
            * Mathf.Clamp01((1f - v) / 0.3f);
        if (alpha <= 0f)
            return Color.clear;

        float heat = Mathf.Clamp01(1f - distance / reach);
        Color colour = Color.Lerp(DEEP, HOT, heat * heat * 0.7f); // purple through, only paling at the core
        colour.a = alpha * (0.75f + 0.25f * heat);
        return colour;
    }

    static float Noise(float u, float v, float time)
    {
        return Mathf.PerlinNoise(u * 3.2f + 7.3f, v * 2.4f - time * 2.2f + 3.1f);
    }
}
