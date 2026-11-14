"""Bring a Unity prefab from this project into Blender the way the game assembles it.

Reads the prefab YAML (nested prefab instances, transform and material overrides), imports the
FBX each instance points at, and gives every mesh the transform and the .mat material Unity uses.

    import unity_prefab as up
    root = up.import_prefab("Assets/Prefabs/Buildings/Buildings New/Clock Tower.prefab", "ClockTower")

The result hangs under one Empty. Unity metres map 1:1 to Blender metres; Unity (x, y, z) lands
at Blender (-x, -z, y).
"""
import bpy, re, os, json, math, struct
from mathutils import Matrix, Vector, Quaternion

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_guid_cache.json")

C = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))    # Unity world -> Blender world
_unit_cache = {}


def _fx(rel):
    """Blender mesh-local -> Unity mesh-local for one FBX: flip X, and file units -> metres
    (UnitScaleFactor 1 = centimetres; the clock hands were exported in inches, 2.54)."""
    if rel not in _unit_cache:
        usf = 1.0
        try:
            data = open(os.path.join(ROOT, rel), "rb").read()
            i = data.find(b"UnitScaleFactor")
            if i >= 0:
                j = data.find(b"Number", i, i + 80)
                if j >= 0 and data[j + 6:j + 11] == b"S\x00\x00\x00\x00" and data[j + 11:j + 12] == b"D":
                    usf = struct.unpack("<d", data[j + 12:j + 20])[0]
                else:
                    m = re.search(rb'"UnitScaleFactor"[^\n]*?,\s*([-\d.eE]+)\s*$', data[i - 4:i + 200], re.M)
                    if m:
                        usf = float(m.group(1))
        except OSError:
            pass
        _unit_cache[rel] = usf if 0.0001 < usf < 10000 else 1.0
    u = _unit_cache[rel] / 100.0
    return Matrix.Diagonal((-u, u, u, 1))
EMIT_MAX = 3.0                                                           # cap on HDR emission (the game leans on bloom)

_guids = None
_fbx_cache = {}     # path -> [(object, default world matrix)]
_mat_cache = {}
_lib = None


def guids():
    global _guids
    if _guids is None:
        if os.path.exists(_CACHE):
            _guids = json.load(open(_CACHE))
        else:
            _guids = {}
            for dp, _, fs in os.walk(os.path.join(ROOT, "Assets")):
                for f in fs:
                    if f.endswith(".meta"):
                        try:
                            with open(os.path.join(dp, f), encoding="utf-8", errors="ignore") as fh:
                                for line in fh:
                                    if line.startswith("guid:"):
                                        _guids[line.split()[1]] = os.path.relpath(os.path.join(dp, f[:-5]), ROOT).replace("\\", "/")
                                        break
                        except OSError:
                            pass
            json.dump(_guids, open(_CACHE, "w"))
    return _guids


def _docs(rel):
    txt = open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore").read()
    return {m.group(2): (int(m.group(1)), m.group(3))
            for m in re.finditer(r"--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=\n--- !u!|\Z)", txt, re.S)}


def _vec(body, key, names):
    m = re.search(key + r": \{([^}]*)\}", body)
    d = {k.strip(): float(v) for k, v in (p.split(":") for p in m.group(1).split(","))}
    return [d[n] for n in names]


def _trs(p, q, s):
    """Unity TRS; q is (x, y, z, w)."""
    return (Matrix.Translation(Vector(p)) @ Quaternion((q[3], q[0], q[1], q[2])).to_matrix().to_4x4()
            @ Matrix.Diagonal((s[0], s[1], s[2], 1)))


def _decompose(M):
    loc = M.to_translation()
    sc = [M.col[i].to_3d().length for i in range(3)]
    R = Matrix([[M[r][c] / (sc[c] or 1) for c in range(3)] for r in range(3)])
    q = R.to_quaternion()
    return list(loc), [q.x, q.y, q.z, q.w], sc


# ---------------------------------------------------------------- materials
def _image(rel, non_color=False):
    im = bpy.data.images.load(os.path.join(ROOT, rel), check_existing=True)
    if non_color:
        im.colorspace_settings.name = 'Non-Color'
    return im


def material(guid):
    """Blender material for a Unity .mat (base map + tiling, tint, normal, emission)."""
    if guid in _mat_cache:
        return _mat_cache[guid]
    rel = guids().get(guid)
    if not rel or not rel.endswith(".mat"):
        _mat_cache[guid] = None
        return None
    b = open(os.path.join(ROOT, rel), encoding="utf-8", errors="ignore").read()
    tex = {}
    for m in re.finditer(r"- (_\w+):\s*\n\s*m_Texture: \{fileID: \d+(?:, guid: (\w+))?[^}]*\}\s*\n\s*m_Scale: \{x: ([-\d.e]+), y: ([-\d.e]+)\}\s*\n\s*m_Offset: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", b):
        if m.group(2) and guids().get(m.group(2)):
            tex[m.group(1)] = (guids()[m.group(2)], float(m.group(3)), float(m.group(4)), float(m.group(5)), float(m.group(6)))
    col = {}
    for m in re.finditer(r"- (_\w+): \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}", b):
        col[m.group(1)] = [float(m.group(i)) for i in range(2, 6)]
    flt = {m.group(1): float(m.group(2)) for m in re.finditer(r"- (_\w+): ([-\d.e]+)\n", b)}
    emissive = "_EMISSION" in b.split("m_SavedProperties")[0]

    name = os.path.splitext(os.path.basename(rel))[0] + "_" + guid[:4]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    N, L = mat.node_tree.nodes, mat.node_tree.links
    bsdf = N["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 1.0 - min(max(flt.get("_Smoothness", flt.get("_Glossiness", 0.3)), 0.0), 0.9)
    bsdf.inputs["Metallic"].default_value = flt.get("_Metallic", 0.0)
    base = col.get("_BaseColor", col.get("_Color", [1, 1, 1, 1]))
    tc = N.new("ShaderNodeTexCoord")

    def tex_node(key, non_color=False):
        rel_t, sx, sy, ox, oy = tex[key]
        t = N.new("ShaderNodeTexImage")
        try:
            t.image = _image(rel_t, non_color)
        except RuntimeError:
            return None
        mp = N.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (sx, sy, 1)
        mp.inputs["Location"].default_value = (ox, oy, 0)
        L.new(tc.outputs["UV"], mp.inputs["Vector"])
        L.new(mp.outputs["Vector"], t.inputs["Vector"])
        return t

    bkey = "_BaseMap" if "_BaseMap" in tex else ("_MainTex" if "_MainTex" in tex else None)
    t = tex_node(bkey) if bkey else None
    if t:
        mul = N.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
        mul.inputs["Factor"].default_value = 1.0
        L.new(t.outputs["Color"], mul.inputs[6])
        mul.inputs[7].default_value = (base[0], base[1], base[2], 1)
        L.new(mul.outputs[2], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (base[0], base[1], base[2], 1)
    if "_BumpMap" in tex:
        tn = tex_node("_BumpMap", True)
        if tn:
            nm = N.new("ShaderNodeNormalMap")
            nm.inputs["Strength"].default_value = flt.get("_BumpScale", 1.0)
            L.new(tn.outputs["Color"], nm.inputs["Color"])
            L.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    ec = col.get("_EmissionColor", [0, 0, 0, 1])
    peak = max(ec[:3])
    if emissive and peak > 0.001:
        tint = (ec[0] / peak, ec[1] / peak, ec[2] / peak, 1)
        te = tex_node("_EmissionMap") if "_EmissionMap" in tex else None
        if te:
            em = N.new("ShaderNodeMix"); em.data_type = 'RGBA'; em.blend_type = 'MULTIPLY'
            em.inputs["Factor"].default_value = 1.0
            L.new(te.outputs["Color"], em.inputs[6])
            em.inputs[7].default_value = tint
            L.new(em.outputs[2], bsdf.inputs["Emission Color"])
        else:
            bsdf.inputs["Emission Color"].default_value = tint
        bsdf.inputs["Emission Strength"].default_value = min(peak, EMIT_MAX)
    mat["unity_mat"] = rel
    mat["unity_emission"] = ec[:3]
    _mat_cache[guid] = mat
    return mat


# ---------------------------------------------------------------- models
def _library():
    global _lib
    if _lib is None or _lib.name not in bpy.data.collections:
        _lib = bpy.data.collections.new("_unity_fbx_library")     # never linked to a scene
    return _lib


def _fbx(rel):
    """Import an FBX once; return [(mesh object, its world matrix as imported)]. Kept out of the scene."""
    if rel not in _fbx_cache:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, rel))
        new = [o for o in bpy.data.objects if o not in before]
        bpy.context.view_layer.update()
        out = [(o, o.matrix_world.copy()) for o in new if o.type == 'MESH']
        tops = [o for o in new if o.parent is None]
        for o in new:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            _library().objects.link(o)
        _fbx_cache[rel] = (out, len(tops))
    return _fbx_cache[rel]


def _place_model(rel, P, mods, coll, made):
    meshes, ntop = _fbx(rel)
    FX = _fx(rel)
    single = len(meshes) == 1        # one mesh: Unity puts it on the model's root, whatever else the file holds
    if single:
        dp, dq, ds = _decompose(C.inverted() @ meshes[0][1] @ FX.inverted())
    else:
        dp, dq, ds = [0, 0, 0], [0, 0, 0, 1], [1, 1, 1]
    p = [mods["P"].get(k, dp[i]) for i, k in enumerate("xyz")]
    q = [mods["R"].get(k, dq[i]) for i, k in enumerate("xyzw")]
    s = [mods["S"].get(k, ds[i]) for i, k in enumerate("xyz")]
    M_root = P @ _trs(p, q, s)
    for src, mw in meshes:
        o = src.copy()
        o.data = src.data.copy()
        o.parent = None
        o.animation_data_clear()         # e.g. the clock hands carry an FBX take that would snap them back
        coll.objects.link(o)
        M_u = M_root if single else M_root @ (C.inverted() @ mw @ FX.inverted())
        o.matrix_world = C @ M_u @ FX
        orig = [m.name.lower() if m else "" for m in o.data.materials]
        for idx, g in mods["mats"].items():
            m = material(g)
            if m and idx < len(o.data.materials):
                o.data.materials[idx] = m
            elif m and idx == 0 and not o.data.materials:
                o.data.materials.append(m)
        # Unity and Blender can number a model's material slots differently. Where exactly one override glows
        # and exactly one slot was the model's glass/light, make sure the glow lands on the glass.
        glow = [i for i, m in enumerate(o.data.materials) if m and max(m.get("unity_emission", [0, 0, 0])) > 0.001 and i in mods["mats"]]
        glass = [i for i, n in enumerate(orig) if any(k in n for k in ("light", "glass", "glow"))]
        if len(glow) == 1 and len(glass) == 1 and glow[0] != glass[0] and glass[0] in mods["mats"]:
            i, j = glow[0], glass[0]
            o.data.materials[i], o.data.materials[j] = o.data.materials[j], o.data.materials[i]
        o["unity_src"] = rel
        made.append(o)


def _parse_mods(body):
    mods = {"P": {}, "R": {}, "S": {}, "mats": {}, "active": True}
    per = {}
    for m in re.finditer(r"- target: \{fileID: (-?\d+)[^}]*\}\s*\n\s*propertyPath: ([^\n]+)\n\s*value: ?([^\n]*)\n\s*objectReference: \{fileID: (-?\d+)(?:, guid: (\w+))?", body):
        tgt, path, val, _, og = m.groups()
        if path.startswith(("m_LocalPosition.", "m_LocalRotation.", "m_LocalScale.")):
            per.setdefault(tgt, {})[path] = float(val)
        elif path.startswith("m_Materials.Array.data[") and og:
            mods["mats"][int(path[23:-1])] = og
        elif path == "m_IsActive" and val.strip() == "0":
            mods["active_targets"] = mods.get("active_targets", []) + [tgt]
    if per:
        # the instance's own root is the target that carries a position
        root = max(per, key=lambda t: sum(1 for k in per[t] if k.startswith("m_LocalPosition")) * 10 + len(per[t]))
        for path, v in per[root].items():
            mods[path[7]][path[-1]] = v          # m_Local[P]osition.x
    return mods


def _merge(inner, outer):
    out = {"P": dict(inner["P"]), "R": dict(inner["R"]), "S": dict(inner["S"]), "mats": dict(inner["mats"]), "active": True}
    for k in "PRS":
        out[k].update(outer[k])
    out["mats"].update(outer["mats"])
    return out


_EMPTY = {"P": {}, "R": {}, "S": {}, "mats": {}, "active": True}


def _instantiate(guid, P, mods, coll, made, lights, depth=0):
    rel = guids().get(guid)
    if not rel or depth > 6:
        return
    low = rel.lower()
    if low.endswith((".fbx", ".obj")):
        if low.endswith(".fbx"):
            _place_model(rel, P, mods, coll, made)
        return
    if not low.endswith(".prefab"):
        return
    d = _docs(rel)
    trs = {}
    for i, (t, b) in d.items():
        if t == 4 and "m_LocalPosition" in b and "m_Father" in b:
            g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", b)
            active = True
            if g and g.group(1) in d:
                active = "m_IsActive: 0" not in d[g.group(1)][1]
            trs[i] = dict(p=_vec(b, "m_LocalPosition", "xyz"), q=_vec(b, "m_LocalRotation", "xyzw"), s=_vec(b, "m_LocalScale", "xyz"),
                          father=re.search(r"m_Father: \{fileID: (-?\d+)\}", b).group(1), go=g.group(1) if g else None, active=active)
    world = {}

    def wm(i):
        if i not in world:
            t = trs[i]
            if t["father"] in trs:
                world[i] = wm(t["father"]) @ _trs(t["p"], t["q"], t["s"])
            else:   # prefab root: the outer instance's overrides replace its own values
                p = [mods["P"].get(k, t["p"][n]) for n, k in enumerate("xyz")]
                q = [mods["R"].get(k, t["q"][n]) for n, k in enumerate("xyzw")]
                s = [mods["S"].get(k, t["s"][n]) for n, k in enumerate("xyz")]
                world[i] = P @ _trs(p, q, s)
        return world[i]

    def is_active(i):
        while i in trs:
            if not trs[i]["active"]:
                return False
            i = trs[i]["father"]
        return True

    for i, (t, b) in d.items():
        if t == 1001:
            src = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", b)
            par = re.search(r"m_TransformParent: \{fileID: (-?\d+)", b).group(1)
            inner = _parse_mods(b)
            if not src:
                continue
            if par in trs:
                if is_active(par):
                    _instantiate(src.group(1), wm(par), inner, coll, made, lights, depth + 1)
            else:
                _instantiate(src.group(1), P, _merge(inner, mods), coll, made, lights, depth + 1)
        elif t == 108:      # Light on a plain GameObject
            g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", b)
            if not g:
                continue
            tr = next((k for k, v in trs.items() if v["go"] == g.group(1)), None)
            if tr is None or not is_active(tr) or "m_Enabled: 0" in b:
                continue
            c = re.search(r"m_Color: \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+)", b)
            inten = re.search(r"m_Intensity: ([-\d.e]+)", b)
            rng = re.search(r"m_Range: ([-\d.e]+)", b)
            typ = re.search(r"m_Type: (\d+)", b)
            lights.append(dict(pos=C @ wm(tr).to_translation(), color=[float(c.group(k)) for k in (1, 2, 3)] if c else [1, 1, 1],
                               intensity=float(inten.group(1)) if inten else 1.0, range=float(rng.group(1)) if rng else 10.0,
                               type=int(typ.group(1)) if typ else 2))


def import_prefab(rel, name=None, collection=None, light_scale=0.0):
    """Import a prefab; returns the Empty everything is parented to (at the prefab origin).

    light_scale > 0 also adds the prefab's point lights, with watts = Unity intensity * light_scale.
    """
    rel = rel.replace("\\", "/")
    guid = next((g for g, p in guids().items() if p == rel), None)
    if guid is None:
        raise FileNotFoundError(rel)
    coll = collection or bpy.context.scene.collection
    made, lights = [], []
    _instantiate(guid, Matrix.Identity(4), _EMPTY, coll, made, lights)
    root = bpy.data.objects.new(name or os.path.splitext(os.path.basename(rel))[0], None)
    coll.objects.link(root)
    bpy.context.view_layer.update()
    for o in made:
        mw = o.matrix_world.copy()
        o.parent = root
        o.matrix_world = mw
    if light_scale > 0:
        for k, l in enumerate(lights):
            if l["type"] != 2:
                continue
            ld = bpy.data.lights.new(f"{root.name}_L{k}", 'POINT')
            ld.color = l["color"]; ld.energy = l["intensity"] * light_scale; ld.shadow_soft_size = 0.15
            lo = bpy.data.objects.new(ld.name, ld)
            coll.objects.link(lo)
            lo.location = l["pos"]
            lo.parent = root
    root["kids"] = [o.name for o in made]
    root["lights"] = len(lights)
    pts = [o.matrix_world @ Vector(c) for o in made for c in o.bound_box]
    if pts:
        root["min"] = [min(p[k] for p in pts) for k in range(3)]
        root["max"] = [max(p[k] for p in pts) for k in range(3)]
    return root
