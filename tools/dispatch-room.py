"""Builds the Dispatch lobby's back room in Blender and exports it for Unity.

Usage: blender -b --factory-startup --python tools/dispatch-room.py -- [preview.png]
Writes Assets/Models/Dispatch/Dispatch_Room.fbx (1 unit = 1 Unity unit; the textures are tools/dispatch-art.py's, in
Assets/Textures/Dispatch). With a path, also renders a preview from about where the menu camera would sit, with the
game's scooter on each podium for scale (the preview's scooters, lights and camera are not exported).
See EDITOR-TODO.md 1c for setting it up in the menu scene.

Blender is Z up and looks along +Y from the front: the camera is at -Y, the back wall at +Y, seat 1 on the left (-X).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
TEX = os.path.join(ROOT, "Assets", "Textures", "Dispatch")
OUT = os.path.join(ROOT, "Assets", "Models", "Dispatch", "Dispatch_Room.fbx")
SCOOTER = os.path.join(ROOT, "Assets", "Models", "Vehicle Models", "Scooter_faceDelete.fbx")

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = ARGS[0] if ARGS else None

# The layout, in Unity units. The scooter is about 9 long and 5.6 tall (1.1 x its model), its ball 2.15 across
SEATS = [-18.0, -6.0, 6.0, 18.0]  # podium centres along X, seat 1 first
PODIUM_R = 4.6
PODIUM_H = 0.8
SPAWN_UP = 1.16  # how far the menu's spawn points sit above their ground (the scene's booths)
ROOM_W = 104.0  # wide and tall enough to fill a 60 degree, 16:9 view of the board
WALL_Y = 14.0
WALL_H = 42.0
FLOOR_FRONT = -20.0
BOARD_W, BOARD_BOTTOM, BOARD_TOP = 54.0, 12.0, 23.0

# Mockup colours (linear-ish values for Blender)
DEEP = (0.044, 0.0042, 0.155)
PODIUM = (0.10, 0.03, 0.26)
SLIME = (0.018, 0.46, 0.17)
SEAT_COLOURS = [(0.02, 0.35, 0.015), (1.0, 0.0, 0.0), (1.0, 0.24, 0.0), (0.033, 0.033, 0.033)]  # preview only


def mat_textured(name, image, rough=0.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX, image))
    m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough
    return m


def mat_flat(name, rgb, rough=0.8, emit=0.0, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1)
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


ROOM = None  # the parent of everything exported


def finish(o, name, mat, tile=None):
    o.name = name
    o.data.name = name
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.data.materials.append(mat)
    if tile:
        world_uv(o, tile)
    o.parent = ROOM
    return o


def box(name, size, at, mat, tile=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at)
    o = bpy.context.active_object
    o.scale = size
    return finish(o, name, mat, tile)


def cylinder(name, r, h, at, mat, sides=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=r, depth=h, location=at)
    o = bpy.context.active_object
    for p in o.data.polygons:
        p.use_smooth = len(p.vertices) == 4  # smooth sides, flat caps
    return finish(o, name, mat)


def torus(name, r, thick, at, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=thick, major_segments=64, minor_segments=12,
                                     location=at, rotation=rot)
    o = bpy.context.active_object
    for p in o.data.polygons:
        p.use_smooth = True
    return finish(o, name, mat)


def world_uv(o, tile):
    """Box-projects UVs from world position, so a texture tiles once every `tile` units on every face"""
    me = o.data
    uv = me.uv_layers.new(name="UVMap") if not me.uv_layers else me.uv_layers[0]
    loc = o.matrix_world.translation
    for p in me.polygons:
        n = p.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co + loc
            if ax == 2:
                u, v = co.x, co.y
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.y, co.z
            uv.data[li].uv = (u / tile, v / tile)


def empty(name, at):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.location = at
    bpy.context.scene.collection.objects.link(e)
    e.parent = ROOM
    return e


def build():
    global ROOM
    ROOM = bpy.data.objects.new("Dispatch_Room", None)
    bpy.context.scene.collection.objects.link(ROOM)

    wall = mat_textured("Depot_Wall", "Depot_Wall.png")
    floor = mat_textured("Depot_Floor", "Depot_Floor.png", rough=0.7)
    cork = mat_textured("Cork", "Cork.png", rough=1.0)
    wood = mat_textured("Wood_Frame", "Wood_Frame.png", rough=0.6)
    trim = mat_flat("Trim", DEEP)
    podium = mat_flat("Podium", PODIUM, rough=0.5)
    base = mat_flat("Podium_Base", DEEP, rough=0.6)
    stripe = mat_flat("Floor_Stripe", SLIME, rough=0.5)
    card = mat_flat("Cardboard", (0.42, 0.22, 0.08), rough=0.95)
    tape = mat_flat("Tape", (0.80, 0.70, 0.45), rough=0.4)
    label = mat_flat("Label", (0.95, 0.93, 0.80), rough=0.9)
    metal = mat_flat("Shelf_Metal", (0.06, 0.04, 0.10), rough=0.4, metal=0.6)
    clock_face = mat_flat("Clock_Face", (0.95, 0.93, 0.80), rough=0.6)
    clock_hand = mat_flat("Clock_Hands", (0.02, 0.01, 0.03))

    # The room
    depth = WALL_Y - FLOOR_FRONT
    box("Depot_Floor", (ROOM_W, depth, 0.2), (0, (WALL_Y + FLOOR_FRONT) / 2, -0.1), floor, tile=8)
    box("Depot_Wall", (ROOM_W, 0.4, WALL_H), (0, WALL_Y + 0.2, WALL_H / 2), wall, tile=8)
    box("Skirting", (ROOM_W, 0.3, 0.9), (0, WALL_Y - 0.15, 0.45), trim)
    box("Floor_Stripe", (ROOM_W, 0.5, 0.02), (0, -7.5, 0.01), stripe)
    # A painted bay line round the podiums' end of the floor, and a second shelf and pallet out at the sides
    for side in (-1, 1):
        box("Floor_Stripe_Side", (0.5, 21.5, 0.02), (side * 26.0, 3.25, 0.01), stripe)

    # The corkboard and its frame
    face = WALL_Y - 0.15
    mid = (BOARD_BOTTOM + BOARD_TOP) / 2
    box("Corkboard", (BOARD_W, 0.3, BOARD_TOP - BOARD_BOTTOM), (0, face, mid), cork, tile=6)
    f, fd = 0.8, 0.6
    fy = WALL_Y - fd / 2
    box("Corkboard_Frame_Top", (BOARD_W + f * 2, fd, f), (0, fy, BOARD_TOP + f / 2), wood, tile=6)
    box("Corkboard_Frame_Bottom", (BOARD_W + f * 2, fd, f), (0, fy, BOARD_BOTTOM - f / 2), wood, tile=6)
    for side, x in (("Left", -(BOARD_W + f) / 2), ("Right", (BOARD_W + f) / 2)):
        box("Corkboard_Frame_" + side, (f, fd, BOARD_TOP - BOARD_BOTTOM), (x, fy, mid), wood, tile=6)
    # Where the tickets go: one point above each podium, on the board's face
    for i, x in enumerate(SEATS):
        empty("Seat%d_Ticket" % (i + 1), (x, face - 0.2, mid))

    # The podiums: a body, a dark base step and a glowing rim, one material per seat so each takes its company colour
    for i, x in enumerate(SEATS):
        n = i + 1
        cylinder("Podium%d_Base" % n, PODIUM_R + 0.4, 0.25, (x, 0, 0.125), base)
        cylinder("Podium%d" % n, PODIUM_R, PODIUM_H, (x, 0, PODIUM_H / 2), podium)
        torus("Podium%d_Rim" % n, PODIUM_R, 0.16, (x, 0, PODIUM_H), mat_flat("Podium%d_Rim" % n, (1, 1, 1), emit=1.0))
        empty("Seat%d_Spawn" % n, (x, 0, PODIUM_H + SPAWN_UP))

    # Props. Left: a stack of parcels; right: a shelf of them; the wall: a clock
    parcels(card, tape, label)
    shelf(metal, card, tape, label, 26.2, "")
    shelf(metal, card, tape, label, -45.0, "_Far")
    pallet(card, tape, label, trim)
    clock(trim, clock_face, clock_hand)


def parcel(name, size, at, card, tape, label, turn=0.0, with_label=True):
    o = box(name, size, at, card)
    o.rotation_euler.z = turn
    sx, sy, sz = size
    t = box(name + "_Tape", (sx + 0.02, sy * 0.22, sz + 0.02), at, tape)
    t.rotation_euler.z = turn
    if with_label:
        # On the parcel's front face, turned with it
        offset = Matrix.Rotation(turn, 3, "Z") @ Vector((-sx * 0.15, -sy / 2 - 0.01, 0))
        l = box(name + "_Label", (sx * 0.35, 0.02, sz * 0.3), Vector(at) + offset, label)
        l.rotation_euler.z = turn
    return o


def parcels(card, tape, label):
    # Big at the bottom, smaller higher up, each a little turned
    stack = [
        ((4.2, 3.6, 3.0), (-28.6, 9.6, 1.5), 0.05),
        ((3.4, 3.0, 2.4), (-28.4, 9.8, 4.2), -0.10),
        ((2.6, 2.4, 2.0), (-28.9, 9.7, 6.4), 0.14),
        ((3.0, 2.8, 2.2), (-24.8, 10.4, 1.1), -0.18),
    ]
    for i, (size, at, turn) in enumerate(stack):
        parcel("Parcel_L%d" % (i + 1), size, at, card, tape, label, turn)
    parcel("Parcel_Floor", (2.4, 2.0, 1.6), (25.2, 2.0, 0.8), card, tape, label, 0.35)


def shelf(metal, card, tape, label, x0, tag):
    """A metal shelf against the wall, from x0 for 5.4 units, with parcels on it"""
    x1, y0, y1 = x0 + 5.4, WALL_Y - 4.0, WALL_Y - 0.2
    posts = []
    for sx in (x0, x1):
        for sy in (y0, y1):
            posts.append(box("Shelf%s_Post" % tag, (0.3, 0.3, 11.0), (sx, sy, 5.5), metal))
    for k, z in enumerate((0.6, 4.0, 7.4, 10.8)):
        box("Shelf%s_Board%d" % (tag, k + 1), (x1 - x0 + 0.5, y1 - y0 + 0.5, 0.2), ((x0 + x1) / 2, (y0 + y1) / 2, z), metal)
    goods = [
        ((2.2, 2.6, 1.8), (1.4, 12.0, 1.6), 0.08),
        ((2.4, 2.4, 2.2), (4.0, 12.1, 1.8), -0.06),
        ((3.4, 2.8, 2.4), (2.4, 12.0, 5.3), 0.03),
        ((1.8, 2.0, 1.4), (1.2, 12.2, 8.2), -0.12),
        ((2.0, 2.2, 2.0), (3.8, 12.0, 8.5), 0.10),
    ]
    for i, (size, (dx, y, z), turn) in enumerate(goods):
        parcel("Parcel_Shelf%s%d" % (tag, i + 1), size, (x0 + dx, y, z), card, tape, label, turn, with_label=False)
    # The posts as one object
    bpy.ops.object.select_all(action="DESELECT")
    for o in posts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = posts[0]
    bpy.ops.object.join()
    posts[0].name = "Shelf%s_Posts" % tag


def pallet(card, tape, label, trim):
    """A wooden pallet stacked with parcels, out on the right"""
    x, y = 40.0, 7.0
    wood = bpy.data.materials["Wood_Frame"]
    for k in range(5):
        box("Pallet_Slat", (6.0, 0.9, 0.2), (x, y - 2.6 + k * 1.3, 0.9), wood, tile=6)
    for k in (-1, 0, 1):
        box("Pallet_Block", (6.0, 0.9, 0.7), (x, y + k * 2.6, 0.35), wood, tile=6)
    stack = [
        ((2.8, 2.6, 2.2), (x - 1.5, y - 1.2, 2.1), 0.04),
        ((2.6, 2.6, 2.4), (x + 1.5, y - 1.0, 2.2), -0.07),
        ((2.8, 2.4, 2.0), (x - 1.4, y + 1.4, 2.0), -0.03),
        ((2.6, 2.6, 2.2), (x + 1.4, y + 1.3, 2.1), 0.06),
        ((3.6, 3.0, 2.2), (x, y, 4.3), 0.12),
    ]
    for i, (size, at, turn) in enumerate(stack):
        parcel("Parcel_Pallet%d" % (i + 1), size, at, card, tape, label, turn)


def clock(trim, face, hands):
    x, z = -29.6, 19.0
    y = WALL_Y - 0.25
    rot = (math.pi / 2, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1.6, depth=0.3, location=(x, y, z), rotation=rot)
    finish(bpy.context.active_object, "Clock_Face", face)
    torus("Clock_Rim", 1.65, 0.18, (x, y - 0.12, z), trim, rot)
    # Ten to two, the shop-window time
    for name, length, angle in (("Clock_Hour", 0.9, math.radians(60)), ("Clock_Minute", 1.3, math.radians(-60))):
        h = box(name, (0.14, 0.06, length), (0, 0, 0), hands)
        h.data.transform(Matrix.Translation((0, 0, length / 2)))
        h.location = (x, y - 0.2, z)
        h.rotation_euler = (0, angle, 0)
    for k in range(12):
        a = k * math.pi / 6
        box("Clock_Tick", (0.1, 0.04, 0.28 if k % 3 else 0.42), (x + math.sin(a) * 1.3, y - 0.17, z + math.cos(a) * 1.3),
            hands).rotation_euler = (0, a, 0)
    ticks = [o for o in bpy.data.objects if o.name.startswith("Clock_Tick")]
    bpy.ops.object.select_all(action="DESELECT")
    for o in ticks:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ticks[0]
    bpy.ops.object.join()
    ticks[0].name = "Clock_Ticks"


def export():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        o.select_set(o == ROOM or o.parent == ROOM)
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"EMPTY", "MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             path_mode="RELATIVE", use_mesh_modifiers=True)
    print("EXPORTED", os.path.relpath(OUT, ROOT))


def preview(path):
    scene = bpy.context.scene
    # The rims in their mockup colours, and the room dimmer than the podiums (the board at about 75%)
    for i, rgb in enumerate(SEAT_COLOURS):
        bsdf = bpy.data.materials["Podium%d_Rim" % (i + 1)].node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1)
        bsdf.inputs["Emission Strength"].default_value = 1.5
    # A scooter on each podium for scale (seat 4 empty, as in the mockup)
    for i, x in enumerate(SEATS[:3]):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=SCOOTER)
        new = [o for o in bpy.data.objects if o not in before]
        top = [o for o in new if o.parent is None]
        for o in top:
            o.scale = Vector(o.scale) * 1.1
            o.rotation_euler.z += math.radians(-35)
        bpy.context.view_layer.update()
        lo = min((o.matrix_world @ Vector(c)).z for o in new if o.type == "MESH" for c in o.bound_box)
        cx = [(o.matrix_world @ Vector(c)) for o in new if o.type == "MESH" for c in o.bound_box]
        mx = sum(v.x for v in cx) / len(cx)
        my = sum(v.y for v in cx) / len(cx)
        for o in top:
            o.location += Vector((x - mx, -my, PODIUM_H - lo))
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.005, 0.05, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    # Room fill, cool and dim
    bpy.ops.object.light_add(type="AREA", location=(0, -14, 30))
    fill = bpy.context.active_object
    fill.data.energy = 15000
    fill.data.size = 60
    fill.data.color = (0.85, 0.75, 1.0)
    fill.rotation_euler = (math.radians(35), 0, 0)
    # A spotlight on each podium
    for x in SEATS:
        bpy.ops.object.light_add(type="SPOT", location=(x, -12, 24))
        s = bpy.context.active_object
        s.data.energy = 60000 if x != SEATS[3] else 8000
        s.data.spot_size = math.radians(28)
        s.data.spot_blend = 0.4
        s.data.color = (1.0, 0.95, 0.85)
        d = Vector((x, 0, 2)) - s.location
        s.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    # The camera: Unity's 60 degree vertical field of view, 16:9
    cam = bpy.data.cameras.new("Cam")
    cam.sensor_fit = "VERTICAL"
    cam.angle_y = math.radians(60)
    co = bpy.data.objects.new("Cam", cam)
    scene.collection.objects.link(co)
    co.location = (0, -30, 13)
    co.rotation_euler = (Vector((0, 8, 11)) - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", path)


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build()
    export()
    if PREVIEW:
        preview(PREVIEW)
