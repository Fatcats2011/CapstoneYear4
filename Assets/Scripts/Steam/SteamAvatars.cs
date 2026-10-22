#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Players' Steam profile pictures (Steamworks.NET), by Steam ID: Dispatch shows each under its player's podium. Steam
/// loads a picture it doesn't have yet in the background, so Get is asked again each frame until it's there; each
/// picture is made into a texture once. Without Steam there are none (nothing calls Steamworks, which throws without it)
/// </summary>
public static class SteamAvatars
{
    // Steam's large avatar is 184 × 184: anything far bigger isn't one
    const int MAX_SIZE = 512;

    static readonly Dictionary<ulong, Texture2D> loaded = new Dictionary<ulong, Texture2D>();
    static readonly HashSet<ulong> asked = new HashSet<ulong>();

    /// <summary>The player's picture, or null while Steam is still loading it, without Steam, or for ID 0</summary>
    public static Texture2D Get(ulong id)
    {
        if (id == 0)
            return null;
        if (loaded.TryGetValue(id, out Texture2D known) && known != null)
            return known;

#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
            return null;

        CSteamID who = new CSteamID(id);
        int image = SteamFriends.GetLargeFriendAvatar(who);
        if (image == -1)
            return null; // being fetched
        if (image == 0)
        {
            // Steam doesn't know this player yet (not a friend): ask once, the picture follows
            if (asked.Add(id))
                SteamFriends.RequestUserInformation(who, false);
            return null;
        }

        if (!SteamUtils.GetImageSize(image, out uint width, out uint height) || width == 0 || height == 0
            || width > MAX_SIZE || height > MAX_SIZE)
            return null;
        byte[] rgba = new byte[width * height * 4];
        if (!SteamUtils.GetImageRGBA(image, rgba, rgba.Length))
            return null;

        Texture2D picture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false) { name = "Steam avatar" };
        picture.LoadRawTextureData(FlipRows(rgba, (int)width, (int)height));
        picture.Apply();
        loaded[id] = picture;
        return picture;
#else
        return null;
#endif
    }

    /// <summary>
    /// Steam's pixels come top row first, a texture's bottom row first: the rows, bottom to top
    /// </summary>
    public static byte[] FlipRows(byte[] rgba, int width, int height)
    {
        int row = width * 4;
        byte[] flipped = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
            System.Buffer.BlockCopy(rgba, y * row, flipped, (height - 1 - y) * row, row);
        return flipped;
    }
}
