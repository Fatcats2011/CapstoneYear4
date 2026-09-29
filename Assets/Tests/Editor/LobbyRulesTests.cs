using NUnit.Framework;

namespace DoA.Tests
{
    public class LobbyRulesTests
    {
        [Test]
        public void Refusal_ThisGameAndBuild_LetsThePlayerIn()
        {
            Assert.IsNull(LobbyRules.Refusal("doa", "1.2", "1.2"));
        }

        [Test]
        public void Refusal_AnotherGamesLobby_IsTurnedDown()
        {
            Assert.AreEqual(LobbyRules.NOT_THIS_GAME, LobbyRules.Refusal("spacewar", "1.2", "1.2"));
        }

        [Test]
        public void Refusal_AnotherBuild_NamesBothVersions_LikeADirectJoin()
        {
            string refusal = LobbyRules.Refusal("doa", "1.1", "1.2");

            StringAssert.Contains("1.1", refusal);
            StringAssert.Contains("1.2", refusal);
            Assert.AreEqual(JoinRules.Refusal("1.1", "1.2", 0, GameState.Menu), refusal);
        }

        [Test]
        public void LobbyToJoin_SteamsConnectArgument_GivesTheLobby()
        {
            Assert.AreEqual(109775241021923456UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby", "109775241021923456" }));
        }

        [Test]
        public void LobbyToJoin_WithoutTheArgument_IsZero()
        {
            Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe" }));
            Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(null));
        }

        [Test]
        public void LobbyToJoin_NoNumberAfterIt_IsZero()
        {
            Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby", "abc" }));
            Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby" }));
        }

        [TestCase(GameState.Menu)]
        [TestCase(GameState.PlayerSelect)]
        [TestCase(GameState.Options)]
        [TestCase(GameState.Credits)]
        public void Busy_InTheMenus_WithOnePlayer_IsFree(GameState state)
        {
            Assert.IsNull(LobbyRules.Busy(state, 1, false));
        }

        [TestCase(GameState.Loading)]
        [TestCase(GameState.MainLoop)]
        [TestCase(GameState.Results)]
        [TestCase(GameState.Paused)]
        public void Busy_InAMatch_SaysFinishItFirst(GameState state)
        {
            Assert.AreEqual(LobbyRules.BUSY, LobbyRules.Busy(state, 1, false));
        }

        [Test]
        public void Busy_NobodyHereYet_IsFree()
        {
            Assert.IsNull(LobbyRules.Busy(GameState.Menu, 0, false));
        }

        [Test]
        public void Busy_TwoPlayersHere_SaysOnePlayerPerMachine()
        {
            Assert.AreEqual(LobbyRules.ONE_PLAYER, LobbyRules.Busy(GameState.PlayerSelect, 2, false));
        }

        [Test]
        public void Busy_AlreadyOnline_SaysLeaveFirst()
        {
            Assert.AreEqual(LobbyRules.ALREADY_ONLINE, LobbyRules.Busy(GameState.MainLoop, 1, true));
        }
    }
}
