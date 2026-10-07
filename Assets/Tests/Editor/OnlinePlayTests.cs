using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What the line along the top of player select says about playing online (OnlinePlay.HintFor)
    /// </summary>
    public class OnlinePlayTests
    {
        [Test]
        public void Hint_PlayerSelect_OfflineWithSteam_OnePlayer_OffersOnlinePlay()
        {
            Assert.AreEqual(OnlinePlay.HINT_PLAY_ONLINE, OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, 1));
        }

        [Test]
        public void Hint_PlayerSelect_OfflineWithSteam_TwoPlayers_OffersNothing()
        {
            Assert.AreEqual("", OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, 2));
        }

        [Test]
        public void Hint_PlayerSelect_WithoutSteam_OffersNothing()
        {
            Assert.AreEqual("", OnlinePlay.HintFor(GameState.PlayerSelect, false, false, false, 1));
        }

        [Test]
        public void Hint_PlayerSelect_InALobby_OffersInvitesAndLeaving()
        {
            Assert.AreEqual(OnlinePlay.HINT_IN_LOBBY, OnlinePlay.HintFor(GameState.PlayerSelect, true, true, true, 1));
        }

        [Test]
        public void Hint_PlayerSelect_OnlineWithoutALobby_OffersLeaving()
        {
            Assert.AreEqual(OnlinePlay.HINT_LEAVE, OnlinePlay.HintFor(GameState.PlayerSelect, true, false, false, 1));
        }

        [TestCase(GameState.Menu)]
        [TestCase(GameState.MainLoop)]
        public void Hint_OutsidePlayerSelect_OffersNothing(GameState state)
        {
            Assert.AreEqual("", OnlinePlay.HintFor(state, true, true, true, 1));
        }

        [Test]
        public void UseDirect_Twice_TheFirstLobbyHearsNothingMore()
        {
            GameObject holder = new GameObject("Online Play");
            try
            {
                OnlinePlay play = holder.AddComponent<OnlinePlay>();
                FakeLobbyService lobbies = new FakeLobbyService();
                play.UseDirect(lobbies, "127.0.0.1", 7777);
                OnlineLobby first = play.Lobby;

                play.UseDirect(lobbies, "127.0.0.1", 7777);

                Assert.AreNotSame(first, play.Lobby);
                Assert.AreEqual(0, Reflect.HandlerCount(lobbies, nameof(FakeLobbyService.Created), first), "Created");
                Assert.AreEqual(0, Reflect.HandlerCount(lobbies, nameof(FakeLobbyService.Entered), first), "Entered");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
