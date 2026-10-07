using System;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What friends see in Steam (Rich Presence): the token each game state shows, "solo" for one player, and states that
    /// change nothing. The tokens' texts are docs/steam/rich-presence-english.vdf
    /// </summary>
    public class PresenceRulesTests
    {
        [Test]
        public void For_TheMenus_IsMenus()
        {
            Assert.AreEqual("#Menus", PresenceRules.For(GameState.Menu, 1));
            Assert.AreEqual("#Menus", PresenceRules.For(GameState.Options, 1));
            Assert.AreEqual("#Menus", PresenceRules.For(GameState.Credits, 1));
        }

        [Test]
        public void For_PlayerSelect_IsGettingReady()
        {
            Assert.AreEqual("#GettingReady", PresenceRules.For(GameState.PlayerSelect, 2));
        }

        [Test]
        public void For_TheTutorial_IsTutorial()
        {
            Assert.AreEqual("#Tutorial", PresenceRules.For(GameState.Tutorial, 2));
        }

        [Test]
        public void For_TheOpeningCutscene_KeepsTheLastPresence()
        {
            // Every match opens with the tutorial: "Getting ready" stays until it sets "Learning the ropes"
            Assert.IsNull(PresenceRules.For(GameState.StartingCutscene, 3));
            Assert.IsNull(PresenceRules.For(GameState.StartingCutscene, 1));
        }

        [Test]
        public void For_TheMatch_IsDelivering_OrSoloForOne()
        {
            Assert.AreEqual("#Delivering", PresenceRules.For(GameState.Begin, 3));
            Assert.AreEqual("#Delivering", PresenceRules.For(GameState.MainLoop, 3));
            Assert.AreEqual("#DeliveringSolo", PresenceRules.For(GameState.MainLoop, 1));
            Assert.AreEqual("#DeliveringSolo", PresenceRules.For(GameState.MainLoop, 0));
        }

        [Test]
        public void For_TheGoldenRound_IsGolden_OrSoloForOne()
        {
            Assert.AreEqual("#Golden", PresenceRules.For(GameState.GoldenCutscene, 2));
            Assert.AreEqual("#Golden", PresenceRules.For(GameState.FinalPackage, 2));
            Assert.AreEqual("#GoldenSolo", PresenceRules.For(GameState.GoldenCutscene, 1));
            Assert.AreEqual("#GoldenSolo", PresenceRules.For(GameState.FinalPackage, 1));
        }

        [Test]
        public void For_TheResults_IsResults()
        {
            Assert.AreEqual("#Results", PresenceRules.For(GameState.Results, 4));
        }

        [Test]
        public void For_LoadingPausedOrDefault_ChangesNothing()
        {
            Assert.IsNull(PresenceRules.For(GameState.Loading, 2));
            Assert.IsNull(PresenceRules.For(GameState.Paused, 2));
            Assert.IsNull(PresenceRules.For(GameState.Default, 2));
        }

        [Test]
        public void For_EveryState_IsCovered()
        {
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                string token = PresenceRules.For(state, 2);
                if (state == GameState.Loading || state == GameState.Paused || state == GameState.Default
                    || state == GameState.StartingCutscene)
                    Assert.IsNull(token, state.ToString());
                else
                    StringAssert.StartsWith("#", token, state + " needs a token");
            }
        }

        [Test]
        public void SteamPresence_WithoutSteam_IsUnavailable_AndSetDoesNothing()
        {
            SteamPresence presence = new SteamPresence();

            Assert.IsFalse(presence.Available, "Steam isn't running in batch mode");
            Assert.DoesNotThrow(() => presence.Set(PresenceRules.DISPLAY, "#Menus"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
