using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DoA.Tests
{
    public class GraphicsQualityTests
    {
        // The project's URP asset today: MSAA 8x, 500 m shadows, 4 cascades
        static readonly GraphicsQuality.Preset Authored = new GraphicsQuality.Preset(8, 500f, 4);

        UniversalRenderPipelineAsset asset;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            asset.msaaSampleCount = 8;
            asset.shadowDistance = 500f;
            asset.shadowCascadeCount = 4;
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsQuality.Restore();
            Object.DestroyImmediate(asset);
        }

        static void AssertPreset(int msaa, float shadowDistance, int cascades, GraphicsQuality.Preset actual)
        {
            Assert.AreEqual(msaa, actual.msaaSamples, "MSAA samples");
            Assert.AreEqual(shadowDistance, actual.shadowDistance, "shadow distance");
            Assert.AreEqual(cascades, actual.shadowCascades, "shadow cascades");
        }

        [Test]
        public void High_IsTheUrpAssetAsAuthored()
        {
            AssertPreset(8, 500f, 4, GraphicsQuality.PresetFor(GraphicsQuality.HIGH, Authored));
        }

        [Test]
        public void Medium_CapsAntiAliasingAndShadows()
        {
            AssertPreset(4, 200f, 2, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, Authored));
        }

        [Test]
        public void Low_TurnsOffAntiAliasingAndShortensShadows()
        {
            AssertPreset(1, 100f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, Authored));
        }

        [Test]
        public void Presets_NeverRaiseSettingsAboveTheAuthoredAsset()
        {
            GraphicsQuality.Preset modest = new GraphicsQuality.Preset(2, 80f, 1);

            AssertPreset(2, 80f, 1, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, modest));
            AssertPreset(1, 80f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, modest));
        }

        [Test]
        public void DefaultLevel_SteamDeckStartsOnMedium()
        {
            Assert.AreEqual(GraphicsQuality.MEDIUM, GraphicsQuality.DefaultLevel(isSteamDeck: true));
            Assert.AreEqual(GraphicsQuality.HIGH, GraphicsQuality.DefaultLevel(isSteamDeck: false));
        }

        [Test]
        public void Apply_ChangesTheUrpAsset()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);

            AssertPreset(1, 100f, 1, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Apply_HighAfterLow_BringsBackTheAuthoredSettings()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);
            GraphicsQuality.Apply(GraphicsQuality.HIGH, asset);

            AssertPreset(8, 500f, 4, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Restore_PutsTheUrpAssetBackAsAuthored()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);

            GraphicsQuality.Restore(); // what exiting Play Mode does in the editor

            AssertPreset(8, 500f, 4, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Apply_UnknownLevel_UsesTheNearestOne()
        {
            GraphicsQuality.Apply(7, asset);

            Assert.AreEqual(GraphicsQuality.HIGH, GraphicsQuality.CurrentLevel);
        }
    }
}
