"""Shared pieces for the main-capsule concept scenes (build_capsule_v3.py): riders, packages, lights, sky.

Every model comes from the game's own FBXs/prefabs. Model facts (Blender side, after import):
  GhostMates.fbx scooter: nose along +X, grips at model (0.505, +-1.05, 5.0)
  GHOST_FINAL.fbx: looks along +X; at 1.25x the scooter's scale, centred on the seat, its hands sit on the grips
"""
import bpy, sys, math, os
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import unity_prefab as up

ROOT = os.path.dirname(os.path.dirname(HERE)).replace("\\", "/") + "/"
A = ROOT + "Assets/"


def srgb(c):
    """Unity colour (sRGB 0-1) -> Blender linear."""
    f = lambda v: v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return tuple(f(v) for v in c)


# Game ghost colours (Ghost Color 1-6.mat) and company scooter colours (Materials/Companies)
GHOST = {"purple": srgb((0.527, 0.223, 0.774)), "red": srgb((0.84, 0.0, 0.074)), "green": srgb((0.0, 0.84, 0.0)),
         "yellow": srgb((0.84, 0.767, 0.0)), "blue": srgb((0.0, 0.0, 0.84)), "orange": srgb((0.84, 0.417, 0.0))}
SCOOTER = {"ghostmates": srgb((0.20, 0.20, 0.20)), "doordeath": srgb((0.70, 0.0, 0.0)),
           "skip": srgb((0.73, 0.38, 0.0)), "ubercreeps": srgb((0.17, 0.36, 0.14))}
HORN = srgb((0.275, 0.071, 1.0))


def coll():
    return bpy.context.scene.collection


def bbox(objs):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    mn = Vector([min(p[k] for p in pts) for k in range(3)])
    mx = Vector([max(p[k] for p in pts) for k in range(3)])
    return mn, mx


def load(path, name):
    """Import an FBX (path under Assets/) and parent it under an empty at its bbox bottom-centre."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=A + path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        o.animation_data_clear()
    mn, mx = bbox(new)
    root = bpy.data.objects.new(name, None)
    coll().objects.link(root)
    root.location = ((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)
    bpy.context.view_layer.update()
    for o in new:
        if o.parent is None:
            mw = o.matrix_world.copy()
            o.parent = root
            o.matrix_world = mw
    root["kids"] = [o.name for o in new]
    root.location = (0, 0, 0)
    return root


def kids(root):
    return [bpy.data.objects[n] for n in root["kids"] if n in bpy.data.objects]


def mats_of(root):
    seen = {}
    for o in kids(root):
        if o.type == 'MESH':
            for m in o.data.materials:
                if m:
                    seen[m.name] = m
    return seen


def recolor(root, prefix, color, emit=None, emit_strength=0.0, rough=None, alpha=None):
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
                    b.inputs["Metallic"].default_value = 0.0
                    if emit:
                        b.inputs["Emission Color"].default_value = (*emit, 1)
                        b.inputs["Emission Strength"].default_value = emit_strength
                    done.add(m.name)


def make_single(root):
    """Give this copy its own materials so recolouring doesn't touch another copy's."""
    for o in kids(root):
        if o.type == 'MESH':
            o.data = o.data.copy()
            for i, m in enumerate(o.data.materials):
                if m:
                    o.data.materials[i] = m.copy()


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


def look_at(obj, target, up_axis='Y'):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', up_axis).to_euler()


def point(name, loc, color, energy, radius=0.1):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'POINT'))
    l.data.color = color
    l.data.energy = energy
    l.data.shadow_soft_size = radius
    l.location = loc
    coll().objects.link(l)
    return l


def spot(name, loc, target, color, energy, angle=50, blend=0.6):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SPOT'))
    l.data.color = color
    l.data.energy = energy
    l.data.spot_size = math.radians(angle)
    l.data.spot_blend = blend
    l.location = loc
    coll().objects.link(l)
    look_at(l, target)
    return l


def area(name, loc, target, color, energy, size=4.0):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'AREA'))
    l.data.shape = 'DISK'
    l.data.size = size
    l.data.color = color
    l.data.energy = energy
    l.location = loc
    coll().objects.link(l)
    look_at(l, target)
    return l


def sun(name, direction_to, color, energy, angle=3):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, 'SUN'))
    l.data.color = color
    l.data.energy = energy
    l.data.angle = math.radians(angle)
    coll().objects.link(l)
    l.rotation_euler = (-Vector(direction_to)).to_track_quat('-Z', 'Y').to_euler()
    return l


def halo_disc(name, pos, size, color, strength, alpha, cam_loc, power=2.0):
    """A see-through glowing disc that faces the camera."""
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
    d.rotation_euler = (Vector(cam_loc) - Vector(pos)).to_track_quat('Z', 'Y').to_euler()
    d.scale = (size, size, 1)
    d.visible_shadow = False
    return d


# ---------------------------------------------------------------- riders
_SEAT = None
GRIP = Vector((0.505, 1.05, 5.0))


def scooter(name, body, stripe=None, model="Models/Vehicle Models/GhostMates.fbx"):
    sc = load(model, name)
    make_single(sc)
    stripe = stripe or tuple(c * 0.55 for c in body)
    recolor(sc, "Scooter.", body, rough=0.28)
    recolor(sc, "Scooter", body, rough=0.28)
    recolor(sc, "Logo", body, rough=0.28)
    recolor(sc, "Stripe", stripe, rough=0.3)
    recolor(sc, "ScooterBorder", stripe, rough=0.3)
    recolor(sc, "Seat", (0.03, 0.02, 0.02), rough=0.5)
    recolor(sc, "Headlights", (0, 0, 0), emit=(1.0, 0.82, 0.5), emit_strength=8)
    recolor(sc, "RearLight", (1, 0.1, 0.1), emit=(1.0, 0.08, 0.04), emit_strength=12)
    return sc


def mat_centre(root, prefix):
    bpy.context.view_layer.update()
    inv = root.matrix_world.inverted()
    acc, n = Vector(), 0
    for o in kids(root):
        if o.type != 'MESH':
            continue
        for p in o.data.polygons:
            m = o.data.materials[p.material_index] if o.data.materials else None
            if m and m.name.startswith(prefix):
                acc += inv @ (o.matrix_world @ p.center)
                n += 1
    return acc / n if n else None


GHOST_ALPHA = 0.90       # ghosts are a little see-through


def ghost(name, color, glow=0.45, alpha=None):
    gh = load("Models/Ghost Models/GHOST_FINAL.fbx", name)
    make_single(gh)
    recolor(gh, "Body", color, emit=color, emit_strength=glow, rough=0.35)
    a = GHOST_ALPHA if alpha is None else alpha
    if a < 1:
        for m in mats_of(gh).values():
            if m.name.startswith("Body"):
                m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = a
                m.use_backface_culling = True                # one see-through layer, not front + back
                try:
                    m.use_transparent_shadow = False          # see-through shadows stipple under close lights
                except Exception:
                    pass
                try:
                    m.surface_render_method = 'BLENDED'      # smooth see-through (dithered leaves a dot pattern)
                    m.use_transparency_overlap = False
                except Exception:
                    m.blend_method = 'HASHED'
    recolor(gh, "Horn", srgb((1.0, 0.86, 0.78)), rough=0.4)
    return gh


def hat(name, kind, color=None):
    path = {"top": "Models/Hats/Top Hat.fbx", "propeller": "Models/Hats/Propeller_Hat.fbx",
            "bucket": "Models/Hats/Bucket_Hat.fbx", "conical": "Models/Hats/Conical Hat.fbx",
            "trapper": "Models/Hats/Trapper Hat.fbx", "cap": "Models/Hats/BaldCap_Updated.fbx",
            "sunglasses": "Models/Hats/Sunglasses.fbx", "sombrero": "Models/Hats/Chuppalla Hat - Jesser.fbx"}[kind]
    h = load(path, name)
    make_single(h)
    if color:
        for m in mats_of(h).values():
            m.use_nodes = True
            b = m.node_tree.nodes.get("Principled BSDF")
            if b and not b.inputs["Base Color"].links:
                pass
    return h


def rider(name, ghost_color, scooter_color, M, stripe=None, glow=0.45, lights=True, headlight=60.0):
    """A ghost seated on its scooter, both placed by world matrix M (scooter model space: nose +X, origin
    under its middle, model units). Returns (scooter_root, ghost_root, info)."""
    sc = scooter(name + "Scooter", scooter_color, stripe)
    seat = mat_centre(sc, "Seat")
    head = mat_centre(sc, "Headlights")
    rear = mat_centre(sc, "RearLight")
    sc.matrix_world = M
    gh = ghost(name + "Ghost", ghost_color, glow)
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    mn, mx = bbox([gm])
    # ghost local: body middle on the seat, 1.25x the scooter's scale (scooter units are already in M)
    gh_off = Vector(((mn.x + mx.x) / 2 + 0.06, (mn.y + mx.y) / 2, mn.z))
    gh.matrix_world = M @ Matrix.Translation(Vector((seat.x, seat.y, seat.z)) - gh_off * 1.25) @ Matrix.Diagonal((1.25, 1.25, 1.25, 1))
    bpy.context.view_layer.update()
    info = dict(M=M, seat=M @ seat, head=M @ head, rear=M @ rear if rear else None, grip=M @ GRIP,
                fwd=(M.to_3x3() @ Vector((1, 0, 0))).normalized(), up=(M.to_3x3() @ Vector((0, 0, 1))).normalized(),
                scale=(M.to_3x3() @ Vector((1, 0, 0))).length)
    if lights:
        f = info["fwd"]
        s = info["scale"]
        spot(name + "Beam", info["head"] + f * 0.15 * s, info["head"] + f * 4 * s - info["up"] * 3.2 * s,
             (1.0, 0.85, 0.6), headlight * s * s * 0.5, 70, 1.0)     # soft-edged: a hard cone edge read as a stray light
        point(name + "HeadGlow", info["head"] + f * 0.4 * s, (1.0, 0.85, 0.6), 4 * s * s, 0.05)
    return sc, gh, info


def hand_pieces(gm):
    """The ghost's two hands: the two loose mesh pieces furthest forward (model +X). Vertex index lists."""
    import bmesh
    bm = bmesh.new(); bm.from_mesh(gm.data); bm.verts.ensure_lookup_table()
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
    co = gm.data.vertices
    big = [p for p in parts if len(p) > 50]
    T = gm.parent.matrix_world.inverted() @ gm.matrix_world if gm.parent else gm.matrix_world
    return sorted(big, key=lambda p: -sum((T @ co[i].co).x for i in p) / len(p))[:2]


def reach(gh, target, side=1, spin=0.0):
    """Move one of the ghost's hands (side +1 = its left, model +Y; -1 = its right) to a world point."""
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    bpy.context.view_layer.update()
    hands = hand_pieces(gm)
    co = gm.data.vertices
    T = gh.matrix_world.inverted() @ gm.matrix_world
    hand = max(hands, key=lambda p: side * sum((T @ co[i].co).y for i in p) / len(p))
    mid = sum((co[i].co for i in hand), Vector()) / len(hand)
    inv = gm.matrix_world.inverted()
    shift = inv @ Vector(target) - mid
    rot = Matrix.Rotation(math.radians(spin), 3, 'X')
    for i in hand:
        co[i].co = mid + rot @ (co[i].co - mid) + shift
    gm.data.update()


def grab(gh, side, objs, centre, from_dir, overlap=0.35, shallow_to=None):
    """Lay one of the ghost's hands (side +1 its left, -1 its right) flat on the surface of objs, on the side
    facing from_dir: ray-cast from outside toward centre, turn the hand so its thin axis lies along the surface
    normal, and sink it in a little (overlap, as a fraction of its thickness) so it reads as holding on."""
    import numpy as np
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    bpy.context.view_layer.update()
    d = Vector(from_dir).normalized()
    start = Vector(centre) + d * 5.0
    best = None
    for o in objs:
        inv = o.matrix_world.inverted()
        ok, loc, nrm, _ = o.ray_cast(inv @ start, (inv.to_3x3() @ -d).normalized())
        if ok:
            wl = o.matrix_world @ loc
            wn = (o.matrix_world.inverted().transposed().to_3x3() @ nrm).normalized()
            if best is None or (wl - start).length < (best[0] - start).length:
                best = (wl, wn)
    if best is None:
        print("grab: no surface hit")
        return None
    P, n = best
    if n.dot(d) < 0:
        n = -n
    hands = hand_pieces(gm)
    co = gm.data.vertices
    T = gh.matrix_world.inverted() @ gm.matrix_world
    hand = max(hands, key=lambda q: side * sum((T @ co[i].co).y for i in q) / len(q))
    W = gm.matrix_world
    pts = np.array([list(W @ co[i].co) for i in hand])
    c = pts.mean(0)
    _, _, Vt = np.linalg.svd(pts - c)
    thin = Vector(Vt[2])                                       # the hand's flattest direction
    if thin.dot(n) < 0:
        thin = -thin
    R_ = thin.rotation_difference(n).to_matrix()
    rel = [R_ @ Vector(p - c) for p in pts]
    if shallow_to is not None:
        arr = np.array([list(v) for v in rel])
        flat = arr - np.outer(arr @ np.array(list(n)), np.array(list(n)))       # the hand flattened onto the surface
        _, _, Vt2 = np.linalg.svd(flat - flat.mean(0))
        a = Vector(Vt2[0])                                                       # its long axis, in the surface
        thick = lambda half: (max(v.dot(n) for v in half) - min(v.dot(n) for v in half)) if half else 1e9
        pos = [v for v in rel if v.dot(a) > 0]
        neg = [v for v in rel if v.dot(a) <= 0]
        shallow = a if thick(pos) < thick(neg) else -a
        t = Vector(shallow_to) - n * Vector(shallow_to).dot(n)
        if t.length > 1e-6:
            t.normalize()
            ang = math.atan2(shallow.cross(t).dot(n), shallow.dot(t))
            Rn = Matrix.Rotation(ang, 3, n)
            rel = [Rn @ v for v in rel]
    depth = max(v.dot(n) for v in rel) - min(v.dot(n) for v in rel)
    target = P + n * depth * (0.5 - overlap)
    inv = W.inverted()
    for k, i in enumerate(hand):
        co[i].co = inv @ (target + rel[k])
    gm.data.update()
    from bpy_extras.object_utils import world_to_camera_view
    cam = bpy.context.scene.camera
    if cam:
        sc = bpy.context.scene
        f = lambda v: tuple(round(x, 3) for x in world_to_camera_view(sc, cam, v))
        print("GRAB hit", f(P), "normal", tuple(round(x, 2) for x in n), "hand", f(target))
    return P


def twist(gh, info, yaw_deg, hands=(1, -1)):
    """Turn the seated ghost about the seat's upright (+ = toward its left), then put the listed hands
    (+1 its left, -1 its right) back on the scooter's grips."""
    up_ = info["up"]
    c = info["seat"]
    R_ = Matrix.Rotation(math.radians(yaw_deg), 4, up_)
    gh.matrix_world = Matrix.Translation(c) @ R_ @ Matrix.Translation(-c) @ gh.matrix_world
    bpy.context.view_layer.update()
    for sd in hands:
        reach(gh, info["M"] @ Vector((GRIP.x, GRIP.y * sd, GRIP.z)), side=sd)


def ghost_top(gh):
    """World point on top of the ghost's head (front half), for hats."""
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    bpy.context.view_layer.update()
    pts = [gm.matrix_world @ v.co for v in gm.data.vertices]
    return max(pts, key=lambda p: p.z)


# ---------------------------------------------------------------- hats, as the game wears them
# The game's ghost is the rigged Official_Anim_4.fbx; a hat hangs off its Head bone with the hat's own offset,
# rotation and scale (Scriptable Ojects/Player Hats/*.asset). The Head node is 100x in Unity, and Blender's bone
# frame is Unity's mirrored on X, so: hat world = head bone @ X-flip @ 100x @ hat TRS @ hat mesh fix (up._fx).
# The capsule's ghosts are GHOST_FINAL.fbx (seated with hands on the grips), so each hat is carried over from the
# rigged ghost by lining up the two models' horns and eye whites (a best-fit move, turn and scale).
ANIM = "Assets/Animations/Character Animations/Official_Anim_4.fbx"
HAT_ASSETS = {"top": "TopHat", "propeller": "PropellerHat", "bucket": "BucketHat", "trapper": "TrapperHat",
              "baseball": "BaseballHat", "chupalla": "ChupallaHat", "sunglasses": "Sunglasses"}
_anim = {}
_hats = {}


def _hat_info(kind):
    import re
    t = open(os.path.join(ROOT, "Assets/Scriptable Ojects/Player Hats", HAT_ASSETS[kind] + ".asset"), encoding="utf8").read()
    g = up.guids()
    mesh_guid = re.search(r"hatMesh: \{fileID: -?\d+, guid: (\w+)", t).group(1)
    mat_guid = re.search(r"hatMaterial: \{fileID: \d+, guid: (\w+)", t).group(1)
    vec = lambda k: [float(v) for v in re.search(k + r": \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", t).groups()]
    return dict(fbx=g[mesh_guid], mat=mat_guid, pos=vec("hatPosition"), rot=vec("hatRotation"), scale=vec("hatScale"))


def _unity_trs(p, eul, s):
    from mathutils import Quaternion
    q = (Quaternion((0, 1, 0), math.radians(eul[1])) @ Quaternion((1, 0, 0), math.radians(eul[0]))
         @ Quaternion((0, 0, 1), math.radians(eul[2])))      # Unity applies Z, then X, then Y
    return Matrix.Translation(p) @ q.to_matrix().to_4x4() @ Matrix.Diagonal((*s, 1))


def _landmarks(obj, horn, white):
    """Centroids of the two horns and the two eye whites, world space, ordered left/right the same way
    on any upright ghost. horn/white: material base names."""
    import bmesh
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    W = ev.matrix_world
    bm = bmesh.new(); bm.from_mesh(me)
    names = [m.name.split(".")[0] if m else "" for m in me.materials]
    out = {}
    for key, mname in (("horn", horn), ("white", white)):
        faces = [f for f in bm.faces if f.material_index < len(names) and names[f.material_index] == mname]
        seen, parts = set(), []
        for f in faces:                    # connected pieces of this material
            if f.index in seen:
                continue
            stack, comp = [f], []
            seen.add(f.index)
            while stack:
                x = stack.pop(); comp.append(x)
                for e in x.edges:
                    for y in e.link_faces:
                        if y.index not in seen and y.material_index == x.material_index:
                            seen.add(y.index); stack.append(y)
            parts.append(comp)
        parts.sort(key=len, reverse=True)
        cents = []
        for comp in parts[:2]:
            vs = {v.index: v.co for f in comp for v in f.verts}
            cents.append(W @ (sum(vs.values(), Vector()) / len(vs)))
        out[key] = cents
    bm.free(); ev.to_mesh_clear()
    h1, h2 = out["horn"]
    w1, w2 = out["white"]
    m, e = (h1 + h2) / 2, (w1 + w2) / 2
    if (h1 - h2).cross(e - m).z < 0:
        h1, h2 = h2, h1
    if (w1 - w2).dot(h1 - h2) < 0:
        w1, w2 = w2, w1
    return [h1, h2, w1, w2]


def _similarity(src, dst):
    """Best-fit scale + rotation + move taking points src onto dst (Umeyama)."""
    import numpy as np
    A = np.array([list(p) for p in src]); B = np.array([list(p) for p in dst])
    ca, cb = A.mean(0), B.mean(0)
    A0, B0 = A - ca, B - cb
    U, S, Vt = np.linalg.svd(B0.T @ A0)
    d = np.sign(np.linalg.det(U @ Vt))
    Dm = np.diag([1, 1, d])
    R_ = U @ Dm @ Vt
    sc = (S * np.diag(Dm)).sum() / (A0 ** 2).sum()
    t = cb - sc * R_ @ ca
    M = Matrix.Identity(4)
    for i in range(3):
        for j in range(3):
            M[i][j] = sc * R_[i, j]
        M[i][3] = t[i]
    return M


def _anim_ghost():
    if not _anim:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, ANIM), automatic_bone_orientation=False)
        new = [o for o in bpy.data.objects if o not in before]
        arm = next(o for o in new if o.type == 'ARMATURE')
        body = next(o for o in new if o.type == 'MESH')
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        _anim["head"] = arm.matrix_world @ arm.pose.bones["Head"].matrix
        _anim["marks"] = _landmarks(body, "Horns", "Eye")
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)
    return _anim


def _hat_mesh(kind):
    """The hat's mesh, baked where the game puts it on the rigged ghost (frame 1 of its import)."""
    if kind not in _hats:
        a = _anim_ghost()
        info = _hat_info(kind)
        rel = info["fbx"]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, rel))
        new = [o for o in bpy.data.objects if o not in before]
        meshes = [o for o in new if o.type == 'MESH']
        M = (a["head"] @ Matrix.Diagonal((-1, 1, 1, 1)) @ Matrix.Diagonal((100, 100, 100, 1))
             @ _unity_trs(info["pos"], info["rot"], info["scale"]) @ up._fx(rel))
        data = []
        for o in meshes:
            d = o.data.copy()
            # Unity uses the mesh data alone (no FBX node transform); bake it, since Eevee drops the mirrored matrix
            d.transform(M)
            if M.determinant() < 0:
                d.flip_normals()
            data.append(d)
        for o in new:
            bpy.data.objects.remove(o, do_unlink=True)
        mat = up.material(info["mat"])
        for d in data:
            d.materials.clear()
            if mat:
                d.materials.append(mat)
        _hats[kind] = data
    return _hats[kind]


def game_hat(gh, kind):
    """Put the game's hat `kind` on a GHOST_FINAL ghost (root `gh`) exactly as it sits in the game, textured
    with the game's hat material, parented to the ghost so later turns carry it."""
    gm = next(o for o in kids(gh) if o.type == 'MESH')
    bpy.context.view_layer.update()
    T = _similarity(_anim_ghost()["marks"], _landmarks(gm, "Horn", "Eyes"))
    made = []
    for i, d in enumerate(_hat_mesh(kind)):
        o = bpy.data.objects.new(f"{gh.name}Hat{i}", d.copy())
        o.data.transform(T)
        coll().objects.link(o)
        o.parent = gh
        o.matrix_parent_inverse = gh.matrix_world.inverted()
        made.append(o)
    return made


# ---------------------------------------------------------------- packages
def package(name, size="Medium"):
    """The game's package prefab (Small = green bag, Medium = yellow bag, Large = red box)."""
    r = up.import_prefab(f"Assets/Prefabs/Player Prefabs/{size}Package.prefab", name)
    return r


def all_meshes(root):
    out, st = [], [root]
    while st:
        o = st.pop()
        if o.type == 'MESH':
            out.append(o)
        st.extend(o.children)
    return out


def fit(root, target_size, loc, rot=(0, 0, 0)):
    """Scale root so its largest side is target_size, bottom-centre at loc."""
    root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0)
    mn, mx = bbox(all_meshes(root))
    s = target_size / max(mx - mn)
    root.scale = (root.scale.x * s, root.scale.y * s, root.scale.z * s)
    mn, mx = bbox(all_meshes(root))
    c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2))
    piv = bpy.data.objects.new(root.name + "Pivot", None)
    coll().objects.link(piv)
    piv.location = c
    bpy.context.view_layer.update()
    mw = root.matrix_world.copy()
    root.parent = piv
    root.matrix_world = mw
    piv.location = loc
    piv.rotation_euler = [math.radians(a) for a in rot]
    return piv


def gold(root, strength=2.5):
    g = principled("GoldOrder", srgb((1.0, 0.78, 0.15)), 0.25, emit=srgb((1.0, 0.72, 0.1)), emit_strength=strength)
    g.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.8
    for o in all_meshes(root):
        for i in range(len(o.data.materials)):
            o.data.materials[i] = g
        if not o.data.materials:
            o.data.materials.append(g)


# ---------------------------------------------------------------- sky / world
def night_world(top=(0.012, 0.010, 0.04), bottom=(0.03, 0.025, 0.07), stars=True):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("Night")
    scene.world = world
    world.use_nodes = True
    wn, wl = world.node_tree.nodes, world.node_tree.links
    bg = wn["Background"]
    tc = wn.new("ShaderNodeTexCoord")
    sep = wn.new("ShaderNodeSeparateXYZ")
    wl.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = wn.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.45
    ramp.color_ramp.elements[0].color = (*bottom, 1)
    ramp.color_ramp.elements[1].position = 0.75
    ramp.color_ramp.elements[1].color = (*top, 1)
    wl.new(sep.outputs["Z"], ramp.inputs["Fac"])
    if stars:
        vor = wn.new("ShaderNodeTexVoronoi")
        vor.inputs["Scale"].default_value = 260
        st = wn.new("ShaderNodeMapRange")
        st.inputs["From Min"].default_value = 0.0
        st.inputs["From Max"].default_value = 0.035
        st.inputs["To Min"].default_value = 1.0
        st.inputs["To Max"].default_value = 0.0
        wl.new(tc.outputs["Generated"], vor.inputs["Vector"])
        wl.new(vor.outputs["Distance"], st.inputs["Value"])
        add = wn.new("ShaderNodeMix")
        add.data_type = 'RGBA'
        add.blend_type = 'ADD'
        wl.new(st.outputs["Result"], add.inputs["Factor"])
        wl.new(ramp.outputs["Color"], add.inputs[6])
        add.inputs[7].default_value = (0.5, 0.5, 0.7, 1)
        wl.new(add.outputs[2], bg.inputs["Color"])
    else:
        wl.new(ramp.outputs["Color"], bg.inputs["Color"])
    return world


def moon(cam, cam_space_pos, size, strength=3.0, halo=True):
    mat = bpy.data.materials.new("Moon")
    mat.use_nodes = True
    N, L = mat.node_tree.nodes, mat.node_tree.links
    for n in list(N):
        N.remove(n)
    o_ = N.new("ShaderNodeOutputMaterial")
    t_ = N.new("ShaderNodeTexImage"); t_.image = bpy.data.images.load(A + "Materials/Skybox/Moon_Texture.png", check_existing=True)
    e_ = N.new("ShaderNodeEmission"); e_.inputs["Strength"].default_value = strength
    tr_ = N.new("ShaderNodeBsdfTransparent")
    mx_ = N.new("ShaderNodeMixShader")
    L.new(t_.outputs["Color"], e_.inputs["Color"])
    L.new(t_.outputs["Alpha"], mx_.inputs["Fac"])
    L.new(tr_.outputs[0], mx_.inputs[1])
    L.new(e_.outputs[0], mx_.inputs[2])
    L.new(mx_.outputs[0], o_.inputs["Surface"])
    bpy.ops.mesh.primitive_plane_add(size=1)
    mo = bpy.context.object
    mo.name = "Moon"
    mo.data.materials.append(mat)
    bpy.context.view_layer.update()
    mo.location = cam.matrix_world @ Vector(cam_space_pos)
    mo.rotation_euler = cam.matrix_world.to_euler()
    mo.scale = (782 / 701 * size, size, 1)
    mo.visible_shadow = False
    if halo:
        halo_disc("MoonHalo", cam.matrix_world @ (Vector(cam_space_pos) * 1.02), size * 2.4, (0.55, 0.60, 1.0), 1.4, 0.30,
                  cam.matrix_world.translation, 3.0)
    return mo


def haze(center, size, density=0.002, color=(0.45, 0.5, 0.9)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    hz = bpy.context.object
    hz.name = "Haze"
    hz.scale = size
    hm = bpy.data.materials.new("HazeMat"); hm.use_nodes = True
    for n_ in list(hm.node_tree.nodes): hm.node_tree.nodes.remove(n_)
    vo = hm.node_tree.nodes.new("ShaderNodeVolumePrincipled")
    vo.inputs["Density"].default_value = density
    vo.inputs["Color"].default_value = (*color, 1)
    oo = hm.node_tree.nodes.new("ShaderNodeOutputMaterial")
    hm.node_tree.links.new(vo.outputs[0], oo.inputs["Volume"])
    hz.data.materials.append(hm)
    return hz


def camera(loc, target, lens, shift=(0, 0), roll=0.0):
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    coll().objects.link(cam)
    cam.location = loc
    look_at(cam, target)
    if roll:
        cam.rotation_euler.rotate_axis('Z', math.radians(roll))
    cam.data.lens = lens
    cam.data.sensor_fit = 'HORIZONTAL'
    cam.data.shift_x, cam.data.shift_y = shift
    bpy.context.scene.camera = cam
    bpy.context.view_layer.update()
    return cam


def render_setup(out, res=(1232, 706), percent=100, samples=48, exposure=0.3, engine="eevee"):
    scene = bpy.context.scene
    r = scene.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = res[0], res[1], percent
    scene.view_settings.view_transform = 'AgX' if 'AgX' in [i.identifier for i in scene.view_settings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = exposure
    if engine == "cycles":
        r.engine = 'CYCLES'
        scene.cycles.samples = samples
        scene.cycles.use_denoising = True
    else:
        engines = [e.identifier for e in r.bl_rna.properties['engine'].enum_items]
        r.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
        scene.eevee.taa_render_samples = samples
        try:
            scene.eevee.shadow_pool_size = '1024'      # the prefabs bring many lamp lights
        except Exception:
            pass
        for k in ("use_shadows", "use_raytracing"):
            try:
                setattr(scene.eevee, k, True)
            except Exception:
                pass
    r.filepath = out
