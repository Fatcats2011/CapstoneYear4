using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace DoA.Tests
{
    public class ReplacementControllerListenerTests
    {
        readonly TestObjects objects = new TestObjects();
        ReplacementControllerListener listener;
        PlayerInputManager joinManager;

        [SetUp]
        public void SetUp()
        {
            listener = new ReplacementControllerListener(pad => { }, device => { });
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                listener.SetListening(false);
            }
            finally
            {
                // Clears PlayerInputManager.instance, so later tests start without one
                if (joinManager != null)
                    Reflect.Invoke(joinManager, "OnDisable");
                joinManager = null;
                objects.DestroyAll();
            }
        }

        PlayerInputManager CreateJoinManager()
        {
            joinManager = objects.Add<PlayerInputManager>();
            Reflect.Invoke(joinManager, "OnEnable"); // becomes PlayerInputManager.instance and allows joining, like the game's
            return joinManager;
        }

        [Test]
        public void Listening_WatchesControllersNobodyIsUsing()
        {
            int before = InputUser.listenForUnpairedDeviceActivity;

            listener.SetListening(true);

            Assert.AreEqual(before + 1, InputUser.listenForUnpairedDeviceActivity);
        }

        [Test]
        public void StartingTwiceThenStopping_LeavesNothingListening()
        {
            int before = InputUser.listenForUnpairedDeviceActivity;

            listener.SetListening(true);
            listener.SetListening(true);
            listener.SetListening(false);

            Assert.AreEqual(before, InputUser.listenForUnpairedDeviceActivity);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public void WhileListening_NewPlayersCannotJoin()
        {
            PlayerInputManager manager = CreateJoinManager();

            listener.SetListening(true);

            Assert.IsFalse(manager.joiningEnabled);
        }

        [Test]
        public void AfterListening_NewPlayersCanJoinAgain()
        {
            PlayerInputManager manager = CreateJoinManager();

            listener.SetListening(true);
            listener.SetListening(false);

            Assert.IsTrue(manager.joiningEnabled);
        }
    }
}
