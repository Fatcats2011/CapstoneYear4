using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    public class PlayerCameraResizerTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<Object> textures = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // Cameras first: Unity refuses to release a texture a camera still draws into
            objects.DestroyAll();
            foreach (Object texture in textures)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
            textures.Clear();
        }

        PlayerCameraResizer CreateResizer(out Camera reference, out Camera follower)
        {
            PlayerCameraResizer resizer = objects.Add<PlayerCameraResizer>();
            reference = objects.Add<Camera>();
            follower = objects.Add<Camera>();
            Reflect.SetField(resizer, "referenceCam", reference);
            Reflect.SetField(resizer, "camerasToFollow", new[] { follower });
            Reflect.SetField(resizer, "viewPortRectDefault", new Vector4(0f, 0f, 1f, 1f));
            return resizer;
        }

        /// <summary>
        /// A resizer whose reference camera draws into a screen-sized texture, so viewport sizes in pixels don't depend on the test machine
        /// </summary>
        PlayerCameraResizer CreateResizerWithPhaseCamera(int screenWidth, int screenHeight, out Camera reference, out Camera phase)
        {
            PlayerCameraResizer resizer = CreateResizer(out reference, out _);
            reference.targetTexture = NewScreen(screenWidth, screenHeight);

            phase = objects.Add<Camera>();
            GameObject phaseRender = objects.NewGameObject("Phase Render");
            phaseRender.AddComponent<Image>();
            Material transition = new Material(Shader.Find("UI/Default"));
            textures.Add(transition);

            Reflect.SetField(resizer, "phaseCamera", phase);
            Reflect.SetField(resizer, "phaseRender", phaseRender);
            Reflect.SetField(resizer, "phaseTransitionMaterial", transition);
            return resizer;
        }

        RenderTexture NewScreen(int width, int height)
        {
            RenderTexture screen = new RenderTexture(width, height, 0);
            textures.Add(screen);
            return screen;
        }

        Vector2Int PhaseTextureSize(Camera phase)
        {
            textures.Add(phase.targetTexture);
            return new Vector2Int(phase.targetTexture.width, phase.targetTexture.height);
        }

        [Test]
        public void FollowerCameras_TakeTheNewViewport_WhenMorePlayersJoin()
        {
            PlayerCameraResizer resizer = CreateResizer(out Camera reference, out Camera follower);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f); // 2 players
            Reflect.Invoke(resizer, "Update");

            reference.rect = new Rect(0f, 0.5f, 0.5f, 0.5f); // a 3rd player joined
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Rect(0f, 0.5f, 0.5f, 0.5f), follower.rect);
        }

        [Test]
        public void FollowerCameras_ReturnToFullScreen_WhenTheOtherPlayersLeave()
        {
            PlayerCameraResizer resizer = CreateResizer(out Camera reference, out Camera follower);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            Reflect.Invoke(resizer, "Update");

            reference.rect = new Rect(0f, 0f, 1f, 1f);
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), follower.rect);
        }

        [Test]
        public void PhaseTexture_FourPlayersAt1080p_IsAQuarterOfTheScreen()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            reference.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(960, 540), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_TwoPlayersOnSteamDeck_KeepsTheScreensShape()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1280, 800, out Camera reference, out Camera phase);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(640, 400), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_ShrinksWhenMorePlayersJoin()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            Reflect.Invoke(resizer, "Start");
            textures.Add(phase.targetTexture);

            reference.rect = new Rect(0f, 0f, 0.5f, 0.5f); // a 3rd and 4th player joined
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Vector2Int(960, 540), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_FollowsTheWindowSize()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            Reflect.Invoke(resizer, "Start");
            textures.Add(phase.targetTexture);

            reference.targetTexture = NewScreen(2560, 1080); // moved to an ultrawide monitor
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Vector2Int(2560, 1080), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_ZeroSizeViewport_IsAtLeastOnePixel()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            reference.rect = new Rect(0f, 0f, 0f, 0f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(1, 1), PhaseTextureSize(phase));
        }
    }
}
