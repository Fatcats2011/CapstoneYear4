using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Players' Steam profile pictures (SteamAvatars): Steam hands them over top row first, Unity's textures start at the
    /// bottom; without Steam there's no picture
    /// </summary>
    public class SteamAvatarsTests
    {
        [Test]
        public void FlipRows_PutsTheTopRowAtTheBottom()
        {
            // 1 × 3 RGBA: rows A, B, C from the top
            byte[] steam = { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3 };

            CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1 }, SteamAvatars.FlipRows(steam, 1, 3));
        }

        [Test]
        public void Get_WithoutSteam_IsNull()
        {
            // Steam isn't running in the test runner, and 0 is nobody
            Assert.IsNull(SteamAvatars.Get(76561198000000000UL));
            Assert.IsNull(SteamAvatars.Get(0));
        }
    }
}
