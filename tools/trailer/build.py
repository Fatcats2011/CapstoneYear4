"""Makes the private trailer build: snapshots the working tree into a folder beside the project (Library copied from the
test mirror the first time, to skip a full import), adds the capture scripts, turns Steam off there, and builds it. The game's
project is only read. Usage: python tools/trailer/build.py [--no-sync]"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, '..', '..'))
COPY = os.path.abspath(os.path.join(PROJECT, '..', '_doa_capture', 'CapstoneYear4'))
MIRROR = os.path.abspath(os.path.join(PROJECT, '..', '_doa_test_mirror', 'CapstoneYear4'))
UNITY = r'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'


def robocopy(source, dest, *extra):
    code = subprocess.call(['robocopy', source, dest, '/MIR', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1', *extra])
    if code >= 8:
        sys.exit('robocopy failed: %s (%d)' % (source, code))


def replace(path, old, new):
    with open(path, encoding='utf-8', newline='') as f:
        text = f.read()
    if old not in text:
        sys.exit('not found in %s: %s' % (path, old))
    with open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(text.replace(old, new))


def find(name):
    for root, _, files in os.walk(os.path.join(COPY, 'Assets')):
        if name in files:
            return os.path.join(root, name)
    sys.exit('no ' + name)


def main():
    if '--no-sync' not in sys.argv:
        os.makedirs(COPY, exist_ok=True)
        for part in ('Assets', 'Packages', 'ProjectSettings'):
            robocopy(os.path.join(PROJECT, part), os.path.join(COPY, part))
        if not os.path.exists(os.path.join(COPY, 'Library')):
            robocopy(os.path.join(MIRROR, 'Library'), os.path.join(COPY, 'Library'), '/XD', 'Temp', 'Bee')

    # Bots must not earn achievements or show as playing on the real Steam account
    steam = find('SteamManager.cs')
    with open(steam, encoding='utf-8', newline='') as f:
        patched = 'apiInitialized = false; // trailer capture' in f.read()
    if not patched:
        replace(steam, 'if (SteamStartup.ShouldRestartThroughSteam(SteamStartup.APP_ID, Application.isEditor)',
                'if (false && SteamStartup.ShouldRestartThroughSteam(SteamStartup.APP_ID, Application.isEditor)')
        replace(steam, 'apiInitialized = SteamAPI.Init();', 'apiInitialized = false; // trailer capture')

    # The filmed seats are bots standing in for people, so when they're out they spectate as a person would
    visuals = find('ModeVisuals.cs')
    with open(visuals, encoding='utf-8', newline='') as f:
        if '&& ball.GetComponent<BotDriver>() == null' in f.read():
            replace(visuals, ' && ball.GetComponent<BotDriver>() == null',
                    ' && !PlayerInstantiate.Instance.Roster[seat].IsBot')

    trailer = os.path.join(COPY, 'Assets', 'Scripts', 'Trailer')
    editor = os.path.join(COPY, 'Assets', 'Scripts', 'Editor')
    os.makedirs(trailer, exist_ok=True)
    shutil.copy(os.path.join(HERE, 'TrailerDirector.cs'), trailer)
    shutil.copy(os.path.join(HERE, 'TrailerBuild.cs'), editor)

    log = os.path.join(COPY, 'build.log')
    code = subprocess.call([UNITY, '-batchmode', '-quit', '-projectPath', COPY, '-buildTarget', 'Win64',
                            '-executeMethod', 'TrailerBuild.Windows', '-logFile', log])
    print('build exit', code, 'log', log)
    sys.exit(code)


if __name__ == '__main__':
    main()
