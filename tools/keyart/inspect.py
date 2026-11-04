import bpy, sys, os
A = "D:/.PersonalWork/DoA/CapstoneYear4/Assets/"
files = sys.argv[sys.argv.index("--")+1:]
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=A+f)
    except Exception as e:
        print("FAIL", f, e); continue
    print("=====", f)
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': 
            print("  ", o.type, o.name); continue
        mats = [m.name for m in o.data.materials if m]
        print("   MESH %-30s dims=%s verts=%d mats=%s parent=%s" % (o.name, tuple(round(d,2) for d in o.dimensions), len(o.data.vertices), mats, o.parent.name if o.parent else None))
    imgs = [i.filepath for i in bpy.data.images]
    if imgs: print("   images:", imgs)
