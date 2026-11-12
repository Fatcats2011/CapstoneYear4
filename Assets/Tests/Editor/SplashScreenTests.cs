using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoA.Tests
{
    /// <summary>
    /// The boot intro's engine sound: how loud it is for the saved settings (the menu scene, which applies them, hasn't
    /// loaded yet), and the splash scene wired with the clip and its timing against the wiper (TitleScreenBuilder)
    /// </summary>
    public class SplashScreenTests
    {
        [Test]
        public void IntroVolume_NothingSaved_IsModest()
        {
            Assert.AreEqual(SplashScreen.DEFAULT_VOLUME, SplashScreen.IntroVolume(null), 0.0001f);
        }

        [Test]
        public void IntroVolume_FollowsTheSavedMasterAndEffects()
        {
            int top = GameSettings.MAX_VOLUME_POSITION - 1;
            GameSettings saved = new GameSettings(top, top, 0, 0);
            Assert.AreEqual(SplashScreen.FULL_VOLUME, SplashScreen.IntroVolume(saved.ToText()), 0.0001f, "both at the top");

            saved.masterPosition = 0;
            Assert.AreEqual(0f, SplashScreen.IntroVolume(saved.ToText()), 0.0001f, "Master at nothing");

            saved.masterPosition = GameSettings.MAX_MASTER_POSITION;
            saved.sfxPosition = 0;
            Assert.AreEqual(0f, SplashScreen.IntroVolume(saved.ToText()), 0.0001f, "Effects at nothing");

            saved.masterPosition = GameSettings.MAX_MASTER_POSITION / 2;
            saved.sfxPosition = top;
            Assert.AreEqual(SplashScreen.FULL_VOLUME * 0.5f, SplashScreen.IntroVolume(saved.ToText()), 0.0001f, "Master at half");
        }

        [Test]
        public void IntroVolume_ADamagedFile_IsStillAVolume()
        {
            Assert.That(SplashScreen.IntroVolume("not a settings file\n===\nmaster=banana"), Is.InRange(0f, 1f));
            Assert.That(SplashScreen.IntroVolume(""), Is.InRange(0f, 1f));
        }

        [Test]
        public void Scene_HasTheEngineClip_TimedToTheWiper()
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScreenBuilder.SPLASH_SCENE, OpenSceneMode.Additive);
            try
            {
                SplashScreen splash = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SplashScreen>(true)).FirstOrDefault();
                Assert.IsNotNull(splash, "the splash screen");
                Assert.IsNotNull(splash.engineClip, "the engine");
                Assert.That(splash.engineClip.length, Is.InRange(1.5f, 4f), "a short rev, not a loop");

                float start, end;
                TitleScreenBuilder.WiperSweep(out start, out end);
                Assert.Greater(end, start, "the wiper sweeps");
                Assert.AreEqual(start, splash.revAt, 0.01f, "it revs away as the wiper starts");
                Assert.AreEqual(end - start, splash.revTime, 0.01f, "for as long as the sweep");
                Assert.AreEqual(Mathf.Max(0f, start - TitleScreenBuilder.ENGINE_LEAD), splash.engineDelay, 0.01f, "starting a little before");
                Assert.Less(splash.engineDelay, splash.revAt, "the engine is running before it revs");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
