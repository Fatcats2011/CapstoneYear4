using System.Collections;
using NUnit.Framework;

namespace DoA.Tests
{
    public class RumblerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void SuspendedRumble_WithNoGamepad_DoesNothing()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            Assert.DoesNotThrow(() => rumbler.SuspendedRumble(null, 0.1f, 0.2f));
        }

        [Test]
        public void EndSuspension_WithNoGamepad_DoesNothing()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            Assert.DoesNotThrow(() => rumbler.EndSuspension(null));
        }

        [Test]
        public void RumblePulse_WithNoGamepad_FinishesWithoutError()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            IEnumerator pulse = (IEnumerator)Reflect.Invoke(rumbler, "PulseTime", 0f, null, false);

            Assert.DoesNotThrow(() => { while (pulse.MoveNext()) { } });
        }
    }
}
