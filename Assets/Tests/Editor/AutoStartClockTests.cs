using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// When an open lobby starts by itself (AutoStartClock), in server time: only a machine's first seat restarts the
    /// count, so a stranger's join and leave spam can't put it off (spec Safety 4)
    /// </summary>
    public class AutoStartClockTests
    {
        const ulong HOST = 0;
        const ulong STRANGER = 76561198000000001;
        const ulong FRIEND = 76561198000000002;

        AutoStartClock clock;

        static double StartFrom(double seatedAt)
        {
            return MatchRules.AutoStartAt(seatedAt);
        }

        [SetUp]
        public void SetUp()
        {
            clock = new AutoStartClock();
            clock.Seated(HOST, 0);
        }

        [Test]
        public void HostAlone_NoStart()
        {
            Assert.AreEqual(0.0, clock.StartAt(true, 1, 5));
        }

        [Test]
        public void FriendsOnly_NoStart()
        {
            clock.Seated(STRANGER, 10);
            Assert.AreEqual(0.0, clock.StartAt(false, 2, 10));
        }

        [Test]
        public void FirstSeat_StartsTheCount()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);

            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, 10));
        }

        [Test]
        public void AnotherMachinesFirstSeat_RestartsIt()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);

            clock.Seated(FRIEND, 30);
            Assert.AreEqual(StartFrom(30), clock.StartAt(true, 3, 30));
        }

        [Test]
        public void ReSeat_SameMachine_DoesNotRestart()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);

            // A seat asked for and given back, over and over
            clock.Seated(STRANGER, 20);
            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, 20));
            clock.Seated(STRANGER, 30);
            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, 30));
        }

        [Test]
        public void LeavingAndComingBack_ContinuesFromTheFirstSeat()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);

            Assert.AreEqual(0.0, clock.StartAt(true, 1, 20), "below two machines: no count");
            clock.Seated(STRANGER, 30); // Finds its way back in
            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, 30), "not from now");
        }

        [Test]
        public void ComingBackAfterTheStartPassed_RunsFromNow()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);
            clock.StartAt(true, 1, 20);

            double later = StartFrom(10) + 1;
            clock.Seated(STRANGER, later);
            Assert.AreEqual(StartFrom(later), clock.StartAt(true, 2, later), "a stale start would fire at once");
        }

        [Test]
        public void ACountReachingItsStart_IsNotPushedBack()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);

            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, StartFrom(10)));
            Assert.AreEqual(StartFrom(10), clock.StartAt(true, 2, StartFrom(10) + 3));
        }

        [Test]
        public void FillTurnedOn_CountBeginsFromThen()
        {
            clock.Seated(STRANGER, 10);
            clock.StartAt(false, 2, 10);

            Assert.AreEqual(StartFrom(30), clock.StartAt(true, 2, 30), "not from the seat taken before");
        }

        [Test]
        public void BackInPlayerSelect_CountBeginsFromThen()
        {
            clock.StartAt(true, 1, 5);
            clock.Seated(STRANGER, 10);
            clock.StartAt(true, 2, 10);

            clock.StartAt(false, 2, 20); // the match
            Assert.AreEqual(StartFrom(500), clock.StartAt(true, 2, 500));
        }

        [Test]
        public void Reset_EveryMachineNewAgain()
        {
            clock.Seated(STRANGER, 10);
            clock.Reset();
            clock.Seated(HOST, 0);
            clock.StartAt(true, 1, 1);

            clock.Seated(STRANGER, 3);
            Assert.AreEqual(StartFrom(3), clock.StartAt(true, 2, 3));
        }
    }
}
