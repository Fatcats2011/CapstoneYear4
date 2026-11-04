"""Rebuild the Dead on Arrival key art as a wide 1232x706 shot from game FBXs.

Run headless:
  blender -b --factory-startup --python tools/keyart/build_keyart.py -- <out.png> [eevee|cycles] [samples]
Also saves tools/keyart/keyart.blend so the shot can be tweaked by hand.
"""
import bpy, sys, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/") + "/"
A = ROOT + "Assets/"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else ROOT + "docs/steam-store/keyart_render.png"
ENGINE = argv[1] if len(argv) > 1 else "eevee"
SAMPLES = int(argv[2]) if len(argv) > 2 else 64

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
    root.location = (0, 0, 0)
    root["size"] = list(mx - mn)
    root["kids"] = [o.name for o in new]
    return root


def kids(root):
    return [bpy.data.objects[n] for n in root["kids"]]


def mat_centre(root, matname_prefix):
    """World-space centre of the faces using a material (root at origin, unrotated)."""
    bpy.context.view_layer.update()
    acc, n = Vector(), 0
    for o in kids(root):
        if o.type != 'MESH':
            continue
        for p in o.data.polygons:
            m = o.data.materials[p.material_index] if o.data.materials else None
            if m and m.name.startswith(matname_prefix):
                acc += o.matrix_world @ p.center
                n += 1
    return acc / n if n else None


def place(root, loc, yaw_deg=0, scale=1.0):
    root.location = loc
    root.rotation_euler = (0, 0, math.radians(yaw_deg))
    root.scale = (scale, scale, scale)


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


def textured(name, base, tint=(1, 1, 1), box_scale=None, normal=None, emissive=None, emit_strength=3.0, rough=0.8):
    """Image material. box_scale -> box-projected object-space mapping, else UVs."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    b = N["Principled BSDF"]
    b.inputs["Roughness"].default_value = rough
    vec = None
    if box_scale:
        tc = N.new("ShaderNodeTexCoord")
        mp = N.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (box_scale,) * 3
        L.new(tc.outputs["Object"], mp.inputs["Vector"])
        vec = mp.outputs["Vector"]

    def tex(path, nc=False):
        t = N.new("ShaderNodeTexImage")
        t.image = img(path, nc)
        if box_scale:
            t.projection = 'BOX'
            t.projection_blend = 0.25
            L.new(vec, t.inputs["Vector"])
        return t

    t = tex(base)
    mul = N.new("ShaderNodeMix")
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs["Factor"].default_value = 1.0
    L.new(t.outputs["Color"], mul.inputs[6])
    mul.inputs[7].default_value = (*tint, 1)
    L.new(mul.outputs[2], b.inputs["Base Color"])
    if normal:
        tn = tex(normal, True)
        nm = N.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = 0.6
        L.new(tn.outputs["Color"], nm.inputs["Color"])
        L.new(nm.outputs["Normal"], b.inputs["Normal"])
    if emissive:
        te = tex(emissive)
        L.new(te.outputs["Color"], b.inputs["Emission Color"])
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
        for i, m in enumerate(o.data.materials):
            if m and m.name.startswith(prefix):
                if m.name not in done:
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


# ---------------------------------------------------------------- materials
def plaster(name, base, rust):
    """Smooth dark plaster with rusty blotches, like the poster's walls."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    N, L = m.node_tree.nodes, m.node_tree.links
    b = N["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.85
    tc = N.new("ShaderNodeTexCoord")
    nz = N.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.6
    nz.inputs["Detail"].default_value = 6
    L.new(tc.outputs["Object"], nz.inputs["Vector"])
    rp = N.new("ShaderNodeValToRGB")
    rp.color_ramp.elements[0].position = 0.45
    rp.color_ramp.elements[0].color = (*base, 1)
    rp.color_ramp.elements[1].position = 0.75
    rp.color_ramp.elements[1].color = (*rust, 1)
    L.new(nz.outputs["Fac"], rp.inputs["Fac"])
    L.new(rp.outputs["Color"], b.inputs["Base Color"])
    return m


wall_mats = [plaster("WallA", (0.09, 0.08, 0.13), (0.22, 0.10, 0.07)),
             plaster("WallB", (0.12, 0.09, 0.12), (0.26, 0.13, 0.08)),
             plaster("WallC", (0.07, 0.07, 0.11), (0.18, 0.09, 0.08))]
roof_mats = [textured(f"Roof{i}", p, tint=t, rough=0.75) for i, (p, t) in enumerate([
    ("Textures/Roofs/RoofGeneric1/DefaultMaterial_Base_color.png", (0.62, 0.22, 0.22)),
    ("Textures/Roofs/RoofGeneric2/DefaultMaterial_Base_color.png", (0.55, 0.24, 0.30)),
    ("Textures/Roofs/RoofGeneric3/RoofGeneric_03_DefaultMaterial_BaseColor.png", (0.60, 0.25, 0.25))])]

# ground: big square tiles, slightly glossy
ground_mat = bpy.data.materials.new("Tiles")
ground_mat.use_nodes = True
gN, gL = ground_mat.node_tree.nodes, ground_mat.node_tree.links
gb = gN["Principled BSDF"]
gb.inputs["Roughness"].default_value = 0.32
gtc = gN.new("ShaderNodeTexCoord")
br = gN.new("ShaderNodeTexBrick")
br.offset = 0.0
br.inputs["Scale"].default_value = 1.0
br.inputs["Brick Width"].default_value = 1.3
br.inputs["Row Height"].default_value = 1.3
br.inputs["Mortar Size"].default_value = 0.035
br.inputs["Mortar Smooth"].default_value = 0.3
br.inputs["Color1"].default_value = (0.13, 0.15, 0.21, 1)
br.inputs["Color2"].default_value = (0.10, 0.12, 0.17, 1)
br.inputs["Mortar"].default_value = (0.025, 0.025, 0.04, 1)
gL.new(gtc.outputs["Object"], br.inputs["Vector"])
gL.new(br.outputs["Color"], gb.inputs["Base Color"])
bump = gN.new("ShaderNodeBump")
bump.inputs["Strength"].default_value = 0.4
gL.new(br.outputs["Fac"], bump.inputs["Height"])
gL.new(bump.outputs["Normal"], gb.inputs["Normal"])

# clock tower: game texture, the green glass in the texture glows
tower_mat = textured("ClockTower", "Textures/ClockTower_Textures/Clock_Tower_Clock Tower_Textured_BaseMap.png",
                     normal="Textures/ClockTower_Textures/Clock_Tower_Clock Tower_Textured_Normal.png", rough=0.75)
tN, tL = tower_mat.node_tree.nodes, tower_mat.node_tree.links
tex_node = [n for n in tN if n.type == 'TEX_IMAGE' and 'BaseMap' in n.image.name][0]
sep = tN.new("ShaderNodeSeparateColor")
tL.new(tex_node.outputs["Color"], sep.inputs["Color"])
mxrb = tN.new("ShaderNodeMath"); mxrb.operation = 'MAXIMUM'
tL.new(sep.outputs[0], mxrb.inputs[0]); tL.new(sep.outputs[2], mxrb.inputs[1])
diff = tN.new("ShaderNodeMath"); diff.operation = 'SUBTRACT'
tL.new(sep.outputs[1], diff.inputs[0]); tL.new(mxrb.outputs[0], diff.inputs[1])
mr = tN.new("ShaderNodeMapRange")
mr.inputs["From Min"].default_value = 0.14
mr.inputs["From Max"].default_value = 0.30
tL.new(diff.outputs[0], mr.inputs["Value"])
tb = tN["Principled BSDF"]
tb.inputs["Emission Color"].default_value = (0.35, 1.0, 0.45, 1)
tL.new(mr.outputs["Result"], tb.inputs["Emission Strength"])
mul_e = tN.new("ShaderNodeMath"); mul_e.operation = 'MULTIPLY'; mul_e.inputs[1].default_value = 0.0
tL.new(mr.outputs["Result"], mul_e.inputs[0])
tL.new(mul_e.outputs[0], tb.inputs["Emission Strength"])

window_glow = principled("WindowGlow", (1.0, 0.85, 0.30), 0.5, emit=(1.0, 0.80, 0.25), emit_strength=5.0)
arch_glow = principled("ArchGlow", (1.0, 0.75, 0.30), 0.5, emit=(1.0, 0.65, 0.2), emit_strength=3.0)
lamp_glow = principled("LampGlow", (0.7, 1.0, 1.0), 0.3, emit=(0.55, 0.95, 1.0), emit_strength=18.0)
lamp_metal = principled("LampMetal", (0.05, 0.06, 0.08), 0.5)
frame_mat = principled("Frame", (0.05, 0.04, 0.05), 0.7)
face_mat = principled("ClockFace", (0.75, 0.66, 0.50), 0.6, emit=(1.0, 0.85, 0.6), emit_strength=0.15)
rim_mat = principled("ClockRim", (0.22, 0.12, 0.10), 0.6)
hand_mat = principled("ClockHands", (0.1, 0.9, 0.2), 0.4, emit=(0.1, 1.0, 0.2), emit_strength=3.0)
tower_glass = principled("TowerGlass", (0.1, 0.8, 0.2), 0.4, emit=(0.15, 1.0, 0.25), emit_strength=2.5)

# ---------------------------------------------------------------- ground + sky
bpy.ops.mesh.primitive_plane_add(size=300, location=(0, 60, 0))
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
ramp.color_ramp.elements[0].color = (0.05, 0.04, 0.12, 1)
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

moon_img = img("Materials/Skybox/Moon_Texture.png")
moon_mat = bpy.data.materials.new("Moon")
moon_mat.use_nodes = True
mn_, ml_ = moon_mat.node_tree.nodes, moon_mat.node_tree.links
for n in list(mn_):
    mn_.remove(n)
o_ = mn_.new("ShaderNodeOutputMaterial")
t_ = mn_.new("ShaderNodeTexImage"); t_.image = moon_img
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

# ---------------------------------------------------------------- clock tower (front = local +X)
TOWER = Vector((0.0, 15.0, 0.0))
TS = 1.75
tower = load("Models/City Center Models/Clock Tower_Textured.fbx", "ClockTower")
set_all(tower, tower_mat)
place(tower, TOWER, -90, TS)          # local +X -> world -Y (toward camera)
# clock face (model has only the octagon frame): local x 0.80, z 5.6
bpy.context.view_layer.update()
def tower_pt(x, y, z):
    return tower.matrix_world @ Vector((x, y, z))
face_c = tower_pt(0.80, 0.0, 5.58)
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.56 * TS, depth=0.05, location=face_c,
                                    rotation=(math.radians(90), 0, math.radians(22.5)))
bpy.context.object.data.materials.append(rim_mat)
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.47 * TS, depth=0.05, location=face_c - Vector((0, 0.03, 0)),
                                    rotation=(math.radians(90), 0, math.radians(22.5)))
bpy.context.object.data.materials.append(face_mat)
for k in range(12):  # tick marks
    a = math.radians(k * 30)
    r_ = 0.39 * TS
    bpy.ops.mesh.primitive_cube_add(size=1, location=face_c + Vector((math.sin(a) * r_, -0.07, math.cos(a) * r_)),
                                    rotation=(0, -a, 0))
    t = bpy.context.object
    t.scale = (0.025 * TS, 0.02, (0.09 if k % 3 == 0 else 0.05) * TS)
    t.data.materials.append(rim_mat)
for ang, ln, wd in ((math.radians(60), 0.30, 0.05), (math.radians(-10), 0.40, 0.035)):  # ~2 o'clock-ish hands
    bpy.ops.mesh.primitive_cube_add(size=1)
    h = bpy.context.object
    h.scale = (wd * TS, 0.02, ln * TS)
    h.rotation_euler = (0, -ang, 0)
    h.location = face_c + Vector((math.sin(ang) * ln * TS / 2, -0.1, math.cos(ang) * ln * TS / 2))
    h.data.materials.append(hand_mat)
point("ClockLight", face_c + Vector((0, -1.5, 0)), (1.0, 0.9, 0.7), 25, 0.5)
# glowing green glass behind the arched windows (local x just inside the facade)
bpy.ops.mesh.primitive_plane_add(size=1, location=tower_pt(0.62, 0.0, 3.72), rotation=(math.radians(90), 0, 0))
gl = bpy.context.object
gl.scale = (1.4 * TS, 0.85 * TS, 1)
gl.data.materials.append(tower_glass)
spot("TowerUp", TOWER + Vector((0, -6, 0.2)), TOWER + Vector((0, 0, 6)), (1.0, 0.5, 0.2), 5000, 35).data.volume_factor = 0.0
point("TowerGreen", tower_pt(0.9, 0, 4.0) + Vector((0, -1.5, 0)), (0.3, 1.0, 0.4), 150, 1.0)

# ---------------------------------------------------------------- buildings round the square
BUILD = {"g1": ("Models/Building Revision 3/Generic_01.fbx", "Models/Building Revision 3/RoofGeneric_01.fbx"),
         "g2": ("Models/Building Revision 3/Generic_02.fbx", "Models/Building Revision 3/RoofGeneric_02.fbx"),
         "g3": ("Models/Building Revision 3/Generic_03.fbx", "Models/Building Revision 3/RoofGeneric_03.fbx"),
         "ms": ("Models/Building Revision 3/MultiStory-building.fbx", None),
         "oh": ("Models/Building Revision 3/OverHang-building.fbx", None)}


def window(c, normal, w=0.8, h=1.0, arch=False):
    """Four-pane lit window facing along `normal` (unit XY vector) centred at c."""
    n = Vector((normal[0], normal[1], 0)).normalized()
    yaw = math.atan2(n.y, n.x)
    rot = (math.radians(90), 0, yaw + math.radians(90))
    bpy.ops.mesh.primitive_plane_add(size=1, location=c + n * 0.03, rotation=rot)
    g = bpy.context.object
    g.scale = (w, h, 1)
    g.data.materials.append(arch_glow if arch else window_glow)
    if arch:
        bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=w / 2, depth=0.02,
                                            location=c + n * 0.03 + Vector((0, 0, h / 2)),
                                            rotation=(math.radians(90), 0, yaw + math.radians(90)))
        bpy.context.object.data.materials.append(arch_glow)
    bpy.ops.mesh.primitive_cube_add(size=1, location=c + n * 0.01, rotation=(0, 0, yaw))
    f = bpy.context.object
    f.scale = (0.04, w + 0.16, h + 0.16)
    f.data.materials.append(frame_mat)
    for sx, sz in ((0.05, h), (w, 0.05)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=c + n * 0.06, rotation=(0, 0, yaw))
        mu = bpy.context.object
        mu.scale = (0.03, sx, sz)
        mu.data.materials.append(frame_mat)


def building(key, name, loc, yaw, s, mat, windows):
    """windows: list of (local face normal (x,y), [(u, z, arch?), ...]) with u in -1..1 along the face."""
    bpath, rpath = BUILD[key]
    b = load(bpath, name)
    set_all(b, mat)
    sx, sy, sz = b["size"]
    place(b, Vector(loc), yaw, s)
    if rpath:
        r = load(rpath, name + "Roof")
        set_all(r, roof_mats[len(name) % 3])
        place(r, Vector(loc) + Vector((0, 0, sz * s - 0.03)), yaw, s * 1.07)
    bpy.context.view_layer.update()
    rot = b.matrix_world.to_3x3().normalized()
    for (nx, ny), items in windows:
        n_local = Vector((nx, ny, 0))
        half_n = (sx if nx else sy) * s / 2
        half_t = (sy if nx else sx) * s / 2
        t_local = Vector((-ny, nx, 0))
        for u, z, arch in items:
            p = Vector(loc) + rot @ (n_local * half_n + t_local * u * half_t * 0.75) + Vector((0, 0, z))
            nw = rot @ n_local
            dg = bpy.context.evaluated_depsgraph_get()
            hit, hl, hn, _, ho, _ = scene.ray_cast(dg, p + nw * 4, -nw)
            if not hit:
                continue
            p, nw = hl, Vector((hn.x, hn.y, 0)).normalized()
            window(p, nw, 0.75, 1.0, arch)
            if True:
                point(f"{name}L{u}", p + nw * 1.0, (1.0, 0.65, 0.3), 60, 0.4)
    return b


# Buildings face into the square: their window wall (local -Y) turned ~30 deg toward the camera.
F = (0, -1)
building("g2", "LeftHouse", (-7.6, 13.0, 0), 32, 2.0, wall_mats[0],
         [(F, [(-0.55, 4.2, False), (0.15, 4.2, False), (0.75, 4.2, False), (-0.55, 1.6, False), (0.45, 1.6, False)])])
building("ms", "LeftFar", (-13.0, 7.5, 0), 40, 1.7, wall_mats[2],
         [(F, [(-0.5, 6.3, False), (0.5, 6.3, False), (-0.5, 3.4, False), (0.5, 3.4, False), (0.0, 9.0, False)])])
building("g1", "BackLeft", (-5.2, 21.0, 0), 10, 2.4, wall_mats[1],
         [(F, [(-0.5, 3.8, False), (0.5, 3.8, False)])])
building("oh", "RightBlock", (7.6, 12.5, 0), -32, 1.9, wall_mats[1],
         [(F, [(-0.55, 1.8, True), (0.55, 1.8, True), (-0.55, 5.0, True), (0.55, 5.0, False)])])
building("ms", "RightFar", (13.0, 7.5, 0), -40, 1.6, wall_mats[0],
         [(F, [(-0.5, 3.2, True), (0.5, 3.2, False), (-0.5, 6.4, False), (0.5, 6.4, True)])])
building("g3", "BackRight", (5.4, 21.0, 0), -10, 2.4, wall_mats[2],
         [(F, [(-0.5, 3.8, False), (0.5, 3.8, False)])])

# cyan street lamps either side
for k, (x, y) in enumerate([(-5.6, 7.5), (5.8, 8.5)]):
    l = load("Models/Modular Kit/Street Props/streetLamp_01.fbx", f"Lamp{k}")
    set_all(l, lamp_metal)
    place(l, (x, y, 0), 0, 1.0)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.2, location=(x, y, 3.2))
    bpy.context.object.data.materials.append(lamp_glow)
    point(f"LampL{k}", (x, y, 3.15), (0.55, 0.95, 1.0), 120, 0.3)


# ---------------------------------------------------------------- ghosts on scooters
def paper_bag(name, color, loc, rot, size=0.36):
    m = principled(name + "Mat", color, 0.85, emit=color, emit_strength=0.25)
    bpy.ops.mesh.primitive_cube_add(size=1)
    b = bpy.context.object
    b.name = name
    b.scale = (size * 0.85, size * 0.5, size * 1.1)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for v in b.data.vertices:          # slight taper + crumpled top
        if v.co.z > 0:
            v.co.x *= 0.92
            v.co.z += 0.06 * math.sin(v.co.x * 9 + v.co.y * 5)
    b.data.materials.append(m)
    b.location = loc
    b.rotation_euler = rot
    # folded top flap
    bpy.ops.mesh.primitive_cube_add(size=1)
    f = bpy.context.object
    f.parent = b
    f.location = (0, 0, 0.56)
    f.scale = (0.9, 0.35, 0.18)
    f.rotation_euler = (math.radians(10), 0, 0)
    f.data.materials.append(m)
    return b


GHOSTS = [
    # body colour, scooter colour, stripe, location, heading (deg, 270 = toward camera), ghost turn
    dict(body=(0.40, 0.26, 1.0), scooter=(0.30, 0.09, 0.05), stripe=(0.20, 0.06, 0.04),
         loc=(-1.85, 1.6, 0), heading=232, gturn=30, lean=16),
    dict(body=(1.0, 0.22, 0.06), scooter=(0.08, 0.14, 0.85), stripe=(0.06, 0.10, 0.6),
         loc=(1.85, 1.9, 0), heading=-52, gturn=-30, lean=16),
]
ghost_roots = []
for i, g in enumerate(GHOSTS):
    sc = load("Models/Vehicle Models/GhostMates.fbx", f"Scooter{i}")
    recolor(sc, "Scooter.", g["scooter"], rough=0.3)
    recolor(sc, "Logo", g["scooter"], rough=0.3)
    recolor(sc, "Stripe", g["stripe"], rough=0.3)
    recolor(sc, "ScooterBorder", g["stripe"], rough=0.3)
    recolor(sc, "Seat", (0.05, 0.03, 0.03), rough=0.5)
    recolor(sc, "Headlights", (1, 0.95, 0.8), emit=(1.0, 0.9, 0.7), emit_strength=60)
    recolor(sc, "RearLight.", (1, 0.1, 0.1), emit=(1.0, 0.1, 0.05), emit_strength=10)
    head = mat_centre(sc, "Headlights")
    seat = mat_centre(sc, "Seat")
    nose_yaw = math.degrees(math.atan2(head.y, head.x))
    S = 0.33
    place(sc, Vector(g["loc"]), g["heading"] - nose_yaw, S)
    # bank outward into the turn, like the poster
    from mathutils import Matrix
    axis = Vector((head.x, head.y, 0)).normalized()
    for sign in (1, -1):
        R_ = Matrix.Rotation(math.radians(g["lean"]) * sign, 4, axis)
        sc.matrix_world = (Matrix.Translation(Vector(g["loc"])) @ Matrix.Rotation(math.radians(g["heading"] - nose_yaw), 4, 'Z')
                           @ R_ @ Matrix.Diagonal((S, S, S, 1)))
        bpy.context.view_layer.update()
        top = sc.matrix_world @ Vector((0, 0, 4))
        if (top.x - g["loc"][0]) * g["loc"][0] > 0:
            break
    bpy.context.view_layer.update()
    seat_w = sc.matrix_world @ seat
    head_w = sc.matrix_world @ head
    fwd = head_w - seat_w; fwd.z = 0; fwd.normalize()
    spot(f"Beam{i}", head_w + fwd * 0.1, head_w + fwd * 6 - Vector((0, 0, 0.9)), (1.0, 0.85, 0.6), 300, 60)
    point(f"HeadGlow{i}", head_w + fwd * 0.25, (1.0, 0.85, 0.6), 40, 0.05)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.11, location=head_w + fwd * 0.05)
    bpy.context.object.data.materials.append(principled(f"Bulb{i}", (1, 0.95, 0.8), 0.2, emit=(1.0, 0.92, 0.75), emit_strength=30))

    gh = load("Models/Ghost Models/GhostEYELID 1.fbx", f"Ghost{i}")
    recolor(gh, "Body", g["body"], emit=g["body"], emit_strength=0.5, rough=0.35)
    recolor(gh, "Horn", (1.0, 0.80, 0.78), rough=0.4)
    eye_yaw = 0.0  # GhostEYELID faces local +X
    place(gh, seat_w + Vector((0, 0, -0.12)) - fwd * 0.05, 0, 0.40)
    gh.rotation_euler.z = math.radians(270 + g["gturn"] - eye_yaw)
    point(f"GhostGlow{i}", seat_w + Vector((0, -1.0, 1.0)), g["body"], 25, 0.5)
    ghost_roots.append((gh, seat_w))

paper_bag("BagYellow", (1.0, 0.82, 0.10), ghost_roots[0][1] + Vector((-0.62, -0.25, 0.70)),
          (math.radians(8), math.radians(-14), math.radians(20)))
paper_bag("BagGreen", (0.30, 0.90, 0.30), ghost_roots[1][1] + Vector((0.15, 0.25, 2.05)),
          (math.radians(-12), math.radians(18), math.radians(-25)), size=0.30)

# ---------------------------------------------------------------- camera
cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
coll.objects.link(cam)
cam.location = (0.0, -3.2, 1.3)
look_at(cam, (0.0, 15, 5.0))
cam.data.lens = 21
scene.camera = cam

bpy.context.view_layer.update()
mpos = cam.matrix_world @ Vector((29, 25, -80))
moon.location = mpos
moon.rotation_euler = cam.rotation_euler
moon.scale = (782 / 701 * 26, 26, 1)
sun.rotation_euler = (cam.location - mpos).to_track_quat('-Z', 'Y').to_euler()

fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'AREA'))
fill.data.shape = 'DISK'; fill.data.size = 12; fill.data.energy = 1100; fill.data.color = (0.45, 0.45, 0.85)
fill.location = (0, -6, 9); coll.objects.link(fill); look_at(fill, (0, 10, 0))
# haze
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 30, 15))
hz = bpy.context.object
hz.name = "Haze"
hz.scale = (120, 80, 30)
hm = bpy.data.materials.new("HazeMat"); hm.use_nodes = True
for n_ in list(hm.node_tree.nodes): hm.node_tree.nodes.remove(n_)
vo = hm.node_tree.nodes.new("ShaderNodeVolumePrincipled")
vo.inputs["Density"].default_value = 0.008
vo.inputs["Color"].default_value = (0.55, 0.5, 0.9, 1)
oo = hm.node_tree.nodes.new("ShaderNodeOutputMaterial")
hm.node_tree.links.new(vo.outputs[0], oo.inputs["Volume"])
hz.data.materials.append(hm)

# ---------------------------------------------------------------- render settings
r = scene.render
r.resolution_x, r.resolution_y, r.resolution_percentage = 1232, 706, 100
r.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'AgX - Punchy' if 'AgX - Punchy' in [i.identifier for i in scene.view_settings.bl_rna.properties['look'].enum_items_static] else 'None'
scene.view_settings.exposure = 0.45
if ENGINE == "cycles":
    r.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for t in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = t
            prefs.get_devices()
            for d in prefs.devices:
                d.use = True
            break
        except Exception:
            pass
    scene.cycles.device = 'GPU'
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
bpy.ops.wm.save_as_mainfile(filepath=ROOT + "tools/keyart/keyart.blend")
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
