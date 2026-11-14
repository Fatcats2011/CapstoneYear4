"""Rebuild the Dead on Arrival poster scene from the game's own assets, framed for Steam's 1232x706 main capsule.

Buildings and the clock tower are the game's Unity prefabs (see unity_prefab.py), so walls, roofs, windows,
lamps, clock ticks and hands sit exactly as they do in the game. The poster's own buildings are stripped
and dressed the way the poster shows them, and each ghost stands by its scooter holding the near handle.

Run headless:
  blender -b --factory-startup --python tools/keyart/build_poster.py -- <out.png> [eevee|cycles] [samples] [percent]
Also saves tools/keyart/poster.blend so the shot can be tweaked and re-rendered by hand.
"""
import bpy, sys, math, os
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import unity_prefab as up

ROOT = os.path.dirname(os.path.dirname(HERE)).replace("\\", "/") + "/"
A = ROOT + "Assets/"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else ROOT + "docs/steam-store/capsule_scene_1232x706.png"
ENGINE = argv[1] if len(argv) > 1 else "eevee"
SAMPLES = int(argv[2]) if len(argv) > 2 else 64
PERCENT = int(argv[3]) if len(argv) > 3 else 100

# ---------------------------------------------------------------- shot constants (poster match)
# Same camera position as the portrait poster, opened out sideways to Steam's 1232x706 main capsule.
# The centre 537 px of the frame is the poster's rows 75..1075. The logo is added afterwards by
# composite_capsule.py (LOGO_* below is where it goes), never rendered.
RES = (1232, 706)
CAM_LOC = (0.0, -3.2, 1.3)
CAM_LENS = 13.94              # 36mm sensor
CAM_SHIFT_Y = 0.038           # horizon ~57% down the frame, like the poster
MOON_CAM = (29.0, 54.0, -80)  # moon position in camera space (right, up, -distance)
MOON_SIZE = 32
LOGO_WIDTH = 0.345            # fraction of frame width
LOGO_CENTRE = (0.503, 0.855)  # fraction of frame from top-left

PB = "Assets/Prefabs/Buildings/Buildings New/"
TOWER = dict(prefab=PB + "Clock Tower.prefab", loc=(0.0, 15.0, 0.0), scale=0.33)
# (name, prefab, location, way the front looks in degrees [-90 = at the camera, 0 = screen-right], scale)
# One row of houses either side of the clock tower, each touching the next at its front corner.
# (name, prefab, way the front looks in degrees [-90 = at the camera, 0 = screen-right], scale), tower outward.
# The first two on the left and the first on the right are the poster's own buildings (see DRESS further down).
ROW_LEFT = [
    ("LeftHouse", "Large Generic House 1", -54, 0.62),     # teal house, red roof, two square windows
    ("LeftEdge", "Medium Generic House 1", -46, 0.60),     # flat-topped block
    ("LeftTall", "Small Generic House 2", -36, 0.80),
    ("LeftFar", "Medium Generic House 2", -24, 0.75),
]
ROW_RIGHT = [
    ("RightBlock", "Overhang Building 1", -138, 0.60),     # overhang block, arched windows, bell lamps
    ("RightNear", "Multistory Building 1", -147, 0.50),
    ("RightFar", "Tilted Tower Building 1", -158, 0.70),
    ("RightEnd", "Large Generic House 2", -170, 0.75),
]
ROW_START_LEFT = (-2.45, 14.6)      # front corner of the first house, hard against the tower's flank
ROW_START_RIGHT = (2.45, 14.6)
GAME_SCALE = 0.6                   # this scene's size relative to the game's (the buildings' typical scale)
LAMPS = [(-3.6, 3.5), (2.9, 3.4)]
RIDERS = [
    # As in the poster, each ghost stands beside its scooter, faces the camera and holds the near handle.
    # scooter: loc, heading (deg; 270 = nose at the camera, 180 = screen-left, 0 = screen-right), ss = scale,
    #          pitch (deg, - = nose up) about the wheel at `pivot` (model units along the scooter)
    # ghost:   gloc = middle of its base, gface = way it looks (-90 = straight at the camera),
    #          groll = tilt as the camera sees it (+ = clockwise), gs = scale, grip = +1 left handle / -1 right
    dict(name="Purple", body=(0.52, 0.40, 1.0), scooter=(0.30, 0.09, 0.05), stripe=(0.20, 0.06, 0.04),
         loc=(-1.68, 2.07, 0), heading=235, ss=0.46, pitch=-16, pivot=-2.12,    # semi-wheelie on the back wheel
         gloc=(-0.97, 1.8, 1.22), gface=-80, groll=14, gs=0.41, grip=1),
    dict(name="Orange", body=(1.0, 0.25, 0.08), scooter=(0.08, 0.14, 0.85), stripe=(0.06, 0.10, 0.6),
         loc=(1.535, 2.78, 0), heading=-48, ss=0.35, pitch=12, pivot=3.2,       # just landing, front wheel down first
         gloc=(0.88, 2.6, 1.02), gface=-78, groll=-19, gs=0.42, grip=-1),
]
SCOOTER_BULK = 1.35           # the poster's scooters are chunkier than the game model: widen them
BAGS = [("BagYellow", (1.0, 0.82, 0.10), (-0.24, 1.4, 0.97), (8, -14, 20), 0.60),
        ("BagGreen", (0.30, 0.90, 0.30), (1.43, 2.3, 2.16), (-12, 18, -25), 0.42)]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
coll = scene.collection


# ---------------------------------------------------------------- helpers
def bbox(objs):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    mn = Vector([min(p[k] for p in pts) for k in range(3)])
    mx = Vector([max(p[k] for p in pts) for k in range(3)])
    return mn, mx


def load(path, name):
    """Import an FBX and parent everything under an empty at its bbox bottom-centre."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=A + path)
    new = [o for o in bpy.data.objects if o not in before]
    mn, mx = bbox(new)
    root = bpy.data.objects.new(name, None)
    coll.objects.link(root)
    root.location = ((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)
    bpy.context.view_layer.update()
    for o in new:
        if o.parent is None:
            mw = o.matrix_world.copy()
            o.parent = root
            o.matrix_world = mw
    root["off"] = list(root.location)
    root.location = (0, 0, 0)
    root["size"] = list(mx - mn)
    root["kids"] = [o.name for o in new]
    return root


def kids(root):
    return [bpy.data.objects[n] for n in root["kids"]]


def mat_centre(root, matname_prefix):
    """Centre of the faces using a material, in the root's own space."""
    bpy.context.view_layer.update()
    inv = root.matrix_world.inverted()
    acc, n = Vector(), 0
    for o in kids(root):
        if o.type != 'MESH':
            continue
        for p in o.data.polygons:
            m = o.data.materials[p.material_index] if o.data.materials else None
            if m and m.name.startswith(matname_prefix):
                acc += inv @ (o.matrix_world @ p.center)
                n += 1
    return acc / n if n else None


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def img(path, non_color=False):
    im = bpy.data.images.load(A + path, check_existing=True)
    if non_color:
        im.colorspace_settings.name = 'Non-Color'
    return im


def principled(name, color=(0.8, 0.8, 0.8), rough=0.7, emit=None, emit_strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = emit_strength
    return m


def set_all(root, mat):
    for o in kids(root):
        if o.type == 'MESH':
            o.data.materials.clear()
            o.data.materials.append(mat)


def recolor(root, prefix, color, emit=None, emit_strength=0.0, rough=None):
    """Make the imported material(s) whose name starts with prefix a single colour."""
    done = set()
    for o in kids(root):
        if o.type != 'MESH':
            continue
        for m in o.data.materials:
            if m and m.name.startswith(prefix) and m.name not in done:
                m.use_nodes = True
                b = m.node_tree.nodes.get("Principled BSDF")
                for l in list(b.inputs["Base Color"].links):
                    m.node_tree.links.remove(l)
                b.inputs["Base Color"].default_value = (*color, 1)
                if rough is not None:
                    b.inputs["Roughness"].default_value = rough
                if emit:
                    b.inputs["Emission Color"].default_value = (*emit, 1)
                    b.inputs["Emission Strength"].default_value = emit_strength
                done.add(m.name)


def point(name, loc, color, energy, radius=0.1):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'POINT'))
    l.data.color = color
    l.data.energy = energy
    l.data.shadow_soft_size = radius
    l.location = loc
    coll.objects.link(l)
    return l


def spot(name, loc, target, color, energy, angle=50):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SPOT'))
    l.data.color = color
    l.data.energy = energy
    l.data.spot_size = math.radians(angle)
    l.data.spot_blend = 0.6
    l.location = loc
    coll.objects.link(l)
    look_at(l, target)
    return l


def halo_disc(name, pos, size, color, strength, alpha, power=2.0):
    """A see-through glowing disc that faces the camera: soft halo for the moon and the ghosts."""
    m = bpy.data.materials.new(name + "Mat"); m.use_nodes = True
    N, L = m.node_tree.nodes, m.node_tree.links
    for n_ in list(N): N.remove(n_)
    out = N.new("ShaderNodeOutputMaterial"); tcn = N.new("ShaderNodeTexCoord"); mp = N.new("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (-1, -1, 0); mp.inputs["Scale"].default_value = (2, 2, 1)
    gr = N.new("ShaderNodeTexGradient"); gr.gradient_type = 'SPHERICAL'
    pw = N.new("ShaderNodeMath"); pw.operation = 'POWER'; pw.inputs[1].default_value = power
    mu = N.new("ShaderNodeMath"); mu.operation = 'MULTIPLY'; mu.inputs[1].default_value = alpha
    em = N.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*color, 1); em.inputs["Strength"].default_value = strength
    tr = N.new("ShaderNodeBsdfTransparent"); mx = N.new("ShaderNodeMixShader")
    L.new(tcn.outputs["UV"], mp.inputs["Vector"]); L.new(mp.outputs["Vector"], gr.inputs["Vector"])
    L.new(gr.outputs["Fac"], pw.inputs[0]); L.new(pw.outputs[0], mu.inputs[0]); L.new(mu.outputs[0], mx.inputs["Fac"])
    L.new(tr.outputs[0], mx.inputs[1]); L.new(em.outputs[0], mx.inputs[2]); L.new(mx.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    bpy.ops.mesh.primitive_plane_add(size=1)
    d = bpy.context.object
    d.name = name
    d.data.materials.append(m)
    d.location = pos
    d.rotation_euler = (Vector(CAM_LOC) - Vector(pos)).to_track_quat('Z', 'Y').to_euler()
    d.scale = (size, size, 1)
    d.visible_shadow = False
    return d


def prefab(rel, name, loc, face_deg, scale, light_watts=0.0):
    """A game prefab with its front (local +X) looking along face_deg."""
    r = up.import_prefab(rel, name, light_scale=light_watts)
    r.location = (loc[0], loc[1], loc[2] if len(loc) > 2 else 0.0)
    r.rotation_euler = (0, 0, math.radians(face_deg))
    r.scale = (scale,) * 3
    return r


# ---------------------------------------------------------------- ground + sky
# The game's main-area floor: RoadMainMap.mat, tiled 40 times across MainAreaGround (scene scale 0.01831).
GROUND_SIZE = 300.0
ROAD = "Assets/Textures/DoA-Road/RoadMainMap.mat"
GROUND_FBX = "Assets/Models/Main Map/MainAreaGround.fbx"
ground_mat = up.material(next(g_ for g_, p_ in up.guids().items() if p_ == ROAD))
gsrc = up._fbx(GROUND_FBX)[0][0][0]
ext = max(max(v.co[i] for v in gsrc.data.vertices) - min(v.co[i] for v in gsrc.data.vertices) for i in range(3))
ground_m = ext * abs(up._fx(GROUND_FBX)[1][1]) * 0.01831          # the ground's width in game metres
tile = ground_m / 40.0 * GAME_SCALE
print("GROUND", round(ext, 2), round(ground_m, 2), "tile", round(tile, 3))
if not 0.2 < tile < 30:
    tile = 2.0
for n_ in ground_mat.node_tree.nodes:
    if n_.type == 'MAPPING':
        n_.inputs["Scale"].default_value = (GROUND_SIZE / tile, GROUND_SIZE / tile, 1)
    elif n_.type == 'MIX' and n_.blend_type == 'MULTIPLY':
        n_.inputs[7].default_value = (0.27, 0.29, 0.37, 1)      # night: the pavers are near-white in daylight
bpy.ops.mesh.primitive_plane_add(size=GROUND_SIZE, location=(0, 60, 0))
bpy.context.object.name = "Ground"
bpy.context.object.data.materials.append(ground_mat)

world = bpy.data.worlds.new("Night")
scene.world = world
world.use_nodes = True
wn, wl = world.node_tree.nodes, world.node_tree.links
bg = wn["Background"]
tc = wn.new("ShaderNodeTexCoord")
sepw = wn.new("ShaderNodeSeparateXYZ")
wl.new(tc.outputs["Generated"], sepw.inputs[0])
ramp = wn.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = 0.0
ramp.color_ramp.elements[0].color = (0.008, 0.009, 0.03, 1)
ramp.color_ramp.elements[1].position = 0.45
ramp.color_ramp.elements[1].color = (0.003, 0.003, 0.012, 1)
wl.new(sepw.outputs["Z"], ramp.inputs["Fac"])
vor = wn.new("ShaderNodeTexVoronoi")
vor.inputs["Scale"].default_value = 300
st = wn.new("ShaderNodeMapRange")
st.inputs["From Min"].default_value = 0.0
st.inputs["From Max"].default_value = 0.04
st.inputs["To Min"].default_value = 1.0
st.inputs["To Max"].default_value = 0.0
wl.new(tc.outputs["Generated"], vor.inputs["Vector"])
wl.new(vor.outputs["Distance"], st.inputs["Value"])
add = wn.new("ShaderNodeMix")
add.data_type = 'RGBA'
add.blend_type = 'ADD'
wl.new(st.outputs["Result"], add.inputs["Factor"])
wl.new(ramp.outputs["Color"], add.inputs[6])
add.inputs[7].default_value = (0.35, 0.35, 0.5, 1)
wl.new(add.outputs[2], bg.inputs["Color"])

moon_mat = bpy.data.materials.new("Moon")
moon_mat.use_nodes = True
mn_, ml_ = moon_mat.node_tree.nodes, moon_mat.node_tree.links
for n in list(mn_):
    mn_.remove(n)
o_ = mn_.new("ShaderNodeOutputMaterial")
t_ = mn_.new("ShaderNodeTexImage"); t_.image = img("Materials/Skybox/Moon_Texture.png")
e_ = mn_.new("ShaderNodeEmission"); e_.inputs["Strength"].default_value = 3.2
tr_ = mn_.new("ShaderNodeBsdfTransparent")
mx_ = mn_.new("ShaderNodeMixShader")
ml_.new(t_.outputs["Color"], e_.inputs["Color"])
ml_.new(t_.outputs["Alpha"], mx_.inputs["Fac"])
ml_.new(tr_.outputs[0], mx_.inputs[1])
ml_.new(e_.outputs[0], mx_.inputs[2])
ml_.new(mx_.outputs[0], o_.inputs["Surface"])
bpy.ops.mesh.primitive_plane_add(size=1)
moon = bpy.context.object
moon.name = "Moon"
moon.data.materials.append(moon_mat)

sun = bpy.data.objects.new("Moonlight", bpy.data.lights.new("Moonlight", 'SUN'))
sun.data.color = (0.55, 0.60, 1.0)
sun.data.energy = 1.6
sun.data.angle = math.radians(3)
coll.objects.link(sun)

# ---------------------------------------------------------------- clock tower (the game prefab: tower, ticks, hands)
tower = prefab(TOWER["prefab"], "ClockTower", TOWER["loc"], 0, TOWER["scale"])
bpy.context.view_layer.update()
tw_mesh = next(o for o in kids(tower) if "Clock Tower_Textured" in o.get("unity_src", ""))
tower_mat = next(m for m in tw_mesh.data.materials if m and m.name.startswith("ClockTower"))
# the prefab's -Y side carries the clock hands, so it already faces the camera
for i in range(len(tw_mesh.data.materials)):     # one texture atlas covers the whole tower in the game
    tw_mesh.data.materials[i] = tower_mat
bpy.context.view_layer.update()
# the game leaves the hands at about 10:20; the poster reads 3 o'clock, so swing them round the clock centre
ring = min((t_ for t_ in kids(tower) if "Clock_Ticks" in t_.get("unity_src", "")), key=lambda t_: bbox([t_])[0].y)
hub = sum(bbox([ring]), Vector()) / 2                      # middle of the tick ring on the camera side
for o in kids(tower):
    want = {"Minute_Hand": 0, "Hour_Hand": 90}.get(os.path.splitext(os.path.basename(o.get("unity_src", "")))[0])
    if want is not None:        # degrees clockwise from 12, as the camera sees it
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        tip = max(pts, key=lambda p: (p.x - hub.x) ** 2 + (p.z - hub.z) ** 2)
        now = math.degrees(math.atan2(tip.x - hub.x, tip.z - hub.z))
        c = Vector((hub.x, o.matrix_world.translation.y, hub.z))
        o.matrix_world = (Matrix.Translation(c) @ Matrix.Rotation(math.radians(want - now), 4, 'Y')
                          @ Matrix.Translation(-c) @ o.matrix_world)
bpy.context.view_layer.update()
T_H = (tower["max"][2]) * TOWER["scale"]
TOWER_V = Vector(TOWER["loc"])
spot("TowerUp", TOWER_V + Vector((0, -6, 0.2)), TOWER_V + Vector((0, 0, T_H * 0.36)), (1.0, 0.5, 0.2), 5000, 35)
spot("TowerTop", TOWER_V + Vector((0, -9, 6)), TOWER_V + Vector((0, -1, T_H * 0.62)), (0.55, 0.62, 1.0), 2600, 30)
point("TowerGreen", TOWER_V + Vector((0, -3.6, T_H * 0.40)), (0.3, 1.0, 0.4), 45, 1.0)

# ---------------------------------------------------------------- buildings (game prefabs; front = local +X)
def src_name(o):
    return os.path.basename(o.get("unity_src", ""))


def keep_only(root, keep, drop_lights=True):
    """Strip a prefab down to the parts whose model file name starts with one of `keep`."""
    kept = []
    for o in kids(root):
        if src_name(o).startswith(tuple(keep)):
            kept.append(o.name)
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    root["kids"] = kept
    if drop_lights:
        for c in list(root.children):
            if c.type == 'LIGHT':
                bpy.data.objects.remove(c, do_unlink=True)


def drop(root, names, drop_lights=True):
    kept = []
    for o in kids(root):
        if src_name(o).startswith(tuple(names)):
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            kept.append(o.name)
    root["kids"] = kept
    if drop_lights:
        for c in list(root.children):
            if c.type == 'LIGHT':
                bpy.data.objects.remove(c, do_unlink=True)


def body_of(root):
    return max((o for o in kids(root) if src_name(o).startswith(("Generic_", "OverHang", "MultiStory", "TiltedTower"))),
               key=lambda o: o.dimensions.x * o.dimensions.y * o.dimensions.z)


def wall_x(root, y, z):
    """Where the front (+X) wall is at (y, z); the prefab root must still be at the origin."""
    bpy.context.view_layer.update()
    b = body_of(root)
    inv = b.matrix_world.inverted()
    hit, loc, _n, _i = b.ray_cast(inv @ Vector((60, y, z)), (inv.to_3x3() @ Vector((-1, 0, 0))).normalized())
    return (b.matrix_world @ loc).x if hit else bbox([b])[1].x


_parts = {}


def kit_part(prefix, donor):
    """A kit model (wall lamp, vine...) as the game mounts it on a wall, re-based so the wall is the plane
    x = 0 and the part sticks out along +X. Borrowed once from `donor`, a prefab that has one."""
    if prefix not in _parts:
        tmp = up.import_prefab(PB + donor + ".prefab", "_donor")
        bpy.context.view_layer.update()
        part = next(o for o in kids(tmp) if src_name(o).startswith(prefix))
        hb0, hb1 = bbox([body_of(tmp)])
        lb0, lb1 = bbox([part])
        c = (lb0 + lb1) / 2
        out = max(((lb1.x - hb1.x, Vector((1, 0, 0))), (hb0.x - lb0.x, Vector((-1, 0, 0))),
                   (lb1.y - hb1.y, Vector((0, 1, 0))), (hb0.y - lb0.y, Vector((0, -1, 0)))), key=lambda e: e[0])[1]
        contact = Vector((lb0.x if out.x > 0 else lb1.x if out.x < 0 else c.x,
                          lb0.y if out.y > 0 else lb1.y if out.y < 0 else c.y, c.z))
        keep = part.copy(); keep.data = part.data.copy(); keep.parent = None
        keep.matrix_world = Matrix.Rotation(-math.atan2(out.y, out.x), 4, 'Z') @ Matrix.Translation(-contact) @ part.matrix_world
        up._library().objects.link(keep)
        keep["half_h"] = (lb1.z - lb0.z) / 2
        _parts[prefix] = keep
        for o in kids(tmp):
            bpy.data.objects.remove(o, do_unlink=True)
        for c_ in list(tmp.children):
            bpy.data.objects.remove(c_, do_unlink=True)
        bpy.data.objects.remove(tmp, do_unlink=True)
    return _parts[prefix]


def mount(root, prefix, donor, y, z, scale=1.0):
    """Put a kit part on the prefab's front wall with its middle at (y, z)."""
    src = kit_part(prefix, donor)
    x = wall_x(root, y, z)
    o = src.copy()
    coll.objects.link(o)
    o.matrix_world = Matrix.Translation(Vector((x, y, z))) @ Matrix.Diagonal((scale, scale, scale, 1)) @ src.matrix_world
    o.parent = root
    root["kids"] = list(root["kids"]) + [o.name]
    return x


def bell_lamp(root, y, z, cone=True):
    """A game wall lamp (wallLamp_03) on the front wall, with the warm pool of light the poster shows under it."""
    x = mount(root, "wallLamp_03", "Medium Generic House 1", y, z)
    if cone:
        s = spot(f"{root.name}Cone{len(root.children)}", (x + 0.9, y, z - 0.3), (x + 0.05, y, z - 5.0), (1.0, 0.50, 0.12), 900, 62)
        s.data.spot_blend = 0.25
        s.parent = root


def vine(root, fy, scale=1.0):
    """The kit's climbing vine, rooted at the foot of the front wall, fy of the way along it."""
    b0, b1 = bbox([body_of(root)])
    half = kit_part("vineWallScaling", "Small Generic House 1")["half_h"] * scale
    mount(root, "vineWallScaling", "Small Generic House 1", b0.y + (b1.y - b0.y) * fy, b0.z + half, scale)


def square_window(root, y, z, scale=1.3):
    """The kit's four-pane window (Window.fbx), lit, on the front wall."""
    w = load("Models/Modular Kit/Modular Building Assets/Window.fbx", f"{root.name}Window")
    recolor(w, "Window", (1.0, 0.85, 0.3), emit=(1.0, 0.78, 0.22), emit_strength=3.0)
    recolor(w, "Frame", (0.03, 0.03, 0.04), rough=0.6)
    w.scale = (scale,) * 3
    w.location = (wall_x(root, y, z + 1.0 * scale), y, z)
    w.parent = root
    p = point(f"{root.name}WinGlow", (w.location.x + 1.5, y, z + 1.3), (1.0, 0.7, 0.3), 120, 0.4)
    p.parent = root


def dress_left_house(r):      # poster: bare house + roof, two square windows upstairs
    keep_only(r, ("Generic_", "RoofGeneric_"))
    b0, b1 = bbox([body_of(r)])
    for f in (0.27, 0.39):
        square_window(r, b0.y + (b1.y - b0.y) * f, b0.z + (b1.z - b0.z) * 0.56, scale=0.85)


def dress_left_edge(r):       # poster: flat-topped block, no roof showing
    drop(r, ("RoofGeneric_", "Chimney"), drop_lights=False)


def dress_overhang(r):        # poster: no roof, arched windows, cross-timber balcony, bell lamps with warm pools
    drop(r, ("RoofGeneric_", "DoubleDoor", "pipe", "wallLamp_02", "ThickWoodDoor", "ArchDoor"))
    b0, b1 = bbox([body_of(r)])
    h = b1.z - b0.z
    for fy, fz in ((0.22, 0.74), (0.60, 0.78), (0.70, 0.36)):
        bell_lamp(r, b0.y + (b1.y - b0.y) * fy, b0.z + h * fz)
    vine(r, 0.90, 1.2)
    vine(r, 0.10, 1.0)


def dress_right_near(r):      # poster: vines climbing the block at the right-hand edge
    vine(r, 0.25, 1.3)
    vine(r, 0.70, 1.1)


DRESS = {"LeftHouse": dress_left_house, "LeftEdge": dress_left_edge, "RightBlock": dress_overhang,
         "RightNear": dress_right_near}
def footprint(body):
    """Bounds of a building's walls (its lower three quarters), so the eaves' overhang doesn't hold neighbours apart."""
    bpy.context.view_layer.update()
    pts = [body.matrix_world @ v.co for v in body.data.vertices]
    z0 = min(p.z for p in pts); z1 = max(p.z for p in pts)
    low = [p for p in pts if p.z <= z0 + (z1 - z0) * 0.75]
    return (Vector([min(p[k] for p in low) for k in range(3)]), Vector([max(p[k] for p in low) for k in range(3)]))


def row(entries, start, side):
    """Lay houses out from `start`, tower outward, each one's front corner on the last one's (side: -1 left, +1 right)."""
    corner = Vector((start[0], start[1], 0))
    for name, pf, face, s in entries:
        r = up.import_prefab(PB + pf + ".prefab", name, light_scale=130.0)
        bpy.context.view_layer.update()
        if name in DRESS:
            DRESS[name](r)
        b0, b1 = footprint(body_of(r))                    # walls only, prefab still at the origin
        a = math.radians(face)
        n = Vector((math.cos(a), math.sin(a), 0))         # way the front looks
        u = Vector((-n.y, n.x, 0))                        # the prefab's +Y in the world
        # which end of the front wall is nearer the tower: on the left row +Y runs toward the tower
        y_in, y_out = (b1.y, b0.y) if side < 0 else (b0.y, b1.y)
        r.location = corner - s * (b1.x * n + y_in * u)
        r.rotation_euler = (0, 0, a)
        r.scale = (s,) * 3
        corner = corner + s * (y_out - y_in) * u


row(ROW_LEFT, ROW_START_LEFT, -1)
row(ROW_RIGHT, ROW_START_RIGHT, 1)

# cyan street lamps either side (short posts close to the camera, as in the poster)
lamp_glow = principled("LampGlow", (0, 0, 0), 0.3, emit=(0.22, 0.80, 1.0), emit_strength=1.7)
lamp_metal = principled("LampMetal", (0.05, 0.06, 0.08), 0.5)
for k, (x, y) in enumerate(LAMPS):
    l = load("Models/Modular Kit/Street Props/streetLamp_01.fbx", f"Lamp{k}")
    set_all(l, lamp_metal)
    l.location = (x, y, 0); l.scale = (0.62,) * 3
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.17, location=(x, y, 1.98))
    bpy.context.object.name = f"LampGlass{k}"
    bpy.context.object.scale = (1, 1, 1.25)
    bpy.context.object.data.materials.append(lamp_glow)
    point(f"LampL{k}", (x, y - 0.3, 1.95), (0.45, 0.9, 1.0), 60, 0.3)


# ---------------------------------------------------------------- ghosts riding scooters
def paper_bag(name, color, loc, rot_deg, size, handles=False):
    m = principled(name + "Mat", color, 0.85, emit=color, emit_strength=0.25)
    bpy.ops.mesh.primitive_cube_add(size=1)
    b = bpy.context.object
    b.name = name
    b.scale = (size * 0.85, size * 0.5, size * 1.1)
    for v in b.data.vertices:          # slight taper + crumpled top
        if v.co.z > 0:
            v.co.x *= 0.92
            v.co.z += 0.06 * math.sin(v.co.x * 9 + v.co.y * 5)
    b.data.materials.append(m)
    b.location = loc
    b.rotation_euler = [math.radians(a) for a in rot_deg]
    bpy.ops.mesh.primitive_cube_add(size=1)      # folded top flap
    f = bpy.context.object
    f.name = name + "Flap"
    f.parent = b
    f.location = (0, 0, 0.56)
    f.scale = (0.9, 0.35, 0.18)
    f.rotation_euler = (math.radians(10), 0, 0)
    f.data.materials.append(m)
    if handles:                                   # twine loops, like the poster's shopping bag
        hm_ = principled(name + "Twine", tuple(c * 0.7 for c in color), 0.9)
        for side in (-0.2, 0.2):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.22, minor_radius=0.03, major_segments=24, minor_segments=8)
            h = bpy.context.object
            h.name = name + "Handle"
            h.parent = b
            h.location = (0, side, 0.60)
            h.rotation_euler = (math.radians(90), 0, 0)
            h.scale = (1.0, 1.25, 1.0 / (size * 0.5) * size * 0.85)
            h.data.materials.append(hm_)
    return b


def hand_pieces(mesh_obj):
    """The ghost's two hands: loose pieces of its mesh, the two furthest forward. Returns lists of vertex indices."""
    import bmesh
    bpy.context.view_layer.update()
    bm = bmesh.new(); bm.from_mesh(mesh_obj.data); bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop(); comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index); stack.append(y)
        parts.append(comp)
    bm.free()
    co = mesh_obj.data.vertices
    big = [p for p in parts if len(p) > 50]
    return sorted(big, key=lambda p: -sum(co[i].co.x for i in p) / len(p))[:2] if False else \
        sorted(big, key=lambda p: -sum((mesh_obj.matrix_world @ co[i].co).x for i in p) / len(p))[:2]


for i, g in enumerate(RIDERS):
    sc = load("Models/Vehicle Models/GhostMates.fbx", f"Scooter{g['name']}")
    recolor(sc, "Scooter.", g["scooter"], rough=0.3)
    recolor(sc, "Logo", g["scooter"], rough=0.3)
    recolor(sc, "Stripe", g["stripe"], rough=0.3)
    recolor(sc, "ScooterBorder", g["stripe"], rough=0.3)
    recolor(sc, "Seat", (0.05, 0.03, 0.03), rough=0.5)
    recolor(sc, "Headlights", (0, 0, 0), emit=(1.0, 0.75, 0.4), emit_strength=5)
    recolor(sc, "RearLight.", (1, 0.1, 0.1), emit=(1.0, 0.1, 0.05), emit_strength=10)
    head = mat_centre(sc, "Headlights")           # scooter space: nose is +X, origin under the middle
    S = g["ss"]
    yaw = Matrix.Rotation(math.radians(g["heading"]), 4, 'Z')
    fwd = yaw @ Vector((1, 0, 0))
    flat = Matrix.Translation(Vector(g["loc"])) @ yaw @ Matrix.Diagonal((S, S * SCOOTER_BULK, S, 1))
    piv = Vector((g["pivot"], 0, 0))                  # the wheel that stays on the ground
    tilt = flat @ Matrix.Translation(piv) @ Matrix.Rotation(math.radians(g["pitch"]), 4, 'Y') @ Matrix.Translation(-piv)
    grip_local = Vector((0.505, 1.05 * g["grip"], 5.0))
    slide = flat @ grip_local - tilt @ grip_local     # keep the held handle where it was; only its height changes
    slide.z = 0
    sc.matrix_world = Matrix.Translation(slide) @ tilt
    bpy.context.view_layer.update()
    head_w = sc.matrix_world @ head
    spot(f"Beam{g['name']}", head_w + fwd * 0.1, head_w + fwd * 6 - Vector((0, 0, 0.9)), (1.0, 0.85, 0.6), 60, 60)
    point(f"HeadGlow{g['name']}", head_w + fwd * 0.25, (1.0, 0.85, 0.6), 40, 0.05)
    # handle grips in scooter space (model units): x 0.505, y +-1.05, z 5.0
    grip_w = sc.matrix_world @ grip_local

    gh = load("Models/Ghost Models/GHOST_FINAL.fbx", f"Ghost{g['name']}")
    recolor(gh, "Body", g["body"], emit=g["body"], emit_strength=0.42, rough=0.35)
    recolor(gh, "Horn", (1.0, 0.80, 0.72), rough=0.4)
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    hands = hand_pieces(gm)                       # found while the ghost still stands at the origin
    k = g["gs"]
    # ghost space: the body's middle is 0.06 ahead of the origin (the tail hangs back)
    gh.matrix_world = (Matrix.Translation(Vector(g["gloc"])) @ Matrix.Rotation(math.radians(g["groll"]), 4, 'Y')
                       @ Matrix.Rotation(math.radians(g["gface"]), 4, 'Z') @ Matrix.Diagonal((k, k, k, 1))
                       @ Matrix.Translation(Vector((-0.06, 0, 0))))
    bpy.context.view_layer.update()
    # put the nearer hand on the handle
    co = gm.data.vertices
    mid = lambda p: sum((gm.matrix_world @ co[j].co for j in p), Vector()) / len(p)
    hand = min(hands, key=lambda p: (mid(p) - grip_w).length)
    inv = gm.matrix_world.inverted()
    shift = inv @ grip_w - inv @ mid(hand)
    for j in hand:
        co[j].co += shift
    gm.data.update()
    gc = Vector(g["gloc"]) + Vector((0, 0, 1.7 * k))
    to_cam = (Vector(CAM_LOC) - gc).normalized()
    point(f"GhostGlow{g['name']}", gc + to_cam * 1.2 + Vector((0, 0, 0.6)), g["body"], 45, 0.5)
    halo_disc(f"GhostHalo{g['name']}", gc - to_cam * 0.6, 3.53 * k * 2.3, g["body"], 1.3, 0.30, 1.8)    # faint glow

for name, color, loc, rot, size in BAGS:
    paper_bag(name, color, Vector(loc), rot, size, handles=(name == "BagYellow"))

# ---------------------------------------------------------------- camera
cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
coll.objects.link(cam)
cam.location = CAM_LOC
look_at(cam, (CAM_LOC[0], 15, CAM_LOC[2]))      # level camera: verticals stay straight
cam.data.lens = CAM_LENS
cam.data.sensor_fit = 'HORIZONTAL'
cam.data.shift_y = CAM_SHIFT_Y
scene.camera = cam

bpy.context.view_layer.update()
mpos = cam.matrix_world @ Vector(MOON_CAM)
moon.location = mpos
moon.rotation_euler = cam.rotation_euler
moon.scale = (782 / 701 * MOON_SIZE, MOON_SIZE, 1)
sun.rotation_euler = (cam.location - mpos).to_track_quat('-Z', 'Y').to_euler()
# soft halo round the moon, as in the poster
halo_disc("MoonHalo", cam.matrix_world @ (Vector(MOON_CAM) * 1.02), MOON_SIZE * 2.3 * 1.02, (0.55, 0.60, 1.0), 1.6, 0.32, 3.0)

sky = bpy.data.objects.new("SkyFill", bpy.data.lights.new("SkyFill", 'SUN'))
sky.data.color = (0.42, 0.50, 1.0); sky.data.energy = 2.2; sky.data.angle = math.radians(20)
coll.objects.link(sky); sky.location = (0, -10, 20); look_at(sky, (0, 14, 4))
fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'AREA'))
fill.data.shape = 'DISK'; fill.data.size = 12; fill.data.energy = 110; fill.data.color = (0.40, 0.48, 0.90)
fill.location = (0, -6, 9); coll.objects.link(fill); look_at(fill, (0, 10, 0))
# haze
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 30, 15))
hz = bpy.context.object
hz.name = "Haze"
hz.scale = (120, 80, 30)
hm = bpy.data.materials.new("HazeMat"); hm.use_nodes = True
for n_ in list(hm.node_tree.nodes): hm.node_tree.nodes.remove(n_)
vo = hm.node_tree.nodes.new("ShaderNodeVolumePrincipled")
vo.inputs["Density"].default_value = 0.0015
vo.inputs["Color"].default_value = (0.45, 0.5, 0.9, 1)
oo = hm.node_tree.nodes.new("ShaderNodeOutputMaterial")
hm.node_tree.links.new(vo.outputs[0], oo.inputs["Volume"])
hz.data.materials.append(hm)

# ---------------------------------------------------------------- render settings
r = scene.render
r.resolution_x, r.resolution_y, r.resolution_percentage = RES[0], RES[1], PERCENT
r.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0.4
if ENGINE == "cycles":
    r.engine = 'CYCLES'
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
else:
    engines = [e.identifier for e in r.bl_rna.properties['engine'].enum_items]
    r.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = SAMPLES
    try:
        scene.eevee.use_shadows = True
        scene.eevee.use_raytracing = True
    except Exception:
        pass
r.filepath = OUT
bpy.ops.file.make_paths_absolute()
bpy.ops.wm.save_as_mainfile(filepath=ROOT + "tools/keyart/poster.blend")
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
