using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DoA.Tests
{
    /// <summary>
    /// The graphics levels (GraphicsQuality): what each one sets on the URP asset and its renderers (anti-aliasing, shadows,
    /// SSAO, render scale), with fewer costly settings for 3-4 split-screen players, never above what the asset was
    /// authored with, and everything put back when Play Mode ends. See docs/performance.md
    /// </summary>
    public class GraphicsQualityTests
    {
        // The project's URP asset today: MSAA 8x, 500 m shadows, 4 cascades, a 4096 shadow map, additional-light shadows
        // and SSAO on, full resolution
        static readonly GraphicsQuality.Preset Authored = new GraphicsQuality.Preset(8, 500f, 4, 4096, true, true, 1f);

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
        public void PresetFor_HighWithTwoPlayers_KeepsTheLookUpTo150m()
        {
            GraphicsQuality.Preset high = GraphicsQuality.PresetFor(GraphicsQuality.HIGH, Authored, 2);

            AssertPreset(8, 150f, 4, high);
            Assert.AreEqual(4096, high.mainShadowResolution);
            Assert.IsTrue(high.ssao);
            Assert.AreEqual(1f, high.renderScale);
            Assert.IsFalse(high.additionalShadows, "the extra lights in the scenes are baked");
        }

        [Test]
        public void PresetFor_HighWithFourPlayers_HalvesTheShadowMap()
        {
            GraphicsQuality.Preset high = GraphicsQuality.PresetFor(GraphicsQuality.HIGH, Authored, 4);

            Assert.AreEqual(2048, high.mainShadowResolution);
            Assert.IsTrue(high.ssao);
        }

        [Test]
        public void Medium_CapsAntiAliasingAndShadows()
        {
            AssertPreset(4, 100f, 2, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, Authored));
        }

        [Test]
        public void ApplyFor_FourPlayersOnMedium_TurnsSSAOOff_AndHalvesShadowMaps()
        {
            GraphicsQuality.Preset two = GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, Authored, 2);
            GraphicsQuality.Preset four = GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, Authored, 4);

            Assert.IsTrue(two.ssao, "on at 1-2 players");
            Assert.IsFalse(four.ssao, "off at 3-4 players");
            Assert.AreEqual(2048, four.mainShadowResolution);
            Assert.AreEqual(2048, two.mainShadowResolution);
        }

        [Test]
        public void Low_TurnsOffAntiAliasingAndShortensShadows()
        {
            AssertPreset(1, 60f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, Authored));
        }

        [Test]
        public void PresetFor_Low_ScalesTo80Percent_AndTurnsOffSSAO()
        {
            GraphicsQuality.Preset low = GraphicsQuality.PresetFor(GraphicsQuality.LOW, Authored, 1);

            Assert.AreEqual(0.8f, low.renderScale, 1e-4f);
            Assert.IsFalse(low.ssao);
        }

        [Test]
        public void PresetFor_NeverRaisesWhatTheAssetHas()
        {
            GraphicsQuality.Preset modestAsset = new GraphicsQuality.Preset(2, 80f, 1, 1024, false, false, 0.7f);

            GraphicsQuality.Preset high = GraphicsQuality.PresetFor(GraphicsQuality.HIGH, modestAsset, 1);

            AssertPreset(2, 80f, 1, high);
            Assert.AreEqual(1024, high.mainShadowResolution);
            Assert.IsFalse(high.ssao);
            Assert.AreEqual(0.7f, high.renderScale, 1e-4f);
        }

        [Test]
        public void Presets_NeverRaiseSettingsAboveTheAuthoredAsset()
        {
            GraphicsQuality.Preset modest = new GraphicsQuality.Preset(2, 80f, 1);

            AssertPreset(2, 80f, 1, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, modest));
            AssertPreset(1, 60f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, modest));
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

            AssertPreset(1, 60f, 1, GraphicsQuality.Read(asset));
            Assert.AreEqual(0.8f, asset.renderScale, 1e-4f);
        }

        [Test]
        public void Apply_HighAfterLow_BringsBackTheAuthoredSettings_Within150m()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);
            GraphicsQuality.Apply(GraphicsQuality.HIGH, asset);

            AssertPreset(8, 150f, 4, GraphicsQuality.Read(asset));
            Assert.AreEqual(1f, asset.renderScale, 1e-4f);
        }

        [Test]
        public void ApplyFor_FourPlayers_ChangesTheAssetsShadowMapAndExtraLightShadows()
        {
            FieldInfo mainShadows = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution", BindingFlags.Instance | BindingFlags.NonPublic);
            mainShadows.SetValue(asset, Enum.ToObject(mainShadows.FieldType, 4096));

            GraphicsQuality.Apply(GraphicsQuality.HIGH, asset);
            Assert.AreEqual(4096, asset.mainLightShadowmapResolution, "1 player on High: as authored");

            GraphicsQuality.ApplyForAsset(4, asset);

            Assert.AreEqual(2048, asset.mainLightShadowmapResolution);
            Assert.IsFalse(asset.supportsAdditionalLightShadows);
            LogAssert.NoUnexpectedReceived();
        }

        // A renderer with an SSAO feature (an internal URP type), listed on the test asset
        static ScriptableRendererFeature AddSsaoRenderer(UniversalRenderPipelineAsset target, List<Object> made)
        {
            Type ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", true);
            ScriptableRendererFeature ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            UniversalRendererData data = ScriptableObject.CreateInstance<UniversalRendererData>();
            data.rendererFeatures.Add(ssao);
            made.Add(ssao);
            made.Add(data);
            FieldInfo list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            list.SetValue(target, new ScriptableRendererData[] { data });
            return ssao;
        }

        [Test]
        public void Restore_PutsEveryFeatureAndSettingBack()
        {
            List<Object> made = new List<Object>();
            try
            {
                ScriptableRendererFeature ssao = AddSsaoRenderer(asset, made);
                ssao.SetActive(true);

                GraphicsQuality.Apply(GraphicsQuality.LOW, asset);
                Assert.IsFalse(ssao.isActive, "Low turns SSAO off");

                GraphicsQuality.Restore();

                Assert.IsTrue(ssao.isActive, "SSAO back on");
                AssertPreset(8, 500f, 4, GraphicsQuality.Read(asset));
                Assert.AreEqual(1f, asset.renderScale, 1e-4f);
            }
            finally
            {
                GraphicsQuality.Restore();
                foreach (Object o in made)
                    Object.DestroyImmediate(o);
            }
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
