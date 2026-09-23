using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    public class EventSubscriptionTests
    {
        static readonly string[] GameStateEvents =
        {
            "OnSwapMenu", "OnSwapOptions", "OnSwapCredits", "OnSwapPlayerSelect", "OnSwapLoading",
            "OnSwapStartingCutscene", "OnSwapTutorial", "OnSwapBegin", "OnSwapMainLoop",
            "OnSwapGoldenCutscene", "OnSwapFinalPackage", "OnSwapResults", "OnSwapAnything",
        };

        readonly TestObjects objects = new TestObjects();
        GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            gameManager = objects.Add<GameManager>();
            Reflect.SetSingleton(gameManager);
        }

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<GameManager>(null);
            Reflect.SetSingleton<SceneManager>(null);
            objects.DestroyAll();
        }

        int GameStateHandlersOf(object subscriber)
        {
            return GameStateEvents.Sum(e => Reflect.HandlerCount(gameManager, e, subscriber));
        }

        [Test]
        public void BallDriving_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            BallDriving ball = objects.Add<BallDriving>();

            Reflect.Invoke(ball, "OnEnable");
            Reflect.Invoke(ball, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(ball));
        }

        [Test]
        public void SoundManager_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            SoundManager sound = objects.Add<SoundManager>();

            Reflect.Invoke(sound, "OnEnable");
            Reflect.Invoke(sound, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(sound));
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesNoHandlers()
        {
            SceneManager scene = objects.Add<SceneManager>();
            Reflect.SetSingleton(scene);
            BallDriving ball = objects.Add<BallDriving>();
            OrderHandler handler = objects.Add<OrderHandler>();
            Reflect.SetField(handler, "ball", ball);

            Reflect.Invoke(handler, "OnEnable");
            Reflect.Invoke(handler, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(handler));
            Assert.AreEqual(0, Reflect.HandlerCount(scene, "OnReturnToMenu", handler));
            Assert.AreEqual(0, Reflect.HandlerCount(ball, "OnBoostStart", handler));
        }
    }
}
