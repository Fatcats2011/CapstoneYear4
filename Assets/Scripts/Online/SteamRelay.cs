#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;

/// <summary>
/// The Steam transport's options for every connection: no direct (ICE) connections, so every connection goes through
/// Valve's relay and no player learns another's IP address. OnlineSession sets them before a Steam session starts. See
/// docs/online-safety.md
/// </summary>
public static class SteamRelay
{
    public static SteamNetworkingConfigValue_t[] Options()
    {
        SteamNetworkingConfigValue_t iceOff = new SteamNetworkingConfigValue_t
        {
            m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
            m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
        };
        iceOff.m_val.m_int32 = Steamworks.Constants.k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_Disable; // the game has its own Constants
        return new[] { iceOff };
    }
}
#endif
