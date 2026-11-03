"""Switches the game from Valve's test app (480) to its own Steam App ID, in the three places that name it.

Usage:  python tools/set-app-id.py APP_ID [WINDOWS_DEPOT [MAC_DEPOT [LINUX_DEPOT]]]
                                  the depots default to APP_ID + 1, + 2 and + 3 (check Steamworks → SteamPipe → Depots)
        python tools/set-app-id.py --check                        prints what each place holds now

Changes:
- Assets/Scripts/Management/SteamStartup.cs: APP_ID = <id> (TEST_APP_ID stays 480: the tests and the restart rule use it)
- steam_appid.txt: <id>
- docs/steam/steampipe/app_build.vdf: "AppID" and the three depot IDs, in the file's order (Windows, macOS, Linux)

The Steam API goes on main (the branch policy): ask before committing. Line endings are kept as they are.
"""
import os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STARTUP = os.path.join(ROOT, "Assets", "Scripts", "Management", "SteamStartup.cs")
APPID_TXT = os.path.join(ROOT, "steam_appid.txt")
VDF = os.path.join(ROOT, "docs", "steam", "steampipe", "app_build.vdf")
APP_LINE = re.compile(rb"(public const uint APP_ID = )([^;]+)(;)")
VDF_APP = re.compile(rb'("AppID" ")([^"]+)(")')
VDF_DEPOT = re.compile(rb'(\n\t\t")([^"]+)("\r?\n\t\t\{\r?\n\t\t\t"FileMapping")')

def read(path):
    with open(path, "rb") as fh:
        return fh.read()

def write(path, data):
    with open(path, "wb") as fh:
        fh.write(data)

def current():
    app = APP_LINE.search(read(STARTUP)).group(2).decode()
    txt = read(APPID_TXT).decode().strip()
    vdf = read(VDF)
    return app, txt, VDF_APP.search(vdf).group(2).decode(), [m.group(2).decode() for m in VDF_DEPOT.finditer(vdf)]

def main(args):
    if args == ["--check"]:
        app, txt, vdf_app, depot = current()
        print("SteamStartup.APP_ID: %s\nsteam_appid.txt:     %s\napp_build.vdf AppID: %s\napp_build.vdf depots: %s" % (app, txt, vdf_app, ", ".join(depot)))
        return 0
    if not 1 <= len(args) <= 4 or not all(a.isdigit() for a in args):
        print(__doc__)
        return 1
    app_id = int(args[0])
    depots = [int(args[i]) if len(args) > i else app_id + i for i in (1, 2, 3)]
    if app_id == 480:
        print("480 is Valve's test app; give the game's own App ID.")
        return 1

    startup = read(STARTUP)
    write(STARTUP, APP_LINE.sub(lambda m: m.group(1) + str(app_id).encode() + m.group(3), startup, count=1))
    old_txt = read(APPID_TXT)
    newline = b"\r\n" if old_txt.endswith(b"\r\n") else (b"\n" if old_txt.endswith(b"\n") else b"")
    write(APPID_TXT, str(app_id).encode() + newline)
    vdf = read(VDF)
    vdf = VDF_APP.sub(lambda m: m.group(1) + str(app_id).encode() + m.group(3), vdf, count=1)
    ids = iter(depots)
    vdf = VDF_DEPOT.sub(lambda m: m.group(1) + str(next(ids)).encode() + m.group(3), vdf, count=3)
    write(VDF, vdf)

    app, txt, vdf_app, depot = current()
    ok = app == txt == vdf_app == str(app_id) and depot == [str(d) for d in depots]
    print("App ID %s, depots Windows %s, macOS %s, Linux %s: %s" % (app_id, depots[0], depots[1], depots[2], "done" if ok else "CHECK BY HAND"))
    return 0 if ok else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
