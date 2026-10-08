using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Playing online from the menus (OnlinePlay): the line along the top of player select, what Y does, and Online Play
    /// on the title screen, which goes back to it when it can't get online
    /// </summary>
    public class OnlinePlayTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameManager.instance = null;
            objects.DestroyAll();
        }

        // Player select, with an OnlinePlay over these lobbies
        OnlinePlay InPlayerSelect(FakeLobbyService lobbies, out GameManager game)
        {
            game = objects.Add<GameManager>();
            GameManager.instance = game;
            game.SetGameState(GameState.PlayerSelect);

            OnlinePlay play = objects.Add<OnlinePlay>();
            play.UseDirect(lobbies, "127.0.0.1", 7777);
            return play;
        }

        [Test]
        public void Hint_PlayerSelect_OfflineWithSteam_OnePlayer_OffersOnlinePlay()
        {
            Assert.AreEqual(OnlinePlay.HINT_PLAY_ONLINE, OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, false));
        }

        [Test]
        public void Hint_PlayerSelect_OfflineWithSteam_TwoPlayers_OffersOnlinePlayToo()
        {
            // Phase 3J: several players on one machine can play online
            Assert.AreEqual(OnlinePlay.HINT_PLAY_ONLINE, OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, false));
        }

        [Test]
        public void Hint_PlayerSelect_WithoutSteam_OffersNothing()
        {
            Assert.AreEqual("", OnlinePlay.HintFor(GameState.PlayerSelect, false, false, false, false));
        }

        [Test]
        public void Hint_PlayerSelect_InALobby_OffersInvitesAndLeaving()
        {
            Assert.AreEqual(OnlinePlay.HINT_IN_LOBBY, OnlinePlay.HintFor(GameState.PlayerSelect, true, true, true, false));
        }

        [Test]
        public void Hint_PlayerSelect_OnlineWithoutALobby_OffersLeaving()
        {
            Assert.AreEqual(OnlinePlay.HINT_LEAVE, OnlinePlay.HintFor(GameState.PlayerSelect, true, false, false, false));
        }

        [Test]
        public void Hint_WithTheOnlineEntry_OfflinePlayerSelect_OffersNothing()
        {
            // Local Play: going online is the title screen's Online Play
            Assert.AreEqual("", OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, true));
            Assert.AreEqual(OnlinePlay.HINT_IN_LOBBY, OnlinePlay.HintFor(GameState.PlayerSelect, true, true, true, true));
        }

        [TestCase(true, false, OnlinePlay.YPress.Nothing)]   // Local Play
        [TestCase(true, true, OnlinePlay.YPress.Invite)]     // Online Play
        [TestCase(false, false, OnlinePlay.YPress.Host)]     // the old 4-entry title screen
        [TestCase(false, true, OnlinePlay.YPress.Invite)]
        public void YAction_FollowsTheMenuAndTheRole(bool hasOnlineEntry, bool online, OnlinePlay.YPress expected)
        {
            Assert.AreEqual(expected, OnlinePlay.YAction(hasOnlineEntry, online));
        }

        [TestCase(GameState.PlayerSelect, false, true)]
        [TestCase(GameState.PlayerSelect, true, false)]   // online already: the session's own flow handles it
        [TestCase(GameState.Options, false, false)]       // moved on: left alone
        [TestCase(GameState.Menu, false, false)]
        public void BackToTitle_OnlyFromOfflinePlayerSelect(GameState state, bool online, bool expected)
        {
            Assert.AreEqual(expected, OnlinePlay.BackToTitle(state, online));
        }

        [Test]
        public void StartOnline_Fails_GoesBackToTheTitleScreen()
        {
            OnlinePlay play = InPlayerSelect(new FakeLobbyService { Available = false }, out GameManager game);

            play.StartOnline();

            Assert.AreEqual(GameState.Menu, game.MainState);
        }

        [Test]
        public void Y_OnTheOldTitleScreen_FailingToGetOnline_StaysInPlayerSelect()
        {
            // Before the Online Play entry is in the scene, Y goes online as it always did: a failure only says why
            OnlinePlay play = InPlayerSelect(new FakeLobbyService { Available = false }, out GameManager game);

            play.PressY(false);

            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
        }

        [Test]
        public void Y_OnTheOldTitleScreen_AfterOnlinePlayWasPicked_StillStaysInPlayerSelect()
        {
            FakeLobbyService lobbies = new FakeLobbyService();
            OnlinePlay play = InPlayerSelect(lobbies, out GameManager game);
            play.StartOnline();
            lobbies.RaiseCreated(0);
            game.SetGameState(GameState.PlayerSelect);

            lobbies.Available = false;
            play.PressY(false);

            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
        }

        [Test]
        public void StartOnline_AsksSteamForALobby()
        {
            // The lobby isn't answered: a made lobby would host a real session. OnlineLobbyTests covers that
            FakeLobbyService lobbies = new FakeLobbyService();
            OnlinePlay play = InPlayerSelect(lobbies, out GameManager game);

            play.StartOnline();

            Assert.AreEqual(1, lobbies.Creates.Count);
            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
        }

        [TestCase(GameState.Menu)]
        [TestCase(GameState.MainLoop)]
        public void Hint_OutsidePlayerSelect_OffersNothing(GameState state)
        {
            Assert.AreEqual("", OnlinePlay.HintFor(state, true, true, true, false));
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
                Assert.AreEqual(0, Reflect.HandlerCount(first, nameof(OnlineLobby.HostFailed), play), "HostFailed");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
