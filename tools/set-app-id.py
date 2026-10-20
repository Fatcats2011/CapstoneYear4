"""Switches the game from Valve's test app (480) to its own Steam App ID, in the three places that name it.

Usage:  python tools/set-app-id.py APP_ID [WINDOWS_DEPOT_ID]     the depot defaults to APP_ID + 1
        python tools/set-app-id.py --check                        prints what each place holds now

Changes:
- Assets/Scripts/Management/SteamStartup.cs: APP_ID = <id> (TEST_APP_ID stays 480: the tests and the restart rule use it)
- steam_appid.txt: <id>
- docs/steam/steampipe/app_build.vdf: "AppID" and the depot ID

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
    return app, txt, VDF_APP.search(vdf).group(2).decode(), VDF_DEPOT.search(vdf).group(2).decode()

def main(args):
    if args == ["--check"]:
        app, txt, vdf_app, depot = current()
        print("SteamStartup.APP_ID: %s\nsteam_appid.txt:     %s\napp_build.vdf AppID: %s\napp_build.vdf depot: %s" % (app, txt, vdf_app, depot))
        return 0
    if not 1 <= len(args) <= 2 or not all(a.isdigit() for a in args):
        print(__doc__)
        return 1
    app_id = int(args[0])
    depot_id = int(args[1]) if len(args) == 2 else app_id + 1
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
    vdf = VDF_DEPOT.sub(lambda m: m.group(1) + str(depot_id).encode() + m.group(3), vdf, count=1)
    write(VDF, vdf)

    app, txt, vdf_app, depot = current()
    ok = app == txt == vdf_app == str(app_id) and depot == str(depot_id)
    print("App ID %s, Windows depot %s: %s" % (app_id, depot_id, "done" if ok else "CHECK BY HAND"))
    return 0 if ok else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
