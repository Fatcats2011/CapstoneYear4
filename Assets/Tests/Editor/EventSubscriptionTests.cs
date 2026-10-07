using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    public class EventSubscriptionTests
    {
        static readonly string[] GameStateEvents =
        {
            nameof(GameManager.OnSwapMenu), nameof(GameManager.OnSwapOptions), nameof(GameManager.OnSwapCredits),
            nameof(GameManager.OnSwapPlayerSelect), nameof(GameManager.OnSwapLoading), nameof(GameManager.OnSwapStartingCutscene),
            nameof(GameManager.OnSwapTutorial), nameof(GameManager.OnSwapBegin), nameof(GameManager.OnSwapMainLoop),
            nameof(GameManager.OnSwapGoldenCutscene), nameof(GameManager.OnSwapFinalPackage), nameof(GameManager.OnSwapResults),
            nameof(GameManager.OnSwapAnything),
        };

        readonly TestObjects objects = new TestObjects();
        GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            gameManager = objects.Add<GameManager>();
            GameManager.instance = gameManager;
        }

        [TearDown]
        public void TearDown()
        {
            GameManager.instance = null;
            SceneManager.instance = null;
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

            ball.OnEnable();
            ball.OnDisable();

            Assert.AreEqual(0, GameStateHandlersOf(ball));
        }

        [Test]
        public void SoundManager_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            SoundManager sound = objects.Add<SoundManager>();

            sound.OnEnable();
            sound.OnDisable();

            Assert.AreEqual(0, GameStateHandlersOf(sound));
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesNoHandlers()
        {
            SceneManager scene = objects.Add<SceneManager>();
            SceneManager.instance = scene;
            BallDriving ball = objects.Add<BallDriving>();
            OrderHandler handler = objects.Add<OrderHandler>();
            handler.ball = ball;

            handler.OnEnable();
            handler.OnDisable();

            Assert.AreEqual(0, GameStateHandlersOf(handler));
            Assert.AreEqual(0, Reflect.HandlerCount(scene, nameof(SceneManager.OnReturnToMenu), handler));
            Assert.AreEqual(0, Reflect.HandlerCount(ball, nameof(BallDriving.OnBoostStart), handler));
        }

        [Test]
        public void SkideeSkidoo_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            SkideeSkidoo skids = objects.Add<SkideeSkidoo>();

            skids.OnEnable();
            skids.OnDisable();

            Assert.AreEqual(0, GameStateHandlersOf(skids));
        }
    }
}
