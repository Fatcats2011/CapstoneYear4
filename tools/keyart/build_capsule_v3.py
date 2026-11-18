"""Main-capsule concepts (Steam 1232x706), built from the game's own models. The logo goes on afterwards
(composite_v3.py), never rendered.

  blender -b --factory-startup --python tools/keyart/build_capsule_v3.py -- <concept> <out.png> [percent] [samples] [save.blend]

Concepts:
  steal    - two couriers race at the camera; one snatches the other's burning (stolen) package mid-air
  rush     - four couriers stampede down a moonlit street at the camera, packages flying
  phase    - a courier phase-boosts out through a house wall toward the camera
  golden   - four couriers converge on the glowing Golden Order
"""
import bpy, sys, math, os, random
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import capsule_kit as K
import unity_prefab as up

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
CONCEPT = argv[0] if argv else "steal"
OUT = argv[1] if len(argv) > 1 else K.ROOT + f"docs/steam-store/capsule_v3_{CONCEPT}.png"
PERCENT = int(argv[2]) if len(argv) > 2 else 50
SAMPLES = int(argv[3]) if len(argv) > 3 else 32
SAVE = argv[4] if len(argv) > 4 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
PB = "Assets/Prefabs/Buildings/Buildings New/"
R = math.radians


# ---------------------------------------------------------------- environment
def ground(size=400.0, tile=3.2, tint=(0.27, 0.29, 0.37), center=(0, 80, 0)):
    gm = up.material(next(g for g, p in up.guids().items() if p == "Assets/Textures/DoA-Road/RoadMainMap.mat"))
    for n in gm.node_tree.nodes:
        if n.type == 'MAPPING':
            n.inputs["Scale"].default_value = (size / tile, size / tile, 1)
        elif n.type == 'MIX' and n.blend_type == 'MULTIPLY':
            n.inputs[7].default_value = (*tint, 1)
    bpy.ops.mesh.primitive_plane_add(size=size, location=center)
    g = bpy.context.object
    g.name = "Ground"
    g.data.materials.append(gm)
    return g


def body_of(root):
    ks = [o for o in K.kids(root) if o.type == 'MESH'] if "kids" in root else K.all_meshes(root)
    cands = [o for o in K.all_meshes(root)
             if os.path.basename(o.get("unity_src", "")).startswith(("Generic_", "OverHang", "MultiStory", "TiltedTower"))]
    cands = cands or K.all_meshes(root)
    return max(cands, key=lambda o: o.dimensions.x * o.dimensions.y * o.dimensions.z)


def footprint(body):
    bpy.context.view_layer.update()
    pts = [body.matrix_world @ v.co for v in body.data.vertices]
    z0 = min(p.z for p in pts); z1 = max(p.z for p in pts)
    low = [p for p in pts if p.z <= z0 + (z1 - z0) * 0.75]
    return (Vector([min(p[k] for p in low) for k in range(3)]), Vector([max(p[k] for p in low) for k in range(3)]))


def street_row(names, side, x_front, y0, scale, gap=0.0, light=110.0):
    """Houses along +Y on one side of a street. side -1: left (fronts look +X), +1: right (fronts look -X)."""
    y = y0
    out = []
    for i, pf in enumerate(names):
        r = up.import_prefab(PB + pf + ".prefab", f"H{side}_{i}", light_scale=light)
        bpy.context.view_layer.update()
        b0, b1 = footprint(body_of(r))          # prefab at origin, front = +X
        if side < 0:
            r.rotation_euler = (0, 0, 0)
            # world = s*local; front wall at local b1.x -> x_front; local y runs +Y
            r.location = (x_front - scale * b1.x, y - scale * b0.y, 0)
        else:
            r.rotation_euler = (0, 0, R(180))
            # rotated 180: local (x,y) -> (-x,-y); front wall at -s*b1.x; the house's local -y end is furthest
            r.location = (x_front + scale * b1.x, y + scale * b1.y, 0)
        r.scale = (scale,) * 3
        y += scale * (b1.y - b0.y) + gap
        out.append(r)
    return out


def tower(loc, scale, face_deg=0.0, time=None, window_glow=True, face_light=0.0):
    """The game's clock tower prefab. time=(hour, minute) sets the hands on the face looking at the camera
    (-Y), e.g. (3, 48)."""
    r = up.import_prefab(PB + "Clock Tower.prefab", "ClockTower", light_scale=60.0)
    r.location = loc
    r.rotation_euler = (0, 0, R(face_deg))
    r.scale = (scale,) * 3
    bpy.context.view_layer.update()
    tw = next(o for o in K.all_meshes(r) if "Clock Tower_Textured" in o.get("unity_src", ""))
    tm = next((m for m in tw.data.materials if m and m.name.startswith("ClockTower")), None)
    if tm:
        for i in range(len(tw.data.materials)):
            tw.data.materials[i] = tm
        if not window_glow:            # the texture's glow map lights the middle windows green
            tm.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.0
    if time:
        set_clock(r, *time)
    if face_light:
        clock_face_light(r, face_light)
    return r


def clock_face_light(root, energy):
    """A faint, soft light washing the clock face (the camera side) from in front and a little below."""
    src = lambda o: os.path.splitext(os.path.basename(o.get("unity_src", "")))[0]
    ring = min((o for o in K.all_meshes(root) if src(o) == "Clock_Ticks"), key=lambda o: K.bbox([o])[0].y)
    a, b = K.bbox([ring])
    hub = (a + b) / 2
    w = b.x - a.x                                     # the clock's width
    l = K.spot("ClockFaceLight", hub + Vector((0, -w * 6, -w * 1.5)), hub, (0.75, 0.82, 1.0), energy, 24, 1.0)
    l.data.shadow_soft_size = w
    l.data.volume_factor = 0.0                       # no beam through the haze
    return l


def set_clock(root, hour, minute):
    """Swing the hands on the camera-side face round its hub to read hour:minute (as seen from -Y)."""
    src = lambda o: os.path.splitext(os.path.basename(o.get("unity_src", "")))[0]
    meshes = K.all_meshes(root)
    ring = min((o for o in meshes if src(o) == "Clock_Ticks"), key=lambda o: K.bbox([o])[0].y)
    a, b = K.bbox([ring])
    hub = (a + b) / 2
    want = {"Minute_Hand": minute * 6.0, "Hour_Hand": (hour % 12 + minute / 60.0) * 30.0}   # degrees clockwise from 12
    for o in meshes:
        if src(o) not in want:
            continue
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        if abs(sum(p.y for p in pts) / len(pts) - hub.y) > (b.x - a.x):      # a hand on another face
            continue
        tip = max(pts, key=lambda p: (p.x - hub.x) ** 2 + (p.z - hub.z) ** 2)
        now = math.degrees(math.atan2(tip.x - hub.x, tip.z - hub.z))
        c = Vector((hub.x, o.matrix_world.translation.y, hub.z))
        o.matrix_world = (Matrix.Translation(c) @ Matrix.Rotation(R(want[src(o)] - now), 4, 'Y')
                          @ Matrix.Translation(-c) @ o.matrix_world)
        bpy.context.view_layer.update()
        print("CLOCK", src(o), round(now, 1), "->", want[src(o)])
        for i, m in enumerate(o.data.materials):        # brighter than the ticks, so the time reads
            if m:
                m2 = m.copy()
                bsdf = m2.node_tree.nodes.get("Principled BSDF")
                if bsdf:
                    bsdf.inputs["Emission Strength"].default_value = max(3.0, bsdf.inputs["Emission Strength"].default_value * 3)
                o.data.materials[i] = m2


def street_lamp(loc, scale=0.9, color=(0.22, 0.80, 1.0), energy=90):
    l = K.load("Models/Modular Kit/Street Props/streetLamp_01.fbx", "Lamp")
    metal = K.principled("LampMetal", (0.05, 0.06, 0.08), 0.5)
    for o in K.kids(l):
        if o.type == 'MESH':
            o.data.materials.clear(); o.data.materials.append(metal)
    mn, mx = K.bbox(K.kids(l))
    h = mx.z - mn.z
    l.location = loc
    l.scale = (scale * 3.2 / h,) * 3
    top = Vector(loc) + Vector((0, 0, 3.2 * scale - 0.25 * scale))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.26 * scale, location=top)
    gl = bpy.context.object
    gl.scale = (1, 1, 1.25)
    gl.data.materials.append(K.principled("LampGlow", (0, 0, 0), 0.3, emit=color, emit_strength=6))
    K.point("LampLight", top - Vector((0, 0, 0.2)), color, energy * scale, 0.3)
    return l


def tree(kind, loc, height, rot=0):
    r = up.import_prefab(f"Assets/Prefabs/Buildings/NEW_Trees/Tree_{kind}.prefab", "Tree" + kind)
    bpy.context.view_layer.update()
    mn, mx = K.bbox(K.all_meshes(r))
    s = height / (mx.z - mn.z)
    r.scale = (s,) * 3
    r.rotation_euler = (0, 0, R(rot))
    r.location = loc
    return r


def flame_ball(name, loc, size, deep=(0.45, 0.05, 0.95), hot=(0.97, 0.7, 1.0), strength=6.0, cam=None):
    """The stolen package's purple flame: an emissive, noise-shaped soft blob around a point."""
    m = bpy.data.materials.new(name + "Mat"); m.use_nodes = True
    N, L = m.node_tree.nodes, m.node_tree.links
    for n_ in list(N): N.remove(n_)
    out = N.new("ShaderNodeOutputMaterial")
    tc = N.new("ShaderNodeTexCoord")
    noise = N.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 3.0; noise.inputs["Detail"].default_value = 6
    mp = N.new("ShaderNodeMapping"); mp.inputs["Location"].default_value = (-1, -1, 0); mp.inputs["Scale"].default_value = (2, 2, 1)
    grad = N.new("ShaderNodeTexGradient"); grad.gradient_type = 'SPHERICAL'
    sepu = N.new("ShaderNodeSeparateXYZ")
    # stretch upward: fade more slowly above the middle
    L.new(tc.outputs["UV"], mp.inputs["Vector"])
    L.new(mp.outputs["Vector"], grad.inputs["Vector"])
    L.new(tc.outputs["UV"], noise.inputs["Vector"])
    mul = N.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'
    L.new(grad.outputs["Fac"], mul.inputs[0])
    nm = N.new("ShaderNodeMapRange"); nm.inputs["From Min"].default_value = 0.3; nm.inputs["From Max"].default_value = 0.7
    nm.inputs["To Min"].default_value = 0.4; nm.inputs["To Max"].default_value = 1.6
    L.new(noise.outputs["Fac"], nm.inputs["Value"])
    L.new(nm.outputs["Result"], mul.inputs[1])
    cl = N.new("ShaderNodeMath"); cl.operation = 'MINIMUM'; cl.inputs[1].default_value = 1.0
    L.new(mul.outputs[0], cl.inputs[0])
    ramp = N.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (*deep, 1)
    ramp.color_ramp.elements[1].position = 0.9; ramp.color_ramp.elements[1].color = (*hot, 1)
    L.new(cl.outputs[0], ramp.inputs["Fac"])
    em = N.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = strength
    L.new(ramp.outputs["Color"], em.inputs["Color"])
    a = N.new("ShaderNodeMapRange"); a.inputs["From Min"].default_value = 0.08; a.inputs["From Max"].default_value = 0.6
    L.new(cl.outputs[0], a.inputs["Value"])
    tr = N.new("ShaderNodeBsdfTransparent"); mx = N.new("ShaderNodeMixShader")
    L.new(a.outputs["Result"], mx.inputs["Fac"]); L.new(tr.outputs[0], mx.inputs[1]); L.new(em.outputs[0], mx.inputs[2])
    L.new(mx.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    bpy.ops.mesh.primitive_plane_add(size=1)
    d = bpy.context.object
    d.name = name
    d.data.materials.append(m)
    d.location = loc
    camloc = scene.camera.location if cam is None else cam.location
    d.rotation_euler = (Vector(camloc) - Vector(loc)).to_track_quat('Z', 'Y').to_euler()
    d.scale = (size, size * 1.25, 1)
    d.visible_shadow = False
    K.point(name + "Light", Vector(loc), deep, 120 * size, size * 0.3)
    return d


def flame_cards(name, center, height, cam, strength=5.0, light=True):
    """The stolen package's purple flame: three painted flame sprites (flame_sprite.py) on camera-facing
    cards round a point: one big behind, two smaller either side and a little in front."""
    to_cam = (cam.location - Vector(center)).normalized()
    side = to_cam.cross(Vector((0, 0, 1))).normalized()
    cards = [(0, Vector((0, 0, 0)) - to_cam * 0.35 * height, 1.0),
             (1, side * 0.32 * height + to_cam * 0.12 * height - Vector((0, 0, 0.08 * height)), 0.7),
             (2, -side * 0.30 * height + to_cam * 0.1 * height - Vector((0, 0, 0.1 * height)), 0.75)]
    for idx, off, sc in cards:
        m = bpy.data.materials.new(f"{name}{idx}Mat"); m.use_nodes = True
        N, L = m.node_tree.nodes, m.node_tree.links
        for n_ in list(N): N.remove(n_)
        out = N.new("ShaderNodeOutputMaterial")
        tex = N.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(HERE, "flames", f"flame_{idx % 3}.png"), check_existing=True)
        tex.extension = 'CLIP'
        em = N.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = strength
        tr = N.new("ShaderNodeBsdfTransparent"); mx = N.new("ShaderNodeMixShader")
        L.new(tex.outputs["Color"], em.inputs["Color"]); L.new(tex.outputs["Alpha"], mx.inputs["Fac"])
        L.new(tr.outputs[0], mx.inputs[1]); L.new(em.outputs[0], mx.inputs[2]); L.new(mx.outputs[0], out.inputs["Surface"])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
        bpy.ops.mesh.primitive_plane_add(size=1)
        d = bpy.context.object
        d.name = f"{name}{idx}"
        d.data.materials.append(m)
        h = height * sc
        # the sprite's base sits a little under the centre, its tongues licking up
        d.location = Vector(center) + off + Vector((0, 0, h * 0.28))
        d.rotation_euler = (cam.location - d.location).to_track_quat('Z', 'Y').to_euler()
        d.scale = (h * 256 / 384, h, 1)
        d.visible_shadow = False
    if light:
        fl = K.point(name + "Light", Vector(center) + to_cam * 0.3, (0.55, 0.15, 1.0), 260 * height, 0.2)
        fl.data.use_shadow = False     # a glow, not a lamp: its shadows only darkened the package


def streaks(name, start, direction, count, length, spread, color, strength=3.0, seed=1, width=0.02):
    """Thin glowing speed lines behind a rider."""
    rnd = random.Random(seed)
    m = K.principled(name + "Mat", (0, 0, 0), 0.5, emit=color, emit_strength=strength)
    d = Vector(direction).normalized()
    for i in range(count):
        off = Vector((rnd.uniform(-spread, spread), rnd.uniform(-spread * 0.3, spread * 0.3), rnd.uniform(-spread * 0.5, spread * 0.5)))
        p0 = Vector(start) + off + d * rnd.uniform(0, length * 0.5)
        L = length * rnd.uniform(0.4, 1.0)
        bpy.ops.mesh.primitive_cylinder_add(radius=width * rnd.uniform(0.5, 1.2), depth=L, vertices=6,
                                            location=p0 + d * L / 2)
        c = bpy.context.object
        c.name = f"{name}{i}"
        c.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
        c.data.materials.append(m)
        c.visible_shadow = False


def place_hat(gh, kind, scale, lift=0.0, back=0.0, tilt=(0, 0, 0)):
    """Sit a hat on the ghost's head (between the horns). scale: hat size relative to the ghost's scale."""
    h = K.hat(gh.name + "Hat", kind)
    gm = next(o for o in K.kids(gh) if o.type == 'MESH')
    bpy.context.view_layer.update()
    gs = gm.matrix_world.to_3x3().col[0].length
    top = K.ghost_top(gh)
    mn, mx = K.bbox(K.kids(h))
    piv = bpy.data.objects.new(h.name + "Piv", None)
    K.coll().objects.link(piv)
    bpy.context.view_layer.update()
    h.parent = piv
    h.location = (-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)
    gw = gm.matrix_world.to_3x3().normalized()
    piv.matrix_world = (Matrix.Translation(top + gw @ Vector((-back, 0, lift)) * gs)
                        @ gw.to_4x4() @ Matrix.Rotation(R(tilt[0]), 4, 'X') @ Matrix.Rotation(R(tilt[1]), 4, 'Y')
                        @ Matrix.Diagonal((gs * scale, gs * scale, gs * scale, 1)))
    return piv


def ride(name, gcol, scol, loc, heading, s, pitch=0.0, roll=0.0, pivot=None, glow=0.5, headlight=1500.0):
    """A rider at loc, nose along heading (deg, 90 = +Y away from camera, -90 = at a camera on -Y), scale s.
    pitch: + = nose up (wheelie) about the back wheel. roll: lean (+ = to its right)."""
    piv = Vector((pivot if pivot is not None else (-2.6 if pitch > 0 else 3.0), 0, 0))
    M = (Matrix.Translation(Vector(loc)) @ Matrix.Rotation(R(heading), 4, 'Z') @ Matrix.Rotation(R(roll), 4, 'X')
         @ Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation(piv) @ Matrix.Rotation(R(-pitch), 4, 'Y') @ Matrix.Translation(-piv))
    return K.rider(name, K.GHOST[gcol], K.SCOOTER[scol], M, glow=glow, headlight=headlight)


def rack_package(info, size, kind="Medium", offset=(0, 0, 0), rot=(0, 0, 0), name="Pkg"):
    """A package riding on the scooter's back, behind the ghost."""
    p = K.package(name, kind)
    f, u = info["fwd"], info["up"]
    loc = info["seat"] - f * 0.55 * info["scale"] * 3 + u * 0.8 * info["scale"] * 3 + Vector(offset)
    return K.fit(p, size, loc, rot)


# ---------------------------------------------------------------- concepts
def concept_steal():
    """Low camera on the street, two riders racing at it side by side. A stolen package, burning purple,
    flies off purple's rack; orange leans over and grabs for it. Clock tower and moon behind."""
    ground()
    street_row(["Large Generic House 1", "Overhang Building 1", "Medium Generic House 1", "Multistory Building 1",
                "Small Generic House 2", "Large Generic House 2"], -1, -7.0, -2.0, 0.85)
    street_row(["Tilted Tower Building 1", "Medium Generic House 2", "Large Generic House 2", "Overhang Building 1",
                "Small Generic House 1", "Medium Generic House 1"], 1, 7.0, -2.0, 0.85)
    tower((1.5, 58, 0), 0.75, face_deg=0)
    for y in (8, 22, 36):
        street_lamp((-5.6, y, 0), 0.95)
        street_lamp((5.6, y + 7, 0), 0.95)
    s = 0.40
    scP, ghP, iP = ride("Purple", "purple", "ghostmates", (-0.25, 2.6, 0), -102, s, pitch=5, roll=-5)
    scO, ghO, iO = ride("Orange", "orange", "doordeath", (2.35, 3.6, 0), -80, s, pitch=10, roll=-16)
    place_hat(ghP, "top", 0.42, lift=-0.25, tilt=(0, -10, 0))
    place_hat(ghO, "propeller", 0.48, lift=-0.2)
    pk_loc = Vector((1.05, 2.55, 2.05))
    K.fit(K.package("Stolen", "Large"), 0.62, pk_loc - Vector((0, 0, 0.3)), (12, -25, 22))
    K.reach(ghO, pk_loc + Vector((0.25, -0.15, 0.05)), side=-1, spin=40)
    cam = K.camera((0.6, -4.6, 0.75), (0.6, 8, 2.2), 21)
    flame_ball("Flame", pk_loc + Vector((0, 0.15, 0.05)), 1.25, cam=cam)
    K.night_world()
    K.moon(cam, (17, 11.0, -70), 12)
    K.sun("Moonlight", (-0.3, -1, -0.7), (0.55, 0.62, 1.0), 1.5)
    K.sun("SkyFill", (0, 0.3, -1), (0.42, 0.50, 1.0), 0.6, angle=30)
    K.area("Rim", (0, 12, 6), (0.5, 3, 1.5), (0.6, 0.4, 1.0), 900, 10)
    K.area("Key", (-3, -4, 5), (0.5, 3, 1.5), (0.55, 0.62, 1.0), 450, 6)
    K.haze((0, 40, 10), (80, 90, 25), 0.0035)


def concept_rush():
    """Four couriers stampede down the street at the camera, packages flying off the racks.
    Riders fill the right two-thirds; the left third (dark house fronts) is kept for the logo."""
    ground()
    street_row(["Large Generic House 1", "Overhang Building 1", "Medium Generic House 1", "Multistory Building 1",
                "Small Generic House 2", "Large Generic House 2", "Medium Generic House 2"], -1, -8.0, -4.0, 0.9)
    street_row(["Tilted Tower Building 1", "Medium Generic House 2", "Large Generic House 2", "Overhang Building 1",
                "Small Generic House 1", "Medium Generic House 1", "Multistory Building 1"], 1, 8.0, -4.0, 0.9)
    tower((2.5, 70, 0), 0.8)
    for y in (6, 20, 34, 48):
        street_lamp((-6.5, y, 0), 1.0)
        street_lamp((6.5, y + 7, 0), 1.0)
    s = 0.42
    _, g1, i1 = ride("Lead", "purple", "ghostmates", (1.55, 2.2, 0), -104, s, pitch=17, roll=-4)    # wheelie, front
    _, g2, i2 = ride("Left", "green", "ubercreeps", (-0.9, 6.2, 0), -84, s, pitch=4, roll=-14)
    _, g3, i3 = ride("Right", "orange", "doordeath", (4.2, 5.4, 0.45), -112, s, pitch=-7, roll=12)   # landing a jump
    _, g4, i4 = ride("Back", "yellow", "skip", (1.9, 10.5, 0), -92, s, pitch=3, roll=6)
    place_hat(g1, "top", 0.42, lift=-0.25, tilt=(0, -8, 0))
    place_hat(g2, "bucket", 0.42, lift=-0.25)
    place_hat(g3, "propeller", 0.48, lift=-0.2)
    place_hat(g4, "conical", 0.45, lift=-0.2)
    rack_package(i1, 0.7, "Medium", rot=(0, 0, 10))
    K.fit(K.package("Fly1", "Small"), 0.5, (0.2, 4.4, 2.9), (30, 15, 40))
    K.fit(K.package("Fly2", "Large"), 0.55, (3.4, 8.0, 3.0), (-20, 35, -10))
    cam = K.camera((-0.4, -4.8, 0.6), (1.2, 10, 2.4), 19)
    K.night_world()
    K.moon(cam, (19, 12.5, -70), 12)
    K.sun("Moonlight", (-0.3, -1, -0.7), (0.55, 0.62, 1.0), 1.5)
    K.sun("SkyFill", (0, 0.3, -1), (0.42, 0.50, 1.0), 0.6, angle=30)
    K.area("Rim", (1, 16, 6), (1.5, 5, 1.5), (0.6, 0.4, 1.0), 1200, 12)
    K.area("Key", (3, -4, 5), (1.5, 4, 1.5), (0.55, 0.62, 1.0), 450, 6)
    K.haze((0, 45, 10), (80, 100, 25), 0.0035)


# Shot constants for the hero concept (tuned by eye; see composite_v3.py for the logo spot)
GHOST_GLOW = 0.3          # how much the ghost bodies light themselves (was 0.6)
HERO = dict(
    cam=(0.9, -2.55, 1.6), aim=(1.2, 9, 2.0), lens=20, shift=(-0.115, 0.04),
    purple=dict(loc=(0.35, 2.5, 0), heading=-124, pitch=7, roll=-6, scooter="ubercreeps"),
    orange=dict(loc=(2.75, 3.4, 0), heading=-74, pitch=11, roll=-18, scooter="ghostmates"),
    stolen_lift=(0.92, 1.12), stolen_size=1.05, flame=1.45, orange_twist=-18,
    # ghost, scooter, hat, loc, heading, pitch, roll, twist (ghost turned toward the camera on its seat)
    chasers=[("green", "skip", "chupalla", (-2.6, 7.2, 0), -68, 4, -12, 0),
             ("yellow", "doordeath", "top", (-2.4, 14.0, 3.3), -145, 8, 8, 62)],      # mid-jump
    moon=(8.6, 18.1, -40), moon_size=6.0, purple_twist=40, speed=5.0, clock_light=4000.0, tower=(3.0, 58, 0), right_row_x=8.5,
)


def concept_hero(over=None):
    """The steal, up close, with the race behind it: purple (centre) has a burning stolen package knocked
    off its rack, orange (right) leans across and grabs for it, two more couriers chase down the street.
    Clock tower and moon behind; the left third (house fronts) is left for the logo."""
    H = dict(HERO, **(over or {}))
    ground(tint=(0.20, 0.21, 0.29))
    y0, pre_l, pre_r = H.get("row_y0", -3.0), H.get("row_pre_left", []), H.get("row_pre_right", [])
    street_row(pre_l + ["Large Generic House 1", "Overhang Building 1", "Medium Generic House 1", "Multistory Building 1",
                "Small Generic House 2", "Large Generic House 2"], -1, -6.5, y0, 0.85)
    street_row(["Tilted Tower Building 1", "Medium Generic House 2", "Large Generic House 2", "Overhang Building 1",
                "Small Generic House 1", "Medium Generic House 1"], 1, H["right_row_x"], y0, 0.85)
    tower(H["tower"], 0.75, time=(3, 48), window_glow=False,      # its green windows showed through purple
          face_light=H["clock_light"])
    if H.get("strip_near_lamps_y") is not None:      # wall lanterns right by the camera read as big gold frames
        cut = H["strip_near_lamps_y"]
        for o in list(bpy.data.objects):
            if o.type == 'MESH' and "wallLamp" in o.get("unity_src", "") and o.matrix_world.translation.y < cut:
                bpy.data.objects.remove(o, do_unlink=True)
        for o in list(bpy.data.objects):
            if o.type == 'LIGHT' and o.parent and o.matrix_world.translation.y < cut and abs(o.matrix_world.translation.x) > 4:
                bpy.data.objects.remove(o, do_unlink=True)
    for y in H.get("lamps_y", (9, 23, 37)):
        street_lamp((-5.2, y, 0), 0.95)
        if y > 9:                      # the nearest one would show through orange (the ghosts are see-through)
            street_lamp((H["right_row_x"] - 1.4, y + 7, 0), 0.95)
    env = [o for o in bpy.data.objects if o.parent is None]
    s = 0.40
    P, O = H["purple"], H["orange"]
    scP, ghP, iP = ride("Purple", "purple", P["scooter"], P["loc"], P["heading"], s, pitch=P["pitch"], roll=P["roll"], glow=GHOST_GLOW)
    scO, ghO, iO = ride("Orange", "orange", O["scooter"], O["loc"], O["heading"], s, pitch=O["pitch"], roll=O["roll"], glow=GHOST_GLOW)
    K.twist(ghP, iP, H["purple_twist"])
    K.twist(ghO, iO, H["orange_twist"])
    # purple goes bare-headed; hats go on after any turn, exactly as the game seats them
    for o in K.kids(ghP):                     # purple's horn threw a shadow across the package
        if o.type == 'MESH':
            o.visible_shadow = False
    for m in K.mats_of(ghP).values():         # purple, front and centre, is a touch more solid than the rest
        if m.name.startswith("Body"):
            m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = 0.95
    K.game_hat(ghO, "propeller")
    ghosts = [(ghP, "purple"), (ghO, "orange")]
    riders = [(scP, iP, True), (scO, iO, True)]          # (scooter, info, on the ground)
    # the stolen package: lifted off purple's rack (toward orange) with orange's hand on it
    rack = iP["seat"] - iP["fwd"] * 0.66 + iP["up"] * 0.75
    toward = (iO["seat"] - iP["seat"]).normalized()
    pk = rack + toward * H["stolen_lift"][0] + Vector((0, 0, H["stolen_lift"][1]))
    # the game's regular yellow package (MediumPackage), as it is
    stolen = K.fit(K.package("Stolen", "Medium"), H["stolen_size"], pk - Vector((0, 0, H["stolen_size"] / 2)),
                   (12, -25, 22 + 180))        # turned round 180 degrees (about the upright)
    for o in K.all_meshes(stolen):
        o.visible_shadow = False     # its material's cut-out shadow stippled everything round the flame's light
        for m in o.data.materials:   # the .mat tint is a Unity (sRGB) colour: convert, or the yellow fades to cream
            for n in (m.node_tree.nodes if m and m.use_nodes else []):
                if n.type == 'MIX' and n.blend_type == 'MULTIPLY':
                    c = n.inputs[7].default_value
                    n.inputs[7].default_value = (*K.srgb(tuple(c[:3])), 1)
                elif n.type == 'BSDF_PRINCIPLED' and not n.inputs["Base Color"].links:
                    c = n.inputs["Base Color"].default_value
                    n.inputs["Base Color"].default_value = (*K.srgb(tuple(c[:3])), 1)
    K.spot("PackageLight", pk + (Vector(H["cam"]) - pk).normalized() * 2.0 + Vector((0, 0, 1.6)), pk,
           (1.0, 0.88, 0.6), 600, 18, 0.8).data.use_shadow = False      # shows the bag's own yellow
    to_cam = (Vector(H["cam"]) - pk).normalized()
    bag = pk - Vector((0, 0, H["stolen_size"] / 2))                  # the bag's middle (fit() puts its centre here)
    cam = K.camera(H["cam"], H["aim"], H["lens"], H["shift"])          # after env, so it rides along (sharp riders)
    bag_down = (stolen.matrix_world.to_3x3().normalized() @ Vector((0, 0, -1))).normalized()   # the bag is tilted
    bag_up = -bag_down
    # orange snatches it by the handle; purple (its right hand off the grip, where it sat in front of its face)
    # holds on from underneath: its hand flat against the bag's bottom
    pts = [o.matrix_world @ v.co for o in K.all_meshes(stolen) for v in o.data.vertices]
    top = max(p_.dot(bag_up) for p_ in pts)
    rim = [p_ for p_ in pts if p_.dot(bag_up) > top - H["stolen_size"] * 0.12]          # the handle and anything poking out
    near = sorted(rim, key=lambda p_: p_.dot(toward))[:max(3, len(rim) // 2)]           # the purple-side half: the handle
    handle = sum(near, Vector()) / len(near)
    print("HANDLE", tuple(round(x, 3) for x in handle), len(rim), len(near))
    K.reach(ghO, handle - bag_up * H["stolen_size"] * 0.05, side=-1, spin=0)        # the handle runs into the fist
    K.grab(ghP, -1, K.all_meshes(stolen), bag, bag_down + to_cam * 0.15 - toward * 0.15, overlap=0.3,
           shallow_to=to_cam)          # the hand's shallow end toward the camera
    for i, (gc, scn, hk, loc, hd, pt, rl, tw) in enumerate(H["chasers"]):
        sc_, g, ii = ride(f"Chaser{i}", gc, scn, loc, hd, s, pitch=pt, roll=rl, glow=GHOST_GLOW)
        riders.append((sc_, ii, loc[2] < 0.1))
        if gc in H.get("look_at_bag", ()):      # turn the ghost on its seat to look at the package
            d = bag - ii["seat"]; f = ii["fwd"]
            tw = math.degrees(math.atan2(f.x * d.y - f.y * d.x, f.x * d.x + f.y * d.y))
            tw = max(-80.0, min(80.0, tw * 2.2))      # overstated: the eyes sit on the face front, so a true aim barely reads
            print("LOOK", gc, round(tw, 1))
        if tw:
            K.twist(g, ii, tw)
        K.game_hat(g, hk)
        ghosts.append((g, gc))
    for g, gc in ghosts:
        ghost_glow(g, gc, cam, size={"green": 1.05}.get(gc, 1.5))
        hat_light(g, cam, {"orange": 500.0}.get(gc, 250.0))
        if gc == "yellow":                 # far back in the haze: a soft light on its face so it doesn't go murky
            face_light(g, cam, 590.0)
    for sc_, ii, grounded in riders:
        spin_wheels(sc_, ii, H["speed"])
        headlight_glow(ii, cam)
        if grounded:
            ground_shadow(ii)
    K.night_world()
    K.moon(cam, H["moon"], H["moon_size"])
    K.sun("Moonlight", (-0.3, -1, -0.7), (0.55, 0.62, 1.0), 1.0)
    K.sun("SkyFill", (0, 0.3, -1), (0.42, 0.50, 1.0), 0.3, angle=30)
    K.area("Rim", (1, 11, 5), (1.2, 3, 1.5), (0.6, 0.4, 1.0), 1100, 10)
    K.spot("Key", (-1.5, -3.5, 4.5), (1.4, 3, 2.0), (0.62, 0.68, 1.0), 2600, 32, 0.9)
    K.spot("KeyChasers", (-4, -2, 5), (-2.4, 9, 1.6), (0.62, 0.68, 1.0), 2000, 18, 0.9)
    K.haze((0, 40, 10), (80, 90, 25), 0.0011)
    tracking_blur(env, H["speed"])
    if os.environ.get("RAY"):                  # debug: what is under a frame point (fractions from top-left)
        fx, fy = [float(v) for v in os.environ["RAY"].split(",")]
        bpy.context.view_layer.update()
        fr = cam.data.view_frame(scene=scene)            # tr, br, bl, tl in camera space
        tl, tr_, bl = fr[3], fr[0], fr[2]
        pnt = tl + (tr_ - tl) * fx + (bl - tl) * fy
        d_ = (cam.matrix_world.to_3x3() @ pnt).normalized()
        o_ = cam.matrix_world.translation.copy()
        for _ in range(8):                               # look past the haze volume's walls
            hit = scene.ray_cast(bpy.context.evaluated_depsgraph_get(), o_, d_)
            if not hit[0] or not hit[4].name.startswith("Haze"):
                break
            o_ = hit[1] + d_ * 0.01
        print("RAYHIT", hit[0], hit[4].name if hit[4] else None, hit[4].get("unity_src") if hit[4] else None, tuple(round(x, 2) for x in hit[1]))
        lights = sorted(((o.name, round((o.matrix_world.translation - hit[1]).length, 2)) for o in bpy.data.objects if o.type == 'LIGHT'), key=lambda t: t[1])[:5]
        print("NEARLIGHTS", lights)
    if os.environ.get("CLOSEUP"):
        who = os.environ["CLOSEUP"]
        tgt = pk
        for g, gc in ghosts:
            if gc == who:
                mn, mx = K.bbox([o for o in K.kids(g) if o.type == 'MESH'])
                tgt = (mn + mx) / 2
        c2 = K.camera(cam.location + (tgt - cam.location) * 0.5, tgt, 70)
        scene.camera = c2
    report(cam, Purple=iP["seat"], Orange=iO["seat"], Stolen=pk, Green=Vector(H["chasers"][0][3]), Yellow=Vector(H["chasers"][1][3]), MoonC=bpy.data.objects["Moon"].location,
           TowerTop=Vector(H["tower"]) + Vector((0, 0, 30)))


def ghost_glow(gh, color_name, cam, size=1.5, strength=1.0, alpha=0.1, light=5.0):
    """The ghost glows in its own colour: a soft halo just behind it and a light of that colour in front."""
    gm = [o for o in K.kids(gh) if o.type == 'MESH']
    mn, mx = K.bbox(gm)
    c = (mn + mx) / 2
    h = max(mx - mn)
    col = K.GHOST[color_name]
    bright = tuple(min(1.0, v / max(col) * 1.0) for v in col)      # full-strength hue for the light
    to_cam = (cam.location - c).normalized()
    K.halo_disc(gh.name + "Halo", c - to_cam * h * 0.45, h * size, bright, strength, alpha, cam.location, 1.6)
    K.point(gh.name + "GlowLight", c + to_cam * h * 0.5, bright, light * h, h * 0.3)


def hat_light(gh, cam, energy=500.0):
    """A small neutral light from above the camera onto the ghost's hat, so the hat shows its own colours
    instead of the night's purple."""
    hats = [o for o in gh.children if "Hat" in o.name and o.type == 'MESH']
    if not hats:
        return
    mn, mx = K.bbox(hats)
    c = (mn + mx) / 2
    h = max(mx - mn)
    to_cam = (cam.location - c).normalized()
    s = K.spot(gh.name + "HatLight", c + to_cam * h * 2.5 + Vector((0, 0, h * 2.0)), c, (1.0, 0.97, 0.92),
               energy * h * h, 22, 0.8)
    s.data.use_shadow = False          # its soft shadow left grain on the see-through bodies
    s.data.volume_factor = 0.0


def face_light(gh, cam, energy):
    """A soft light from above the camera side onto the ghost's body (its hat shades the face under it)."""
    gm = [o for o in K.kids(gh) if o.type == 'MESH']
    mn, mx = K.bbox(gm)
    c = (mn + mx) / 2
    h = max(mx - mn)
    to_cam = (cam.location - c).normalized()
    s = K.spot(gh.name + "FaceLight", c + to_cam * h * 2.2 + Vector((0, 0, h * 2.4)), c, (1.0, 0.97, 0.9), energy * h * h, 25, 0.9)
    s.data.shadow_soft_size = h * 0.15     # casts the hat brim's shadow onto the face
    s.data.volume_factor = 0.0             # no visible beam in the haze


def spin_wheels(sc, info, speed, fps=24):
    """Turn the scooter's two wheels (their own objects in GhostMates.fbx) at road speed across the shutter, so
    they blur like the street instead of looking parked."""
    M = info["M"]
    R3 = M.to_3x3().normalized()
    for o in K.kids(sc):
        if o.type != 'MESH' or not any(m and m.name.startswith("Tires") for m in o.data.materials):
            continue
        mn, mx = K.bbox([o])
        c = (mn + mx) / 2
        r = (mx.z - mn.z) / 2
        piv = bpy.data.objects.new(o.name + "Axle", None)
        K.coll().objects.link(piv)
        piv.rotation_mode = 'QUATERNION'
        piv.matrix_world = Matrix.Translation(c) @ R3.to_4x4()
        bpy.context.view_layer.update()
        mw = o.matrix_world.copy(); o.parent = piv; o.matrix_world = mw
        step = speed / max(r, 1e-3) / fps          # radians per frame, rolling toward the camera (about the axle, +Y)
        base = piv.rotation_quaternion.copy()
        from mathutils import Quaternion
        for f in (0, 1, 2):
            piv.rotation_quaternion = base @ Quaternion((0, 1, 0), step * (f - 1))
            piv.keyframe_insert("rotation_quaternion", frame=f)


def headlight_glow(info, cam):
    """A small warm glow on the headlight so it reads as a light, not a white disc."""
    s = info["scale"]
    K.halo_disc("HeadHalo", info["head"] + (cam.location - info["head"]).normalized() * 0.15, 1.1 * s,
                (1.0, 0.88, 0.65), 1.6, 0.35, cam.location, 3.0)


def ground_shadow(info, darkness=0.9):
    """A soft dark blot on the road under the scooter, so it sits on the ground."""
    m = bpy.data.materials.new("GroundShadow"); m.use_nodes = True
    N, L = m.node_tree.nodes, m.node_tree.links
    for n_ in list(N): N.remove(n_)
    out = N.new("ShaderNodeOutputMaterial"); tc = N.new("ShaderNodeTexCoord"); mp = N.new("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (-1, -1, 0); mp.inputs["Scale"].default_value = (2, 2, 1)
    gr = N.new("ShaderNodeTexGradient"); gr.gradient_type = 'SPHERICAL'
    pw = N.new("ShaderNodeMath"); pw.operation = 'POWER'; pw.inputs[1].default_value = 0.45
    mu = N.new("ShaderNodeMath"); mu.operation = 'MULTIPLY'; mu.inputs[1].default_value = darkness
    blk = N.new("ShaderNodeEmission"); blk.inputs["Color"].default_value = (0, 0, 0, 1)
    tr = N.new("ShaderNodeBsdfTransparent"); mx = N.new("ShaderNodeMixShader")
    L.new(tc.outputs["UV"], mp.inputs["Vector"]); L.new(mp.outputs["Vector"], gr.inputs["Vector"])
    L.new(gr.outputs["Fac"], pw.inputs[0]); L.new(pw.outputs[0], mu.inputs[0]); L.new(mu.outputs[0], mx.inputs["Fac"])
    L.new(tr.outputs[0], mx.inputs[1]); L.new(blk.outputs[0], mx.inputs[2]); L.new(mx.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    s = info["scale"]
    f = info["fwd"].copy(); f.z = 0; f.normalize()
    c = info["seat"].copy(); c.z = 0.012
    bpy.ops.mesh.primitive_plane_add(size=1, location=c)
    d = bpy.context.object
    d.name = "GroundShadow"
    d.data.materials.append(m)
    d.rotation_euler = (0, 0, math.atan2(f.y, f.x))
    d.scale = (8.6 * s, 3.0 * s, 1)            # about the scooter's footprint
    d.visible_shadow = False


# Steam Library Hero: 3840x1240, no text or logo; faces inside the centre 860x380 safe area. The same street and
# riders as the capsule, seen from further back on a longer lens so the group sits small and central in a wide street.
LIBRARY = dict(
    res=(3840, 1240), cam=(1.25, -13.6, 2.0), aim=(1.25, 9, 2.15), lens=26, shift=(0.0, 0.0),
    row_y0=-24.0,
    row_pre_left=["Medium Generic House 2", "Tilted Tower Building 1", "Small Generic House 1", "Overhang Building 1"],
    row_pre_right=["Large Generic House 1", "Small Generic House 2", "Multistory Building 1", "Medium Generic House 2"],
    lamps_y=(-12, 9, 23, 37), moon=(7.0, 9.4, -60), moon_size=4.2, strip_near_lamps_y=3.0,
    # yellow rides on the road here (mid-air it looked stranded in the wide shot), behind orange on the right
    chasers=[("green", "skip", "chupalla", (-2.6, 7.2, 0), -68, 4, -12, 0),
             ("yellow", "doordeath", "top", (6.4, 9.0, 0), -112, 3, -8, 0)],
    look_at_bag=("yellow",),
)


def concept_library():
    concept_hero(LIBRARY)


def tracking_blur(env, speed, fps=24, shutter=0.5):
    """A tracking shot: the camera rides along with the couriers, so the street streaks past while they
    stay sharp. The street (everything built before the riders) slides away from the camera (+Y) at
    `speed` m/s across the shutter; Eevee's motion blur does the rest."""
    rig = bpy.data.objects.new("StreetRig", None)
    K.coll().objects.link(rig)
    bpy.context.view_layer.update()
    for o in env:
        mw = o.matrix_world.copy(); o.parent = rig; o.matrix_world = mw
    d = speed / fps
    for f, y in ((0, -d), (1, 0.0), (2, d)):
        rig.location = (0, y, 0)
        rig.keyframe_insert("location", frame=f)
    for fc in rig.animation_data.action.fcurves if hasattr(rig.animation_data.action, "fcurves") else []:
        for k in fc.keyframe_points:
            k.interpolation = 'LINEAR'
    scene.frame_set(1)
    scene.render.use_motion_blur = True
    scene.render.motion_blur_shutter = shutter
    try:
        scene.eevee.motion_blur_steps = 4
    except Exception:
        pass


def report(cam, **pts):
    """Print where points land in the frame (fractions from top-left), to place things by numbers."""
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    for k, p in pts.items():
        v = world_to_camera_view(scene, cam, Vector(p))
        print("SCREEN", k, round(v.x, 3), round(1 - v.y, 3), round(v.z, 2))


def concept_phase():
    """A courier phase-boosts out through a house's wall at the camera in a wheelie; a glowing ripple
    where it passes through. Two rivals' headlights behind. Clock tower and moon above the roofs."""
    ground()
    # the wall the rider bursts through: a house facing the camera (front looks -Y)
    h = up.import_prefab(PB + "Overhang Building 1.prefab", "Wall", light_scale=110.0)
    bpy.context.view_layer.update()
    b0, b1 = footprint(body_of(h))
    sw = 1.1
    h.rotation_euler = (0, 0, R(-90))       # +X front -> -Y
    h.location = (-sw * (b0.y + b1.y) / 2 * -1, 6.0 + sw * b1.x, 0)
    h.scale = (sw,) * 3
    for i, (pf, x) in enumerate((("Medium Generic House 1", -11.5), ("Large Generic House 2", 11.5))):
        r = up.import_prefab(PB + pf + ".prefab", f"Side{i}", light_scale=110.0)
        bpy.context.view_layer.update()
        c0, c1 = footprint(body_of(r))
        r.rotation_euler = (0, 0, R(-90 + (14 if x < 0 else -14)))
        r.location = (x, 7.5 + 0.9 * c1.x, 0)
        r.scale = (0.9,) * 3
    tower((-9, 40, 0), 0.8)
    s = 0.48
    _, g, i = ride("Hero", "purple", "ghostmates", (0.0, 5.2, 0), -90, s, pitch=24, glow=0.7, headlight=120)
    place_hat(g, "top", 0.42, lift=-0.25, tilt=(0, -12, 0))
    rack_package(i, 0.9, "Large", rot=(0, 0, 8))
    cam = K.camera((1.6, -3.2, 0.7), (0.0, 6, 2.8), 20)
    # ripple where the rider passes through the wall
    for k, (sz, a) in enumerate(((5.5, 0.55), (8.5, 0.30), (12.0, 0.16))):
        K.halo_disc(f"Ripple{k}", (0.0, 5.95 - k * 0.01, 2.2), sz, (0.35, 1.0, 0.75), 2.2, a, (0, -50, 2.2), 1.2 + k * 0.6)
    streaks("Trail", (0, 7.0, 2.0), (0, 1, 0.08), 22, 4.0, 1.4, (0.6, 0.9, 1.0), 4.0)
    K.night_world()
    K.moon(cam, (14, 12.0, -70), 13)
    K.sun("Moonlight", (-0.3, 0.6, -0.7), (0.55, 0.62, 1.0), 1.2)
    K.sun("SkyFill", (0, 0.3, -1), (0.42, 0.50, 1.0), 0.5, angle=30)
    K.area("Key", (-3, -3, 5), (0, 4.5, 2), (0.6, 0.65, 1.0), 600, 5)
    K.area("Rim", (3, 4, 6), (0, 5, 2), (0.4, 1.0, 0.75), 500, 4)
    K.haze((0, 30, 10), (80, 60, 25), 0.005)


GOLD = dict(
    cam=(-0.2, -5.2, 4.0), aim=(1.6, 8.0, 2.2), lens=20, shift=(-0.11, 0.07),
    row_y=20.0, row_x0=-20.0, row_scale=0.72, tower=None, tower_scale=0.95, parcel=(1.6, 6.4, 2.0), parcel_size=1.8,
    riders=[  # ghost, scooter, hat, loc, heading, roll, pitch
        ("purple", "ubercreeps", "top", (-2.4, 3.2, 0), -40, 22, 4),
        ("orange", "ghostmates", "propeller", (4.9, 3.9, 0), -140, -22, 6),
        ("green", "skip", "bucket", (-0.9, 10.6, 0), -35, 16, 2),
        ("yellow", "doordeath", "conical", (4.4, 10.2, 0), -145, -16, 2),
    ],
    moon=(19.0, 34.0, -60), moon_size=7.0,
)


def back_row(names, y_front, x0, scale, light=110.0):
    """Houses side by side along +X, fronts looking at the camera (-Y), front walls on y = y_front."""
    x = x0
    for i, pf in enumerate(names):
        r = up.import_prefab(PB + pf + ".prefab", f"Back{i}", light_scale=light)
        bpy.context.view_layer.update()
        b0, b1 = footprint(body_of(r))
        r.rotation_euler = (0, 0, R(-90))          # local +X (front) -> -Y; local +Y -> +X
        r.location = (x - scale * b0.y, y_front + scale * b1.x, 0)
        r.scale = (scale,) * 3
        x += scale * (b1.y - b0.y)


def drift_sparks(name, info, color, count=26, seed=3):
    """The drift-boost sparks: short glowing streaks thrown off the back wheel."""
    f, u = info["fwd"], info["up"]
    side = f.cross(u).normalized()
    wheel = info["seat"] - f * 0.9 * info["scale"] * 3 - u * 0.95 * info["scale"] * 3
    streaks(name, wheel, -f + side * 0.35 + u * 0.25, count, 1.6, 0.35, color, 8.0, seed, 0.012)
    K.point(name + "Glow", wheel - f * 0.3, color, 120, 0.2)


def concept_golden():
    """The Golden Order showdown: a high three-quarter view of the final area. The golden parcel hangs in
    a pillar of light in front of the haunted mansion; four couriers drift in at it from every side,
    sparks flying. Logo top-left over the night sky."""
    G = GOLD
    ground(tint=(0.20, 0.21, 0.29))
    back_row(["Medium Generic House 2", "Overhang Building 1", "Large Generic House 1", "Multistory Building 1",
              "Tilted Tower Building 1", "Large Generic House 2", "Medium Generic House 1"], G["row_y"], G["row_x0"], G["row_scale"])
    if G.get("tower"):
        tower(G["tower"], G["tower_scale"])
    for kind, loc, h, rot in (("Crooked", (-11, 9, 0), 7.5, 30), ("Swirl", (13.5, 10, 0), 7, 200)):
        tree(kind, loc, h, rot)
    for x, y in ((-6, 13), (9, 13)):
        street_lamp((x, y, 0), 1.0)
    # the golden parcel in its pillar of light
    pc = Vector(G["parcel"])
    gp = K.fit(K.package("Golden", "Large"), G["parcel_size"], pc - Vector((0, 0, G["parcel_size"] / 2)), (8, 0, 30))
    K.gold(gp, 3.0)
    K.point("GoldLight", pc + Vector((0, -0.8, 0.6)), K.srgb((1.0, 0.75, 0.2)), 1400, 0.4)
    beam = K.principled("BeamMat", (0, 0, 0), 0.5, emit=K.srgb((1.0, 0.82, 0.35)), emit_strength=1.2)
    bm = beam.node_tree
    tr = bm.nodes.new("ShaderNodeBsdfTransparent"); mx = bm.nodes.new("ShaderNodeMixShader")
    lw = bm.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.35
    gz = bm.nodes.new("ShaderNodeTexCoord"); sp = bm.nodes.new("ShaderNodeSeparateXYZ")
    fade = bm.nodes.new("ShaderNodeMath"); fade.operation = 'MULTIPLY'
    inv = bm.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    bm.links.new(gz.outputs["Generated"], sp.inputs[0]); bm.links.new(sp.outputs["Z"], inv.inputs[1])
    one = bm.nodes.new("ShaderNodeMath"); one.operation = 'SUBTRACT'; one.inputs[0].default_value = 1.0
    bm.links.new(lw.outputs["Facing"], one.inputs[1])
    bm.links.new(one.outputs[0], fade.inputs[0]); bm.links.new(inv.outputs[0], fade.inputs[1])
    sc_ = bm.nodes.new("ShaderNodeMath"); sc_.operation = 'MULTIPLY'; sc_.inputs[1].default_value = 0.14
    bm.links.new(fade.outputs[0], sc_.inputs[0])
    bm.links.new(sc_.outputs[0], mx.inputs["Fac"])
    bm.links.new(tr.outputs[0], mx.inputs[1]); bm.links.new(bm.nodes["Principled BSDF"].outputs[0], mx.inputs[2])
    bm.links.new(mx.outputs[0], bm.nodes["Material Output"].inputs["Surface"])
    try:
        beam.surface_render_method = 'BLENDED'
    except Exception:
        pass
    bpy.ops.mesh.primitive_cylinder_add(radius=0.6, depth=24, vertices=48, location=(pc.x, pc.y + 0.6, 12.0))
    bc = bpy.context.object; bc.name = "Beam"; bc.data.materials.append(beam); bc.visible_shadow = False
    s = 0.42
    riders = []
    spark_cols = [(0.3, 0.6, 1.0), (1.0, 0.45, 0.1), (0.3, 0.6, 1.0), (0.85, 0.3, 1.0)]
    for i, (gc, scn, hk, loc, hd, rl, pt) in enumerate(G["riders"]):
        _, g, ii = ride(f"R{i}", gc, scn, loc, hd, s, pitch=pt, roll=rl, glow=0.6)
        place_hat(g, hk, 0.42 if hk != "propeller" else 0.48, lift=-0.25 if hk != "propeller" else -0.2)
        # turn the ghost to look at the parcel
        K.twist(g, ii, max(-40, min(40, math.degrees(math.atan2(*(lambda d: (d.y * ii["fwd"].x - d.x * ii["fwd"].y, d.x * ii["fwd"].x + d.y * ii["fwd"].y))(pc - ii["seat"]))))) * 0.7)
        drift_sparks(f"Spark{i}", ii, spark_cols[i], seed=i + 2)
        riders.append(ii)
    cam = K.camera(G["cam"], G["aim"], G["lens"], G["shift"])
    K.night_world()
    K.moon(cam, G["moon"], G["moon_size"])
    K.sun("Moonlight", (0.4, 0.6, -0.7), (0.55, 0.62, 1.0), 1.0)
    K.sun("SkyFill", (0, 0.3, -1), (0.42, 0.50, 1.0), 0.3, angle=30)
    K.spot("Key", (-3, -6, 9), (1.6, 7, 0.5), (0.62, 0.68, 1.0), 6000, 40, 0.9)
    K.haze((0, 30, 10), (80, 60, 25), 0.0015)
    K.halo_disc("GoldHalo", pc + Vector((0, 0.5, 0.1)), 4.5, K.srgb((1.0, 0.8, 0.3)), 2.0, 0.45, cam.location, 2.2)
    report(cam, Parcel=pc, MoonC=bpy.data.objects["Moon"].location,
           **{f"R{i}": r["seat"] for i, r in enumerate(riders)})


SHOT_RES = LIBRARY["res"] if CONCEPT == "library" else (1232, 706)
K.render_setup(OUT, res=SHOT_RES, percent=PERCENT, samples=SAMPLES, exposure=0.6)
{"library": concept_library, "hero": concept_hero, "steal": concept_steal, "rush": concept_rush, "phase": concept_phase, "golden": concept_golden}[CONCEPT]()
K.render_setup(OUT, res=SHOT_RES, percent=PERCENT, samples=SAMPLES, exposure=0.6)
if SAVE:
    bpy.ops.file.make_paths_absolute()
    bpy.ops.wm.save_as_mainfile(filepath=SAVE)
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
