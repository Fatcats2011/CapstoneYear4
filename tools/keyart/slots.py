import bpy, sys, math, mathutils
A="D:/.PersonalWork/DoA/CapstoneYear4/Assets/"
out=sys.argv[sys.argv.index("--")+1]; f=sys.argv[sys.argv.index("--")+2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=A+f)
cols=[(1,0,0),(0,1,0),(0,0,1),(1,1,0),(1,0,1),(0,1,1),(1,1,1),(0.3,0.3,0.3)]
seen={}
for o in bpy.context.scene.objects:
    if o.type!='MESH': continue
    for m in o.data.materials:
        if m and m.name not in seen:
            c=cols[len(seen)%len(cols)]; seen[m.name]=c
            m.use_nodes=True; b=m.node_tree.nodes["Principled BSDF"]
            for l in list(b.inputs["Base Color"].links): m.node_tree.links.remove(l)
            b.inputs["Base Color"].default_value=(*c,1); m.diffuse_color=(*c,1)
print("SLOTS",seen)
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
pts=[o.matrix_world@mathutils.Vector(c) for o in objs for c in o.bound_box]
mn=mathutils.Vector([min(p[k] for p in pts) for k in range(3)]);mx=mathutils.Vector([max(p[k] for p in pts) for k in range(3)])
c=(mn+mx)/2;r=(mx-mn).length/2
for i,d in enumerate([(0,-1,0.15),(0,1,0.15),(1,0,0.15),(-1,0,0.15)]):
    cam=bpy.data.objects.new("c",bpy.data.cameras.new("c"));bpy.context.scene.collection.objects.link(cam)
    cam.location=c+mathutils.Vector(d).normalized()*r*2.3;cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
    bpy.context.scene.camera=cam
    w=bpy.data.worlds.new("w");bpy.context.scene.world=w;w.use_nodes=True;w.node_tree.nodes["Background"].inputs[1].default_value=1.5
    s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.display.shading.color_type='MATERIAL'
    s.render.resolution_x=240;s.render.resolution_y=480;s.render.filepath=f"{out}_{i}.png"
    bpy.ops.render.render(write_still=True)
