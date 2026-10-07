"""Compiles the game's and the editor's scripts (tests included) in about 30 s, without starting Unity.

Copies Unity's generated Assembly-CSharp.csproj and Assembly-CSharp-Editor.csproj with their file lists rebuilt from
the scripts on disk (the generated ones go stale when files are added, moved or deleted outside the editor), then
builds them with dotnet against Unity's reference assemblies. Package assemblies come from the test mirror
(tools/run-tests.sh), which compiles the current packages.

Needs: the .NET SDK (dotnet); the generated .csproj files (open the project in Unity once, or Assets -> Open C#
Project); a test mirror that has run once.
Usage:  python tools/compile-check.py [--warnings]   --warnings also lists unused locals and private fields
Env:    DOA_UNITY   Unity.exe (default: Unity Hub's 2022.3.62f3)
        DOA_MIRROR  the test mirror (default: _doa_test_mirror/<project folder> next to the project, as run-tests.sh)
Exit:   0 when it compiles. Runs one at a time (a lock in Temp/).
"""
import atexit, os, re, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.environ.get("DOA_UNITY", r"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe")
FRAMEWORK = os.path.join(os.path.dirname(UNITY), "Data", "MonoBleedingEdge", "lib", "mono", "4.7.1-api")
MIRROR = os.environ.get("DOA_MIRROR", os.path.join(os.path.dirname(ROOT), "_doa_test_mirror", os.path.basename(ROOT)))
FIRSTPASS = ("Plugins", "Standard Assets", "Pro Standard Assets")
UNUSED_WARNINGS = re.compile(r"warning CS(0168|0169|0219|8321)")

# One check at a time: checks share the generated project files and obj/
os.makedirs(os.path.join(ROOT, "Temp"), exist_ok=True)
LOCK = os.path.join(ROOT, "Temp", "compile-check.lock")
while True:
    try:
        os.close(os.open(LOCK, os.O_CREAT | os.O_EXCL))
        break
    except FileExistsError:
        if time.time() - os.path.getmtime(LOCK) > 900:
            os.remove(LOCK)  # left by a check that was killed
        time.sleep(3)
atexit.register(lambda: os.path.exists(LOCK) and os.remove(LOCK))

# Which assembly each script belongs to, as Unity decides it: folders with an .asmdef and the firstpass folders have
# their own assemblies; a script under any Editor folder is the editor's
asmdef_dirs = [os.path.normpath(dp) for dp, ds, fs in os.walk(os.path.join(ROOT, "Assets"))
               if any(f.endswith(".asmdef") for f in fs)]

def assembly_of(path):
    if any(path.startswith(d + os.sep) for d in asmdef_dirs):
        return None
    rel = os.path.relpath(path, os.path.join(ROOT, "Assets")).split(os.sep)
    if rel[0] in FIRSTPASS:
        return None
    return "editor" if "Editor" in rel[:-1] else "game"

scripts = {"game": [], "editor": []}
for dp, ds, fs in os.walk(os.path.join(ROOT, "Assets")):
    for f in fs:
        if f.endswith(".cs"):
            path = os.path.normpath(os.path.join(dp, f))
            kind = assembly_of(path)
            if kind:
                scripts[kind].append(os.path.relpath(path, ROOT))

def write_project(source, target, files, swap=None):
    text = open(os.path.join(ROOT, source), encoding="utf-8-sig").read()
    text = re.sub(r'\s*<Compile Include="[^"]*"\s*/>', "", text)
    items = "".join('    <Compile Include="%s" />\n' % f for f in sorted(files))
    head, tail = text.rsplit("</Project>", 1)  # the last one: references carry <Project> elements too
    text = head + "  <ItemGroup>\n" + items + "  </ItemGroup>\n</Project>" + tail
    mirror_assemblies = os.path.relpath(os.path.join(MIRROR, "Library", "ScriptAssemblies"), ROOT)
    text = text.replace("<HintPath>Library\\ScriptAssemblies\\", "<HintPath>" + mirror_assemblies + "\\")
    if swap:
        text = text.replace(*swap)
    open(os.path.join(ROOT, target), "w", encoding="utf-8").write(text)

for needed in ("Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj"):
    if not os.path.exists(os.path.join(ROOT, needed)):
        sys.exit(needed + " is missing: open the project in Unity once (Assets -> Open C# Project) to generate it.")

GAME, EDITOR = "_check_game.csproj", "_check_editor.csproj"
try:
    write_project("Assembly-CSharp.csproj", GAME, scripts["game"])
    write_project("Assembly-CSharp-Editor.csproj", EDITOR, scripts["editor"],
                  ('Include="Assembly-CSharp.csproj"', 'Include="%s"' % GAME))
    result = subprocess.run(["dotnet", "build", EDITOR, "-nologo", "-v", "q", "-p:FrameworkPathOverride=" + FRAMEWORK],
                            cwd=ROOT, capture_output=True, text=True)
finally:
    for f in (GAME, EDITOR):
        if os.path.exists(os.path.join(ROOT, f)):
            os.remove(os.path.join(ROOT, f))

show_warnings = "--warnings" in sys.argv
lines = sorted(set(l.strip() for l in (result.stdout + result.stderr).splitlines()
                   if " error " in l or (show_warnings and UNUSED_WARNINGS.search(l))))
for line in lines:
    print(line.replace(ROOT + os.sep, "").split(" [")[0])
print("COMPILES" if result.returncode == 0 else "DOES NOT COMPILE")
sys.exit(0 if result.returncode == 0 else 1)
