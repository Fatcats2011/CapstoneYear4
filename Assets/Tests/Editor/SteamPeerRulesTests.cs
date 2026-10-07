using NUnit.Framework;
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace DoA.Tests
{
    /// <summary>
    /// Who may connect to a Steam host (only the members of its lobby), which messages the Steam transport delivers (none
    /// empty, none huge), and Steam sessions going through Valve's relay only, so no player sees another's IP. See
    /// docs/online-safety.md
    /// </summary>
    public class SteamPeerRulesTests
    {
        [Test]
        public void SteamPeerRules_OnlyLobbyMembers()
        {
            FakeLobbyService lobbies = new FakeLobbyService();
            lobbies.Members[9] = new System.Collections.Generic.List<ulong> { 111, 222 };

            Assert.IsTrue(SteamPeerRules.Accepts(222, 9, lobbies));
            Assert.IsFalse(SteamPeerRules.Accepts(333, 9, lobbies), "a stranger who knows the host's Steam ID");
        }

        [Test]
        public void SteamPeerRules_NoLobbyOrNoSteam_RefusesEveryone()
        {
            FakeLobbyService lobbies = new FakeLobbyService();
            lobbies.Members[9] = new System.Collections.Generic.List<ulong> { 111 };

            Assert.IsFalse(SteamPeerRules.Accepts(111, 0, lobbies), "no lobby");
            Assert.IsFalse(SteamPeerRules.Accepts(111, 9, null), "no Steam");
            Assert.IsFalse(SteamPeerRules.Accepts(0, 9, lobbies), "nobody");
        }

        [Test]
        public void Deliverable_DropsEmptyAndHugeMessages()
        {
            Assert.IsFalse(SteamPeerRules.Deliverable(0));
            Assert.IsFalse(SteamPeerRules.Deliverable(1), "only the channel byte");
            Assert.IsTrue(SteamPeerRules.Deliverable(2));
            Assert.IsTrue(SteamPeerRules.Deliverable(SteamPeerRules.MAX_MESSAGE));
            Assert.IsFalse(SteamPeerRules.Deliverable(SteamPeerRules.MAX_MESSAGE + 1));
        }

#if !DISABLESTEAMWORKS
        [Test]
        public void SteamRelay_TurnsOffDirectConnections()
        {
            SteamNetworkingConfigValue_t[] options = SteamRelay.Options();

            Assert.AreEqual(1, options.Length);
            Assert.AreEqual(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable, options[0].m_eValue);
            Assert.AreEqual(ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, options[0].m_eDataType);
            Assert.AreEqual(0, options[0].m_val.m_int32, "ICE disabled: relay only");
        }
#endif

        [Test]
        public void SteamLobbyService_WithoutSteam_HasNoMembers()
        {
            Assert.IsFalse(new SteamLobbyService().IsMember(9, 111));
        }
    }
}
