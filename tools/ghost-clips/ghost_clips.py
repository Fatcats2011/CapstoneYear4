"""
Ghost hand clips: builds Blender actions from the tables in clips.py, renders review sheets from the in-game
camera angle, and exports them to Unity as one FBX.

Run inside Blender (UI or headless):
    blender -b -P tools/ghost-clips/ghost_clips.py -- [--only NAME,NAME] [--no-export] [--no-render]
or from a live Blender Python console / MCP:
    import sys; sys.path.insert(0, r"<repo>/tools/ghost-clips"); import ghost_clips; ghost_clips.main([...])

Keys are written in the ghost's own frame, relative to the rest pose, so the tables read naturally:
    pos (fwd, left, up) in U, where U = the rest distance from Torso to Head
    rot (roll, pitch, yaw) in degrees about the ghost's forward, left and up axes
    (+pitch tips the bone's front down, +yaw turns it to the ghost's left, +roll leans it to the ghost's right)
The clips only key Head, Hand.Left, Finger.Left, Hand.Right, Finger.Right: the upper-body mask in Unity.
"""
import os
import sys
import math
import importlib

import bpy
import mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SOURCE_FBX = os.path.join(REPO, "Assets", "Animations", "Character Animations", "Official_Anim_4.fbx")
OUT_FBX = os.path.join(REPO, "Assets", "Animations", "Character Animations", "GhostEmotes.fbx")
PREVIEW_DIR = os.path.abspath(os.path.join(REPO, "..", "GhostEmoteCaptures", "blender"))

ARMATURE = "Armature.001"
MESH = "Player_Model"
FPS = 60
MASKED = ("Head", "Hand.Left", "Finger.Left", "Hand.Right", "Finger.Right")

# Unity's in-game camera (Player - Cinemachine.prefab): (0, 6.3, -9.7) from the scooter, FOV 60, aimed ~1.6 m up.
# In Blender the ghost is 3.39x larger (U is 0.313 m in Unity and 1.06 here); the ground sits ~4.35 below the armature origin.
UNITY_TO_BLENDER = 3.39
GROUND_Z = -4.35


def log(*args):
    print("[ghost_clips]", *args)
    sys.stdout.flush()


# ---------------------------------------------------------------- scene

def load_ghost():
    """The ghost from the game's FBX, at rest, with nothing playing. Reuses it if already loaded"""
    arm = bpy.data.objects.get(ARMATURE)
    if arm is None:
        bpy.ops.import_scene.fbx(filepath=SOURCE_FBX)
        arm = bpy.data.objects[ARMATURE]
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    return arm


def frame_axes(arm):
    """The ghost's own frame in armature space and its size U"""
    bones = arm.data.bones
    up = (bones["Head"].head_local - bones["Torso"].head_local)
    unit = up.length
    up.normalize()
    hands = (bones["Hand.Left"].head_local + bones["Hand.Right"].head_local) / 2
    fwd = hands - bones["Tail1"].head_local
    fwd -= up * fwd.dot(up)
    fwd.normalize()
    left = up.cross(fwd)
    if left.dot(bones["Hand.Left"].head_local - bones["Torso"].head_local) < 0:
        left = -left
    return fwd, left, up, unit


# ---------------------------------------------------------------- clips

def _rest_matrix(arm, bone_name):
    return arm.data.bones[bone_name].matrix_local.copy()


def _target(arm, axes, bone_name, pos, rot):
    """Armature-space matrix for a bone moved by pos (fwd, left, up in U) and turned by rot (roll, pitch, yaw)"""
    fwd, left, up, unit = axes
    rest = _rest_matrix(arm, bone_name)
    roll, pitch, yaw = rot
    turn = (mathutils.Matrix.Rotation(math.radians(yaw), 4, up) @
            mathutils.Matrix.Rotation(math.radians(pitch), 4, left) @
            mathutils.Matrix.Rotation(math.radians(-roll), 4, fwd))
    head = rest.to_translation()
    offset = fwd * pos[0] + left * pos[1] + up * pos[2]
    m = turn @ mathutils.Matrix.Translation(-head) @ rest
    m.translation = head + offset * unit
    return m


def _set_pose(arm, axes, pose):
    """pose: {bone: (pos, rot)} for hands/head, {finger: rot} (local bend, degrees about x, y, z)"""
    for pb in arm.pose.bones:
        if pb.name in MASKED:
            pb.location = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    # parents first: Head and hands are siblings under Torso, fingers under hands
    for name in ("Head", "Hand.Left", "Hand.Right"):
        if name in pose:
            pos, rot = pose[name]
            arm.pose.bones[name].matrix = _target(arm, axes, name, pos, rot)
            bpy.context.view_layer.update()
    for name in ("Finger.Left", "Finger.Right"):
        if name in pose:
            x, y, z = pose[name]
            arm.pose.bones[name].rotation_quaternion = mathutils.Euler(
                (math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_quaternion()


def build_action(arm, axes, clip):
    """One Blender action from a clip table: keys on every masked bone at every key time"""
    name = "GE_" + clip["id"] + "_" + clip["name"].replace(" ", "")
    shares = [share for share, _ in clip["keys"]]
    if shares[0] != 0.0 or shares[-1] != 1.0 or any(b <= a for a, b in zip(shares, shares[1:])):
        raise ValueError("%s: key shares must run 0 .. 1 strictly increasing, got %s" % (name, shares))
    if clip["keys"][0][1] or clip["keys"][-1][1]:
        raise ValueError("%s: must start and end at REST" % name)
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data.action = action
    frames = max(1, int(round(clip["length"] * FPS)))
    for share, pose in clip["keys"]:
        frame = 1 + int(round(share * frames))
        _set_pose(arm, axes, pose)
        for bone in MASKED:
            pb = arm.pose.bones[bone]
            pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("rotation_quaternion", frame=frame)
    action.frame_range = (1, 1 + frames)
    arm.animation_data.action = None
    return action


# ---------------------------------------------------------------- preview

def _camera(name, location, target, fov_deg):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        bpy.context.scene.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle = math.radians(fov_deg)
    cam.location = location
    cam.rotation_euler = (target - location).to_track_quat('-Z', 'Y').to_euler()
    return cam


def _views(arm, axes):
    """Behind (the in-game camera, zoomed in like the Unity capture's behindzoom) and front three-quarter"""
    fwd, left, up, unit = axes
    world = arm.matrix_world
    s = UNITY_TO_BLENDER
    ground = mathutils.Vector((0, 0, GROUND_Z))
    aim = world @ (ground + up * 1.6 * s)
    behind = world @ (ground - fwd * 9.7 * s + up * 6.3 * s)
    front = world @ (ground + fwd * 6.0 * s + left * 6.0 * s + up * 4.5 * s)
    return {"behind": (behind, aim, 18.0), "front3q": (front, aim, 22.0)}


def render_sheet(arm, axes, action, clip, shares=(0.0, 0.1, 0.3, 0.5, 0.7, 0.9)):
    """One PNG per view: the clip at each share, side by side"""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.render.resolution_x = 320
    scene.render.resolution_y = 240
    scene.render.film_transparent = False
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    frames = action.frame_range[1] - action.frame_range[0]
    paths = []
    for view, (loc, aim, fov) in _views(arm, axes).items():
        scene.camera = _camera("GhostClipCam_" + view, loc, aim, fov)
        tiles = []
        for k, share in enumerate(shares):
            arm.animation_data.action = action if share > 0 else None
            if share == 0:
                load_ghost()
            scene.frame_set(int(round(action.frame_range[0] + share * frames)))
            tile = os.path.join(PREVIEW_DIR, "_tile%d.png" % k)
            scene.render.filepath = tile
            bpy.ops.render.render(write_still=True)
            tiles.append(tile)
        out = os.path.join(PREVIEW_DIR, "%s_%s_%s.png" % (clip["id"], clip["name"].replace(" ", ""), view))
        _stitch(tiles, out)
        paths.append(out)
    arm.animation_data.action = None
    return paths


def _stitch(tiles, out):
    images = [bpy.data.images.load(t, check_existing=False) for t in tiles]
    w, h = images[0].size
    sheet = bpy.data.images.new("ghost_sheet", w * len(images), h, alpha=False)
    pixels = [0.0] * (w * len(images) * h * 4)
    for i, img in enumerate(images):
        src = list(img.pixels)
        for y in range(h):
            row = src[y * w * 4:(y + 1) * w * 4]
            start = (y * w * len(images) + i * w) * 4
            pixels[start:start + w * 4] = row
    sheet.pixels = pixels
    sheet.filepath_raw = out
    sheet.file_format = 'PNG'
    sheet.save()
    for img in images:
        bpy.data.images.remove(img)
    bpy.data.images.remove(sheet)
    for t in tiles:
        os.remove(t)


# ---------------------------------------------------------------- export

def export_fbx(arm, actions):
    """Exports the ghost (armature + mesh, two roots like Official_Anim_4.fbx) with only our actions as takes"""
    keep = set(a.name for a in actions)
    hidden = []
    for a in bpy.data.actions:
        if a.name not in keep:
            hidden.append((a, a.use_fake_user))
    # the exporter takes every action when bake_anim_use_all_actions is on: park the others in a list and remove them
    stash = {}
    for a, fake in hidden:
        stash[a.name] = fake
        a.name = "_stash_" + a.name
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.data.objects[MESH].select_set(True)
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.export_scene.fbx(
            filepath=OUT_FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
            add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
            bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
            bake_anim_simplify_factor=0.0)
    finally:
        for a in bpy.data.actions:
            if a.name.startswith("_stash_"):
                a.name = a.name[len("_stash_"):]
    log("exported", OUT_FBX, "takes:", sorted(keep))


# ---------------------------------------------------------------- main

def main(argv=None):
    argv = list(argv or [])
    only = None
    if "--only" in argv:
        only = set(argv[argv.index("--only") + 1].split(","))
    sys.path.insert(0, HERE)
    import clips as clip_tables
    importlib.reload(clip_tables)
    arm = load_ghost()
    # The takes' frame rate is the scene's, and importing Official_Anim_4.fbx sets it to that file's 25: at 25 a
    # 36-frame clip comes into Unity 1.44 s long instead of 1.2. So it is set after the import
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.render.fps_base = 1.0
    axes = frame_axes(arm)
    built = []
    sheets = []
    for clip in clip_tables.CLIPS:
        if only and clip["id"] not in only:
            continue
        action = build_action(arm, axes, clip)
        built.append(action)
        if "--no-render" not in argv:
            sheets += render_sheet(arm, axes, action, clip)
    if "--no-export" not in argv and not only:
        everything = [bpy.data.actions["GE_" + c["id"] + "_" + c["name"].replace(" ", "")] for c in clip_tables.CLIPS]
        export_fbx(arm, everything)
    load_ghost()
    log("built", len(built), "sheets", len(sheets))
    return {"built": [a.name for a in built], "sheets": sheets}


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    main(args)
