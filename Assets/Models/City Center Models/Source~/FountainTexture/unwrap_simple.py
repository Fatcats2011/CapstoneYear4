"""Fountain basin UVs: one tiling stone texture, 1 tile = 1 unit.
Ring faces: u wraps round the octagon (whole tiles per ring), v = height (walls) or radius (tops).
Floor, column top and underside: flat from above."""
import bpy, bmesh, math, sys
out_fbx, save = sys.argv[sys.argv.index('--')+1:][:2]
TILE = 1.0                      # metres per texture repeat
ob = bpy.data.objects['Fountain']; me = ob.data
slot = [i for i, m in enumerate(me.materials) if m and m.name == 'Horns'][0]
bm = bmesh.new(); bm.from_mesh(me); uvl = bm.loops.layers.uv['UVMap']
faces = [f for f in bm.faces if f.material_index == slot]
xs = [v.co.x for f in faces for v in f.verts]; ys = [v.co.y for f in faces for v in f.verts]
cx = (min(xs) + max(xs)) / 2; cy = (min(ys) + max(ys)) / 2
def r_of(co): return math.hypot(co.x - cx, co.y - cy)
# two ring sizes so blocks stay roughly the same width on the outer basin and on the pedestal
N_OUTER = round(2 * math.pi * 0.9 / TILE)
N_INNER = round(2 * math.pi * 0.36 / TILE * 2) / 2      # half tiles allowed (one block = half a tile)
RIM_V0 = 0.73                                           # tops: start a course at the rim's inner edge
for f in faces:
    n = f.normal; c = f.calc_center_median(); rc = r_of(c)
    flat = n.z < -0.6 or (n.z > 0.6 and (rc < 0.3 or (c.z < 0.14 and rc < 0.8)))
    if flat:
        for l in f.loops: l[uvl].uv = ((l.vert.co.x - cx) / TILE, (l.vert.co.y - cy) / TILE)
        continue
    N = N_INNER if rc < 0.45 else N_OUTER
    ca = math.atan2(c.y - cy, c.x - cx)
    for l in f.loops:
        a = math.atan2(l.vert.co.y - cy, l.vert.co.x - cx)
        while a - ca > math.pi: a -= 2 * math.pi
        while a - ca < -math.pi: a += 2 * math.pi
        u = a / (2 * math.pi) * N
        v = (r_of(l.vert.co) - RIM_V0) / TILE if n.z > 0.6 else l.vert.co.z / TILE
        l[uvl].uv = (u, v)
bm.to_mesh(me); bm.free(); me.update()
if save == '1': bpy.ops.wm.save_mainfile()
bpy.ops.export_scene.fbx(filepath=out_fbx, object_types={'MESH'}, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
print('DONE', N_OUTER, N_INNER)
