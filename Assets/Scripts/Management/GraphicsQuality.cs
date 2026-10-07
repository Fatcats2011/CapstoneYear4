using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Low / Medium / High graphics, for 1-4 split-screen players: every player's camera pays for anti-aliasing, the shadow
/// map, its cascades, SSAO and the render scale each frame, so the lower levels and the 3-4 player layouts cap them.
/// Nothing is ever raised above what the project's URP asset was authored with, and the asset and its renderers'
/// features are put back when Play Mode ends. See docs/performance.md
/// </summary>
public static class GraphicsQuality
{
    public const int LOW = 0;
    public const int MEDIUM = 1;
    public const int HIGH = 2;
    public const int LEVEL_COUNT = 3;

    public static readonly string[] LevelNames = { "Low", "Medium", "High" };

    /// <summary>The farthest shadows reach on each level, in metres</summary>
    public const float HIGH_SHADOWS = 150f, MEDIUM_SHADOWS = 100f, LOW_SHADOWS = 60f;

    /// <summary>The main shadow map on Medium and Low, and on High with 3-4 players</summary>
    public const int SMALLER_SHADOW_MAP = 2048;

    /// <summary>The render scale on Low</summary>
    public const float LOW_RENDER_SCALE = 0.8f;

    /// <summary>
    /// The URP settings a quality level changes
    /// </summary>
    public struct Preset
    {
        public int msaaSamples;
        public float shadowDistance;
        public int shadowCascades;
        public int mainShadowResolution;
        public bool additionalShadows;
        public bool ssao;
        public float renderScale;

        public Preset(int msaaSamples, float shadowDistance, int shadowCascades)
            : this(msaaSamples, shadowDistance, shadowCascades, 4096, true, true, 1f)
        {
        }

        public Preset(int msaaSamples, float shadowDistance, int shadowCascades, int mainShadowResolution, bool additionalShadows, bool ssao, float renderScale)
        {
            this.msaaSamples = msaaSamples;
            this.shadowDistance = shadowDistance;
            this.shadowCascades = shadowCascades;
            this.mainShadowResolution = mainShadowResolution;
            this.additionalShadows = additionalShadows;
            this.ssao = ssao;
            this.renderScale = renderScale;
        }
    }

    const BindingFlags FIELDS = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo mainShadowField = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution", FIELDS);
    static readonly FieldInfo additionalShadowsField = typeof(UniversalRenderPipelineAsset).GetField("m_AdditionalLightShadowsSupported", FIELDS);
    static readonly FieldInfo rendererListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", FIELDS);
    static bool warnedMissing;

    static UniversalRenderPipelineAsset changedAsset;
    static Preset authoredSettings;
    static readonly Dictionary<ScriptableRendererFeature, bool> authoredSsao = new Dictionary<ScriptableRendererFeature, bool>();

    /// <summary>
    /// The level applied last (High until another one is applied)
    /// </summary>
    public static int CurrentLevel { get; private set; } = HIGH;

    /// <summary>
    /// The players the level was applied for last (1 until a match says otherwise)
    /// </summary>
    public static int CurrentPlayers { get; private set; } = 1;

    ///<summary>
    /// A level's settings for one player, never above what the project's URP asset uses
    ///</summary>
    public static Preset PresetFor(int level, Preset authored)
    {
        return PresetFor(level, authored, 1);
    }

    ///<summary>
    /// A level's settings for this many split-screen players, never above what the project's URP asset uses. Extra-light
    /// shadows are always off: the scenes' extra lights are baked
    ///</summary>
    public static Preset PresetFor(int level, Preset authored, int players)
    {
        bool many = players >= 3;
        switch (level)
        {
            case LOW:
                return new Preset(1, Mathf.Min(authored.shadowDistance, LOW_SHADOWS), 1,
                    Mathf.Min(authored.mainShadowResolution, SMALLER_SHADOW_MAP), false, false,
                    Mathf.Min(authored.renderScale, LOW_RENDER_SCALE));
            case MEDIUM:
                return new Preset(Mathf.Min(authored.msaaSamples, 4), Mathf.Min(authored.shadowDistance, MEDIUM_SHADOWS),
                    Mathf.Min(authored.shadowCascades, 2), Mathf.Min(authored.mainShadowResolution, SMALLER_SHADOW_MAP), false,
                    authored.ssao && !many, authored.renderScale);
            default:
                return new Preset(authored.msaaSamples, Mathf.Min(authored.shadowDistance, HIGH_SHADOWS), authored.shadowCascades,
                    many ? Mathf.Min(authored.mainShadowResolution, SMALLER_SHADOW_MAP) : authored.mainShadowResolution, false,
                    authored.ssao, authored.renderScale);
        }
    }

    ///<summary>
    /// The level used until the player picks one: Medium on a Steam Deck, High everywhere else
    ///</summary>
    public static int DefaultLevel(bool isSteamDeck)
    {
        return isSteamDeck ? MEDIUM : HIGH;
    }

    ///<summary>
    /// The quality-related settings a URP asset has now (SSAO: whether any of its renderers has an active SSAO feature)
    ///</summary>
    public static Preset Read(UniversalRenderPipelineAsset asset)
    {
        int mainShadows = mainShadowField != null ? System.Convert.ToInt32(mainShadowField.GetValue(asset)) : 4096;
        bool additional = additionalShadowsField != null ? (bool)additionalShadowsField.GetValue(asset) : true;
        bool ssao = false;
        foreach (ScriptableRendererFeature feature in SsaoFeatures(asset))
            ssao |= feature.isActive;
        return new Preset(asset.msaaSampleCount, asset.shadowDistance, asset.shadowCascadeCount, mainShadows, additional, ssao, asset.renderScale);
    }

    static void Write(UniversalRenderPipelineAsset asset, Preset preset)
    {
        WriteSettings(asset, preset);
        foreach (ScriptableRendererFeature feature in SsaoFeatures(asset))
            feature.SetActive(preset.ssao && authoredSsao.TryGetValue(feature, out bool wasOn) && wasOn);
    }

    // The asset's own settings (not its renderers' features)
    static void WriteSettings(UniversalRenderPipelineAsset asset, Preset preset)
    {
        asset.msaaSampleCount = preset.msaaSamples;
        asset.shadowDistance = preset.shadowDistance;
        asset.shadowCascadeCount = preset.shadowCascades;
        asset.renderScale = preset.renderScale;

        if (mainShadowField == null || additionalShadowsField == null)
        {
            if (!warnedMissing)
                Debug.LogWarning("Graphics quality: this URP version has no shadow map fields to change; they're left as authored");
            warnedMissing = true;
            return;
        }

        mainShadowField.SetValue(asset, System.Enum.ToObject(mainShadowField.FieldType, preset.mainShadowResolution));
        additionalShadowsField.SetValue(asset, preset.additionalShadows);
    }

    // Every SSAO feature on the asset's renderers (an internal URP type, found by name)
    static IEnumerable<ScriptableRendererFeature> SsaoFeatures(UniversalRenderPipelineAsset asset)
    {
        if (rendererListField == null)
        {
            RendererListMissing();
            yield break;
        }
        if (!(rendererListField.GetValue(asset) is ScriptableRendererData[] renderers))
            yield break;

        foreach (ScriptableRendererData renderer in renderers)
        {
            if (renderer == null)
                continue;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                    yield return feature;
            }
        }
    }

    ///<summary>
    /// Applies a level to the URP asset the game renders with, for the players it was applied for last
    ///</summary>
    public static void Apply(int level)
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            Apply(level, asset);
    }

    ///<summary>
    /// Applies the current level again for this many split-screen players (a player joined or left)
    ///</summary>
    public static void ApplyFor(int players)
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            ApplyForAsset(players, asset);
    }

    ///<summary>
    /// Applies the current level again to a URP asset for this many split-screen players
    ///</summary>
    public static void ApplyForAsset(int players, UniversalRenderPipelineAsset asset)
    {
        if (quit)
            return;

        CurrentPlayers = Mathf.Clamp(players, 1, Constants.MAX_PLAYERS);
        Apply(CurrentLevel, asset);
    }

    ///<summary>
    /// Applies a level to a URP asset, remembering its authored settings the first time so High can bring them back.
    /// Nothing once the game is quitting: the asset was put back, and nothing would put it back again
    ///</summary>
    public static void Apply(int level, UniversalRenderPipelineAsset asset)
    {
        if (quit)
            return;

        if (changedAsset != asset)
        {
            int players = CurrentPlayers; // putting the last asset back forgets them
            Restore();
            CurrentPlayers = players;
            authoredSsao.Clear();
            foreach (ScriptableRendererFeature feature in SsaoFeatures(asset))
                authoredSsao[feature] = feature.isActive;
            authoredSettings = Read(asset);
            changedAsset = asset;

            // In the editor the URP asset and its renderers are project files: exiting Play Mode puts them back as authored
            Application.quitting += OnQuitting;
        }

        CurrentLevel = Mathf.Clamp(level, 0, LEVEL_COUNT - 1);
        Write(asset, PresetFor(CurrentLevel, authoredSettings, CurrentPlayers));
    }

    static bool quit;               // the game is quitting: nothing is applied any more
    static bool warnedRendererList; // the missing renderer list was logged

    // The game (or Play Mode) is ending: the asset goes back as authored, and stays so
    internal static void OnQuitting()
    {
        Restore();
        quit = true;
    }

    // Each launch (and each Play Mode, with domain reload off) starts able to apply
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetForLaunch()
    {
        quit = false;
        warnedRendererList = false;
    }

    // URP renamed or removed the renderer list: SSAO can't be found, so it stays as authored
    internal static void RendererListMissing()
    {
        if (warnedRendererList)
            return;

        warnedRendererList = true;
        Debug.LogWarning("Graphics quality: this URP version has no renderer list; SSAO is left as authored");
    }

    ///<summary>
    /// Puts the changed URP asset and its renderers' features back as authored
    ///</summary>
    public static void Restore()
    {
        if (changedAsset != null)
        {
            WriteSettings(changedAsset, authoredSettings);
            foreach (KeyValuePair<ScriptableRendererFeature, bool> feature in authoredSsao)
            {
                if (feature.Key != null)
                    feature.Key.SetActive(feature.Value);
            }
        }

        changedAsset = null;
        CurrentLevel = HIGH;
        CurrentPlayers = 1;
        Application.quitting -= OnQuitting;
    }
}
