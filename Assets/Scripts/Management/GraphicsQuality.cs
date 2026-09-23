using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Low / Medium / High graphics. High is the project's URP asset as authored; the lower levels cap anti-aliasing
/// and shadows, which every split-screen camera pays for separately each frame.
/// </summary>
public static class GraphicsQuality
{
    public const int LOW = 0;
    public const int MEDIUM = 1;
    public const int HIGH = 2;
    public const int LEVEL_COUNT = 3;

    public static readonly string[] LevelNames = { "Low", "Medium", "High" };

    /// <summary>
    /// The URP settings a quality level changes
    /// </summary>
    public struct Preset
    {
        public int msaaSamples;
        public float shadowDistance;
        public int shadowCascades;

        public Preset(int msaaSamples, float shadowDistance, int shadowCascades)
        {
            this.msaaSamples = msaaSamples;
            this.shadowDistance = shadowDistance;
            this.shadowCascades = shadowCascades;
        }
    }

    static UniversalRenderPipelineAsset changedAsset;
    static Preset authoredSettings;

    /// <summary>
    /// The level applied last (High until another one is applied)
    /// </summary>
    public static int CurrentLevel { get; private set; } = HIGH;

    ///<summary>
    /// A level's settings, never above what the project's URP asset uses
    ///</summary>
    public static Preset PresetFor(int level, Preset authored)
    {
        switch (level)
        {
            case LOW:
                return new Preset(1, Mathf.Min(authored.shadowDistance, 100f), 1);
            case MEDIUM:
                return new Preset(Mathf.Min(authored.msaaSamples, 4), Mathf.Min(authored.shadowDistance, 200f), Mathf.Min(authored.shadowCascades, 2));
            default:
                return authored;
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
    /// The quality-related settings a URP asset has now
    ///</summary>
    public static Preset Read(UniversalRenderPipelineAsset asset)
    {
        return new Preset(asset.msaaSampleCount, asset.shadowDistance, asset.shadowCascadeCount);
    }

    static void Write(UniversalRenderPipelineAsset asset, Preset preset)
    {
        asset.msaaSampleCount = preset.msaaSamples;
        asset.shadowDistance = preset.shadowDistance;
        asset.shadowCascadeCount = preset.shadowCascades;
    }

    ///<summary>
    /// Applies a level to the URP asset the game renders with
    ///</summary>
    public static void Apply(int level)
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            Apply(level, asset);
    }

    ///<summary>
    /// Applies a level to a URP asset, remembering its authored settings the first time so High can bring them back
    ///</summary>
    public static void Apply(int level, UniversalRenderPipelineAsset asset)
    {
        if (changedAsset != asset)
        {
            Restore();
            authoredSettings = Read(asset);
            changedAsset = asset;

            // In the editor the URP asset is a project file: exiting Play Mode puts it back as authored
            Application.quitting += Restore;
        }

        CurrentLevel = Mathf.Clamp(level, 0, LEVEL_COUNT - 1);
        Write(asset, PresetFor(CurrentLevel, authoredSettings));
    }

    ///<summary>
    /// Puts the changed URP asset back as authored
    ///</summary>
    public static void Restore()
    {
        if (changedAsset != null)
            Write(changedAsset, authoredSettings);

        changedAsset = null;
        CurrentLevel = HIGH;
        Application.quitting -= Restore;
    }
}
