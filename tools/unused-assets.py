"""Lists the files under Assets/ that nothing in the build uses.

Follows Unity's GUID references (the project saves its assets as text) from what the build starts from:
- the enabled and disabled scenes in Build Settings;
- everything ProjectSettings names;
- Resources, Editor, StreamingAssets, Plugins and Gizmos folders;
- every script and shader;
- any "Assets/..." path a script names.

A file nothing reaches is "unused". The list is for people to review (docs/unused-assets.md), not to delete
blindly: source art, bake data and assets loaded by name don't show up as references.

Usage:  python tools/unused-assets.py [output file]   default: Logs/unused-assets.txt
Output: one line per unused file: its size in bytes, a tab, its path (forward slashes). A summary by top folder on stdout.
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Logs", "unused-assets.txt")
GUID = re.compile(rb"guid: ([0-9a-f]{32})")
# Assets Unity saves as text, which can reference others by GUID
TEXT = {".unity", ".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController", ".playable", ".mask",
        ".lighting", ".spriteatlas", ".signal", ".preset", ".shadergraph", ".shadersubgraph", ".physicMaterial",
        ".physicsMaterial2D", ".flare", ".guiskin", ".fontsettings", ".renderTexture", ".cubemap", ".terrainlayer",
        ".brush", ".giparams", ".mixer", ".vfx", ".asmdef", ".asmref"}
ROOT_FOLDERS = ("Resources", "Editor", "StreamingAssets", "Plugins", "Gizmos", "Editor Default Resources")
ROOT_FILES = (".cs", ".asmdef", ".shader", ".hlsl", ".cginc", ".compute")

def guids_in(path):
    with open(path, "rb") as fh:
        return set(g.decode() for g in GUID.findall(fh.read()))

# Every asset by its GUID (from its .meta)
by_guid, files = {}, []
for dp, ds, fs in os.walk(ASSETS):
    for f in fs:
        path = os.path.normpath(os.path.join(dp, f))
        if f.endswith(".meta"):
            with open(path, "rb") as fh:
                m = GUID.search(fh.read(4000))
            if m:
                by_guid[m.group(1).decode()] = path[:-5]
        else:
            files.append(path)

def references(path):
    """GUIDs a file names: its own content if Unity saves it as text, and its .meta (a model's external materials)"""
    found = set()
    if os.path.isfile(path) and os.path.splitext(path)[1] in TEXT:
        found |= guids_in(path)
    if os.path.exists(path + ".meta"):
        found |= guids_in(path + ".meta")
    return found

roots = set()
def add_root(path):
    path = os.path.normpath(path)
    if os.path.isdir(path):
        for dp, ds, fs in os.walk(path):
            roots.update(os.path.normpath(os.path.join(dp, f)) for f in fs if not f.endswith(".meta"))
    elif os.path.exists(path):
        roots.add(path)

for dp, ds, fs in os.walk(os.path.join(ROOT, "ProjectSettings")):
    for f in fs:
        for g in guids_in(os.path.join(dp, f)):
            if g in by_guid:
                add_root(by_guid[g])
build = open(os.path.join(ROOT, "ProjectSettings", "EditorBuildSettings.asset"), encoding="utf-8").read()
for scene in re.findall(r"path: (Assets/.+\.unity)", build):
    add_root(os.path.join(ROOT, scene))
for dp, ds, fs in os.walk(ASSETS):
    if os.path.basename(dp) in ROOT_FOLDERS:
        add_root(dp)
    for f in fs:
        if f.endswith(ROOT_FILES):
            roots.add(os.path.normpath(os.path.join(dp, f)))
        if f.endswith(".cs"):
            source = open(os.path.join(dp, f), encoding="utf-8", errors="ignore").read()
            for named in re.findall(r'"(Assets/[^"]+)"', source):
                add_root(os.path.join(ROOT, named))

reached, stack = set(), list(roots)
while stack:
    path = stack.pop()
    if path in reached:
        continue
    reached.add(path)
    for g in references(path):
        target = by_guid.get(g)
        if target and os.path.normpath(target) not in reached:
            stack.append(os.path.normpath(target))

unused = sorted(f for f in files if f not in reached)
os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
    for f in unused:
        fh.write("%d\t%s\n" % (os.path.getsize(f), os.path.relpath(f, ROOT).replace(os.sep, "/")))

by_top = {}
for f in unused:
    parts = os.path.relpath(f, ASSETS).split(os.sep)
    top = parts[0] if len(parts) > 1 else "(loose in Assets/)"
    count, size = by_top.get(top, (0, 0))
    by_top[top] = (count + 1, size + os.path.getsize(f))
print("%d files under Assets/, %d unused (%.0f MB). List: %s" % (
    len(files), len(unused), sum(s for c, s in by_top.values()) / 1e6, OUT))
for top, (count, size) in sorted(by_top.items(), key=lambda kv: -kv[1][1]):
    print("%9.1f MB %6d  %s" % (size / 1e6, count, top))
