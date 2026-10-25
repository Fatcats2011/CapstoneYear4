using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What a host tells a player it turns away or sends off (HostNotice): sent as a key and its arguments, shown in the
    /// player's own language; a reason without a key (an older build's, Netcode's own) shows as it came; a key that isn't
    /// a known key never shows as one
    /// </summary>
    public class HostNoticeTests
    {
        StringsCsv saved;

        [SetUp]
        public void SetUp()
        {
            saved = Loc.Csv;
            Loc.Csv = StringsCsv.Parse(
                "key,note,en,fr,reviewed\n" +
                "online.full,,That match is full.,Cette partie est complète.,\n" +
                "online.version,,That match is on version {0} and you have {1}.,Version {0} et tu as {1}.,\n");
            Loc.SetLanguage("en");
        }

        [TearDown]
        public void TearDown()
        {
            Loc.SetLanguage("en");
            Loc.Csv = saved;
        }

        [Test]
        public void Notice_ByKey_ShowsInTheClientsLanguage()
        {
            string sent = HostNotice.Encode("online.version", "1.2", "1.1");

            Loc.SetLanguage("fr");

            Assert.AreEqual("Version 1.2 et tu as 1.1.", HostNotice.Shown(sent));
        }

        [Test]
        public void Notice_WithoutKey_ShowsTheText()
        {
            Assert.AreEqual("Disconnected due to host shutting down.", HostNotice.Shown("Disconnected due to host shutting down."));
        }

        [Test]
        public void Notice_AnUnknownOrBadKey_ShowsAsSent()
        {
            Assert.AreEqual("#loc:no.such.key", HostNotice.Shown("#loc:no.such.key"));
            Assert.AreEqual("#loc:<b>x</b>", HostNotice.Shown("#loc:<b>x</b>"));
        }

        [Test]
        public void Notice_TooFewArgs_ShowsAsSent()
        {
            Assert.AreEqual("#loc:online.version|1.2", HostNotice.Shown("#loc:online.version|1.2"));
            Assert.AreEqual("#loc:online.version", HostNotice.Shown("#loc:online.version"));
        }

        [Test]
        public void Notice_AnArgumentCantSplitTheNotice()
        {
            string sent = HostNotice.Encode("online.version", "1|2", "x");

            StringAssert.Contains("1/2", HostNotice.Shown(sent));
        }

        [Test]
        public void EndReason_ShowsTheHostsNotice_InTheClientsLanguage()
        {
            Loc.SetLanguage("fr");

            Assert.AreEqual("Cette partie est complète.", OnlineSession.EndReason(false, HostNotice.Encode("online.full")));
        }

        [Test]
        public void RefusalNotice_IsAKey()
        {
            StringAssert.StartsWith(HostNotice.PREFIX + "online.version|", JoinRules.RefusalNotice("1.2", "1.1", 1, GameState.Menu));
            Assert.AreEqual(HostNotice.Encode("online.full"), JoinRules.RefusalNotice("1.2", "1.2", Constants.MAX_PLAYERS, GameState.Menu));
            Assert.IsNull(JoinRules.RefusalNotice("1.2", "1.2", 1, GameState.Menu));
        }
    }
}
