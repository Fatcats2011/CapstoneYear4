using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// DoA/Setup/Ghost Emote Layer sets up the ghost's hand clips: GhostEmotes.fbx imported as Generic with no Avatar and
/// one clip per GE_ take (named by its GE_ part, loop off); GhostUpperBody.mask, a transform mask with only the
/// Head, the hands and the fingers on; and the "Emotes" layer of PlayerAnimator.controller
/// (override, that mask, weight 0) with an "Empty" default state and one state per clip but GE_0_RestCheck, Write
/// Defaults off. Running it again sets everything up again, adding nothing twice and dropping states whose clip is
/// gone. The Base Layer is never touched
/// </summary>
public static class GhostEmoteLayerSetup
{
    public const string FOLDER = "Assets/Animations/Character Animations/";
    public const string FBX = FOLDER + "GhostEmotes.fbx";
    public const string MASK = FOLDER + "GhostUpperBody.mask";
    public const string CONTROLLER = FOLDER + "PlayerAnimator.controller";

    public const string LAYER = GhostEmotes.LAYER;
    public const string EMPTY = GhostEmotes.EMPTY;
    public const string PREFIX = "GE_";
    /// <summary>The rest pose, for checking the round trip: a clip, but no state</summary>
    public const string REST_CHECK = "GE_0_RestCheck";

    /// <summary>
    /// The paths the mask leaves on: the bones the clips are for. Blender bakes every bone into every take, the armature
    /// node and the Torso too (at Blender's rest: the armature's offset puts the ghost metres off its scooter), so those
    /// stay off, and the Base Layer keeps the Torso's float. A transform mask goes path by path: a child plays with its
    /// parent off
    /// </summary>
    public static readonly string[] MaskedIn =
    {
        "Armature.001/Torso/Head",
        "Armature.001/Torso/Hand.Left", "Armature.001/Torso/Hand.Left/Finger.Left",
        "Armature.001/Torso/Hand.Right", "Armature.001/Torso/Hand.Right/Finger.Right"
    };

    [MenuItem("DoA/Setup/Ghost Emote Layer")]
    static void Run()
    {
        Build();
    }

    /// <summary>From the command line (-executeMethod GhostEmoteLayerSetup.BuildFromCommandLine)</summary>
    public static void BuildFromCommandLine()
    {
        Build();
    }

    /// <summary>Everything, in order: the import, the mask, the layer</summary>
    public static void Build()
    {
        List<AnimationClip> clips = Import(FBX);
        AvatarMask mask = BuildMask(FBX, MASK);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER);
        if (controller == null)
            throw new InvalidOperationException("Ghost emote layer: no controller at " + CONTROLLER);
        BuildLayer(controller, mask, clips.Where(c => c.name != REST_CHECK));
        AssetDatabase.SaveAssets();
        Debug.Log("Ghost emote layer: " + clips.Count + " clips (" + string.Join(", ", clips.Select(c => c.name))
            + ") into the " + LAYER + " layer of " + CONTROLLER);
    }

    /// <summary>The GE_ part of a take's name ("Armature.001|GE_2a_FistPump" gives "GE_2a_FistPump"; null when none)</summary>
    public static string ClipName(string take)
    {
        if (string.IsNullOrEmpty(take))
            return null;
        string name = take.Substring(take.LastIndexOf('|') + 1);
        int at = name.IndexOf(PREFIX, StringComparison.Ordinal);
        return at >= 0 ? name.Substring(at) : null;
    }

    /// <summary>Sets the FBX's import settings, reimports it, and returns its GE_ clips</summary>
    public static List<AnimationClip> Import(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Ghost emote layer: no model at " + path);

        // Generic with no Avatar: the clips bind by path, as Official_Anim_4.fbx's do
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation = true;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] old = importer.clipAnimations;
        List<ModelImporterClipAnimation> wanted = new List<ModelImporterClipAnimation>();
        foreach (TakeInfo take in importer.importedTakeInfos)
        {
            string name = ClipName(take.name);
            if (name == null || wanted.Any(c => c.name == name))
                continue;
            ModelImporterClipAnimation clip = old.FirstOrDefault(c => c.takeName == take.name)
                ?? new ModelImporterClipAnimation();
            clip.takeName = take.name;
            clip.name = name;
            clip.firstFrame = (float)Math.Round(take.startTime * take.sampleRate);
            clip.lastFrame = (float)Math.Round(take.stopTime * take.sampleRate);
            clip.loopTime = false;
            clip.loop = false;
            clip.loopPose = false;
            clip.wrapMode = WrapMode.Default;
            wanted.Add(clip);
        }
        if (wanted.Count == 0)
            throw new InvalidOperationException("Ghost emote layer: no " + PREFIX + " takes in " + path);
        importer.clipAnimations = wanted.ToArray();
        importer.SaveAndReimport();

        HashSet<string> names = new HashSet<string>(wanted.Select(c => c.name));
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => names.Contains(c.name)).OrderBy(c => c.name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Every transform path in the model under its root ("" for the root itself left out)</summary>
    public static List<string> ModelPaths(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
            throw new InvalidOperationException("Ghost emote layer: no model at " + path);
        return model.GetComponentsInChildren<Transform>(true).Where(t => t != model.transform)
            .Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)).ToList();
    }

    /// <summary>The upper-body mask: every path in the model, only MaskedIn on</summary>
    public static AvatarMask BuildMask(string model, string path)
    {
        List<string> paths = ModelPaths(model);
        foreach (string needed in MaskedIn)
            if (!paths.Contains(needed))
                throw new InvalidOperationException("Ghost emote layer: no " + needed + " in " + model);

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }
        mask.transformCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            mask.SetTransformPath(i, paths[i]);
            mask.SetTransformActive(i, MaskedIn.Contains(paths[i]));
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    /// <summary>Adds or refreshes the Emotes layer: override, the mask, weight 0, Empty first, a state per clip</summary>
    public static void BuildLayer(AnimatorController controller, AvatarMask mask, IEnumerable<AnimationClip> clips)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        int index = Array.FindIndex(layers, l => l.name == LAYER);
        if (index < 0)
        {
            AnimatorStateMachine machine = new AnimatorStateMachine
            {
                name = LAYER,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(machine, controller);
            controller.AddLayer(new AnimatorControllerLayer { name = LAYER, stateMachine = machine });
            layers = controller.layers;
            index = layers.Length - 1;
        }

        AnimatorControllerLayer layer = layers[index];
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.avatarMask = mask;
        layer.defaultWeight = 0f;
        layer.iKPass = false;
        layer.syncedLayerIndex = -1;
        AnimatorStateMachine states = layer.stateMachine;

        AnimatorState empty = State(states, EMPTY, new Vector3(300f, 0f, 0f));
        empty.motion = null;
        empty.writeDefaultValues = false;
        states.defaultState = empty;

        List<AnimationClip> list = clips.ToList();
        HashSet<string> keep = new HashSet<string>(list.Select(c => c.name));
        foreach (ChildAnimatorState child in states.states)
            if (child.state.name.StartsWith(PREFIX, StringComparison.Ordinal) && !keep.Contains(child.state.name))
                states.RemoveState(child.state);

        for (int i = 0; i < list.Count; i++)
        {
            AnimatorState state = State(states, list[i].name, new Vector3(300f + 250f * (1 + i / 12), 60f * (1 + i % 12), 0f));
            state.motion = list[i];
            state.writeDefaultValues = false;
            state.speed = 1f;
        }

        controller.layers = layers;
        EditorUtility.SetDirty(states);
        EditorUtility.SetDirty(controller);
    }

    // The state of that name, made when missing
    static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 at)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state.name == name)
                return child.state;
        return machine.AddState(name, at);
    }
}
