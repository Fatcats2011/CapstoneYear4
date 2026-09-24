using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The scene flow while online: starting a match online waits for Phase 3C (it says so and loads nothing); going
    /// back to the menu uses the local loader; the loading screen never waits for A
    /// </summary>
    public class OnlineSceneFlowTests
    {
        [Test]
        public void Loads_WaitForPhase3C_AndSaySo()
        {
            FakeSceneFlow local = new FakeSceneFlow();
            OnlineSceneFlow flow = new OnlineSceneFlow(local);
            LogAssert.Expect(LogType.Warning, OnlineSceneFlow.NOT_YET);
            LogAssert.Expect(LogType.Warning, OnlineSceneFlow.NOT_YET);

            flow.LoadGameScene();
            flow.LoadFinalOrderScene();

            Assert.AreEqual(0, local.GameLoads + local.FinalOrderLoads, "nothing loaded");
        }

        [Test]
        public void ReturnToMenu_UsesTheLocalLoader_AndItsListeners()
        {
            FakeSceneFlow local = new FakeSceneFlow();
            OnlineSceneFlow flow = new OnlineSceneFlow(local);
            int heard = 0;
            flow.OnReturnToMenu += () => heard++;

            flow.ReturnToMenu();

            Assert.AreEqual(1, local.MenuReturns);
            Assert.AreEqual(1, heard, "listeners of the online flow hear the local loader");
        }

        [Test]
        public void TheLoadingScreen_NeverWaitsForA()
        {
            FakeSceneFlow local = new FakeSceneFlow { WaitingForConfirm = true };
            OnlineSceneFlow flow = new OnlineSceneFlow(local);

            Assert.IsFalse(flow.WaitingForConfirm);
            flow.ConfirmLoad();
            Assert.AreEqual(0, local.Confirms);
        }
    }
}
