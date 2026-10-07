using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// How many requests a machine may send the host (RateGate): each sender and kind of request has its own allowance,
    /// which refills over time; a sender who keeps flooding is to be disconnected. See docs/online-safety.md
    /// </summary>
    public class RateGateTests
    {
        double now;

        double Clock()
        {
            return now;
        }

        [Test]
        public void Allow_UpToTheBurst_ThenRefills()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 20; i++)
                Assert.IsTrue(gate.Allow(5, RpcKind.Cue), "cue " + i);

            Assert.IsFalse(gate.Allow(5, RpcKind.Cue), "the 21st at once");

            now += 0.1; // 10 a second
            Assert.IsTrue(gate.Allow(5, RpcKind.Cue));
            Assert.IsFalse(gate.Allow(5, RpcKind.Cue));
        }

        [Test]
        public void Allow_EachSenderAndKindHasItsOwnBucket()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 20; i++)
                gate.Allow(5, RpcKind.Cue);

            Assert.IsTrue(gate.Allow(6, RpcKind.Cue), "another sender");
            Assert.IsTrue(gate.Allow(5, RpcKind.Drop), "another kind");
        }

        [Test]
        public void Allow_SlowKindsHaveSmallerAllowances()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(gate.Allow(5, RpcKind.Learnt));

            Assert.IsFalse(gate.Allow(5, RpcKind.Learnt));
        }

        [Test]
        public void ShouldKick_After200DropsIn10Seconds_NotBefore()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 20 + 199; i++)
                gate.Allow(5, RpcKind.Cue);
            Assert.IsFalse(gate.ShouldKick(5), "199 dropped");

            gate.Allow(5, RpcKind.Cue);
            Assert.IsTrue(gate.ShouldKick(5), "200 dropped");
        }

        [Test]
        public void ShouldKick_OldDropsAreForgiven()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 20 + 150; i++)
                gate.Allow(5, RpcKind.Cue);

            now += 11;
            for (int i = 0; i < 20 + 100; i++)
                gate.Allow(5, RpcKind.Cue);

            Assert.IsFalse(gate.ShouldKick(5), "only the last 10 s count");
        }

        [Test]
        public void Forget_StartsTheSenderAfresh()
        {
            RateGate gate = new RateGate(Clock);
            for (int i = 0; i < 20 + 200; i++)
                gate.Allow(5, RpcKind.Cue);

            gate.Forget(5);

            Assert.IsFalse(gate.ShouldKick(5));
            Assert.IsTrue(gate.Allow(5, RpcKind.Cue));
        }
    }
}
