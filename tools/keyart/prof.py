import bpy, os, mathutils
A = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/") + "/Assets/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=A+"Models/City Center Models/Clock Tower_Textured.fbx")
o=[o for o in bpy.context.scene.objects if o.type=='MESH'][0]
bpy.context.view_layer.update()
pts=[o.matrix_world@mathutils.Vector(c) for c in o.bound_box]
mn=mathutils.Vector([min(p[k] for p in pts) for k in range(3)]);mx=mathutils.Vector([max(p[k] for p in pts) for k in range(3)])
c=(mn+mx)/2;H=mx.z-mn.z
print("BB",tuple(mn),tuple(mx))
dg=bpy.context.evaluated_depsgraph_get()
for i in range(0,41):
    z=mn.z+H*i/40
    for dy in (0,):
        hit,loc,n,*_=bpy.context.scene.ray_cast(dg,mathutils.Vector((mx.x+5,c.y+dy,z)),mathutils.Vector((-1,0,0)))
        print("PROF %.2f rel=%.3f x=%s"%(z,(z-mn.z)/H, round(loc.x,3) if hit else None))
# hub: scan y,z around clock region for most protruding point
best=None
for zi in range(60,90):
    z=mn.z+H*zi/100
    for yi in range(-10,11):
        y=c.y+yi*0.05
        hit,loc,*_=bpy.context.scene.ray_cast(dg,mathutils.Vector((mx.x+5,y,z)),mathutils.Vector((-1,0,0)))
        if hit and (best is None or loc.x>best[0]): best=(loc.x,y,z)
print("HUB",best, "rel", (best[2]-mn.z)/H, "dy", best[1]-c.y)
