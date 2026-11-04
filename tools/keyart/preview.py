import bpy, os, sys, math, mathutils
A = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/") + "/Assets/"
OUT = sys.argv[sys.argv.index("--")+1]
files = sys.argv[sys.argv.index("--")+2:]
print("BLENDER", bpy.app.version_string)
for i,f in enumerate(files):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=A+f)
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.context.view_layer.update()
    pts=[o.matrix_world@mathutils.Vector(c) for o in objs for c in o.bound_box]
    mn=mathutils.Vector([min(p[k] for p in pts) for k in range(3)]); mx=mathutils.Vector([max(p[k] for p in pts) for k in range(3)])
    c=(mn+mx)/2; r=(mx-mn).length/2
    print("==", f, "min", tuple(round(v,2) for v in mn), "max", tuple(round(v,2) for v in mx))
    cam=bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
    d=mathutils.Vector((1.0,-1.6,0.7)).normalized()
    cam.location=c+d*r*2.6
    cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.scene.camera=cam
    sun=bpy.data.objects.new("sun", bpy.data.lights.new("sun",'SUN')); bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler=(0.8,0.2,0.6); sun.data.energy=3
    w=bpy.data.worlds.new("w"); bpy.context.scene.world=w; w.use_nodes=True; w.node_tree.nodes["Background"].inputs[1].default_value=0.6
    s=bpy.context.scene; s.render.engine='BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
    s.render.resolution_x=s.render.resolution_y=320
    s.render.filepath=f"{OUT}/p{i:02d}.png"
    bpy.ops.render.render(write_still=True)
