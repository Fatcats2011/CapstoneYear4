import bpy, os, sys, mathutils
A = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/") + "/Assets/"
out=sys.argv[sys.argv.index("--")+1]; files=sys.argv[sys.argv.index("--")+2:]
for fi,f in enumerate(files):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=A+f)
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.context.view_layer.update()
    pts=[o.matrix_world@mathutils.Vector(c) for o in objs for c in o.bound_box]
    mn=mathutils.Vector([min(p[k] for p in pts) for k in range(3)]);mx=mathutils.Vector([max(p[k] for p in pts) for k in range(3)])
    c=(mn+mx)/2;r=(mx-mn).length/2
    w=bpy.data.worlds.new("w");bpy.context.scene.world=w;w.use_nodes=True;w.node_tree.nodes["Background"].inputs[1].default_value=1.0
    s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL'
    s.display.shading.show_cavity=True
    s.render.resolution_x=s.render.resolution_y=256
    for i,d in enumerate([(1,0,0.3),(-1,0,0.3),(0,1,0.3),(0,-1,0.3)]):
        cam=bpy.data.objects.new("c",bpy.data.cameras.new("c"));bpy.context.scene.collection.objects.link(cam)
        cam.location=c+mathutils.Vector(d).normalized()*r*2.4;cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
        s.camera=cam;s.render.filepath=f"{out}_{fi}_{i}.png";bpy.ops.render.render(write_still=True)
