using NUnit.Framework;

namespace DoA.Tests
{
    public class SteamStartupTests
    {
        const uint SomeRealAppId = 1234560;

        [Test]
        public void TestAppId_NeverRelaunchesThroughSteam()
        {
            // Relaunching with 480 would start Valve's Spacewar instead of the game
            Assert.IsFalse(SteamStartup.ShouldRestartThroughSteam(SteamStartup.TEST_APP_ID, isEditor: false));
        }

        [Test]
        public void Editor_NeverRelaunchesThroughSteam()
        {
            Assert.IsFalse(SteamStartup.ShouldRestartThroughSteam(SomeRealAppId, isEditor: true));
        }

        [Test]
        public void RealAppIdInABuild_RelaunchesThroughSteam()
        {
            Assert.IsTrue(SteamStartup.ShouldRestartThroughSteam(SomeRealAppId, isEditor: false));
        }

        [Test]
        public void IsSteamDeck_WithoutSteam_IsFalse()
        {
            // No SteamManager runs in Edit Mode, like a player without Steam
            Assert.IsFalse(SteamManager.IsSteamDeck);
        }
    }
}
