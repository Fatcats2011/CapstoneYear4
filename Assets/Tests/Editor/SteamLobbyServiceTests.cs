using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Steam's lobbies while Steam isn't running (EditMode: SteamManager only starts in Play Mode). Every call answers at
    /// once, and nothing calls Steamworks, which throws without Steam. With Steam, lobbies are checked on two PCs
    /// (EDITOR-TODO.md)
    /// </summary>
    public class SteamLobbyServiceTests
    {
        [SetUp]
        public void NoSteam()
        {
            Assume.That(!SteamManager.Initialized);
        }

        [Test]
        public void Started_AnInvalidCall_IsNot()
        {
            // Steam refusing at once gives an invalid call: no answer would ever come, so Y would stay blocked
            Assert.IsFalse(SteamLobbyService.Started(Steamworks.SteamAPICall_t.Invalid));
            Assert.IsTrue(SteamLobbyService.Started(new Steamworks.SteamAPICall_t(5)));
        }

        [Test]
        public void WithoutSteam_IsUnavailable()
        {
            Assert.IsFalse(new SteamLobbyService().Available);
        }

        [Test]
        public void WithoutSteam_CreateAnswersAtOnce_WithNoLobby()
        {
            SteamLobbyService lobbies = new SteamLobbyService();
            LobbyRecorder recorder = new LobbyRecorder();
            lobbies.Created += recorder.Created;

            lobbies.Create(4);

            CollectionAssert.AreEqual(new[] { 0UL }, recorder.Lobbies);
        }

        [Test]
        public void WithoutSteam_JoinAnswersAtOnce_NotEntered()
        {
            SteamLobbyService lobbies = new SteamLobbyService();
            LobbyRecorder recorder = new LobbyRecorder();
            lobbies.Entered += recorder.Entered;

            lobbies.Join(7);

            CollectionAssert.AreEqual(new[] { 7UL }, recorder.Lobbies);
            CollectionAssert.AreEqual(new[] { false }, recorder.Answers);
        }

        [Test]
        public void WithoutSteam_TheRestIsSafe()
        {
            SteamLobbyService lobbies = new SteamLobbyService();

            Assert.AreEqual("", lobbies.GetData(7, "game"));
            Assert.AreEqual(0UL, lobbies.Owner(7));
            Assert.IsFalse(lobbies.Invite(7));
            Assert.DoesNotThrow(() => lobbies.Leave(7));
            Assert.DoesNotThrow(() => lobbies.SetData(7, "game", "doa"));
            Assert.DoesNotThrow(() => lobbies.SetJoinable(7, true));
        }

        class LobbyRecorder
        {
            public readonly List<ulong> Lobbies = new List<ulong>();
            public readonly List<bool> Answers = new List<bool>();

            public void Created(ulong lobby)
            {
                Lobbies.Add(lobby);
            }

            public void Entered(ulong lobby, bool entered)
            {
                Lobbies.Add(lobby);
                Answers.Add(entered);
            }
        }
    }
}
