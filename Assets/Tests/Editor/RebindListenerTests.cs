using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DoA.Tests
{
    /// <summary>
    /// The Controls panel's ear (RebindListener), with virtual devices: only a fresh press on the player's own device
    /// counts (a button still held from choosing the row doesn't), and two players can wait at once
    /// </summary>
    public class RebindListenerTests
    {
        Gamepad mine, theirs;
        static Gamepad owned;
        static string heard;
        static string heardOther;

        static bool Mine(InputDevice device) { return device == owned; }
        static bool Anyone(InputDevice device) { return true; }
        static void Heard(string path) { heard = path; }
        static void HeardOther(string path) { heardOther = path; }

        [SetUp]
        public void SetUp()
        {
            mine = InputSystem.AddDevice<Gamepad>("Listener Test Pad");
            theirs = InputSystem.AddDevice<Gamepad>("Listener Test Pad 2");
            owned = mine;
            heard = heardOther = null;
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(mine);
            InputSystem.RemoveDevice(theirs);
        }

        static void Send(Gamepad pad, GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            InputSystem.Update();
        }

        [Test]
        public void AButtonStillHeld_DoesntCount_AFreshPressDoes()
        {
            GamepadState aDown = new GamepadState().WithButton(GamepadButton.South);
            Send(mine, aDown); // the A that chose the row
            RebindListener listener = new RebindListener();
            listener.Listen(Mine, Heard);

            aDown.leftStick = new UnityEngine.Vector2(0.3f, 0f); // still held, the stick moves: another state event
            Send(mine, aDown);
            Assert.IsNull(heard, "A was already down");

            Send(mine, new GamepadState().WithButton(GamepadButton.North));
            Assert.AreEqual("<Gamepad>/buttonNorth", heard);
            listener.Stop();
        }

        [Test]
        public void AnotherPlayersPress_DoesntCount()
        {
            RebindListener listener = new RebindListener();
            listener.Listen(Mine, Heard);

            Send(theirs, new GamepadState().WithButton(GamepadButton.North));
            Assert.IsNull(heard);

            Send(mine, new GamepadState().WithButton(GamepadButton.West));
            Assert.AreEqual("<Gamepad>/buttonWest", heard);
            listener.Stop();
        }

        [Test]
        public void TwoPlayersWaiting_EachHearsTheirOwn()
        {
            RebindListener first = new RebindListener();
            RebindListener second = new RebindListener();
            first.Listen(Mine, Heard);
            second.Listen(Theirs, HeardOther);

            Send(mine, new GamepadState().WithButton(GamepadButton.North));
            Send(theirs, new GamepadState().WithButton(GamepadButton.West));

            Assert.AreEqual("<Gamepad>/buttonNorth", heard, "the first wait still listens");
            Assert.AreEqual("<Gamepad>/buttonWest", heardOther);
            first.Stop();
            second.Stop();
        }

        bool Theirs(InputDevice device) { return device == theirs; }

        [Test]
        public void Stopped_HearsNothing()
        {
            RebindListener listener = new RebindListener();
            listener.Listen(Anyone, Heard);
            listener.Stop();

            Send(mine, new GamepadState().WithButton(GamepadButton.North));

            Assert.IsNull(heard);
        }
    }
}
