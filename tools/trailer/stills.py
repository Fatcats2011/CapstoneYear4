"""Takes store screenshots with the private build (tools/trailer/build.py): bots play each take in stills.json and the
game saves PNGs instead of a video (TrailerStills.cs). Each take gets a folder in ../SteamScreenshots/raw with view/ (what
the players see) and photo/ (the free camera). Usage: python tools/trailer/stills.py [take names...]"""
import glob
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, '..', '..'))
GAME = os.path.abspath(os.path.join(PROJECT, '..', '_doa_capture', 'CapstoneYear4', 'TrailerBuild', 'DeadOnArrival.exe'))
OUT = os.path.abspath(os.path.join(PROJECT, '..', 'SteamScreenshots', 'raw'))

with open(os.path.join(HERE, 'stills.json')) as f:
    TAKES = json.load(f)


def shoot(name, options):
    args = [GAME, '-trailer', '-multiinstance', '-stills', '1', '-out', OUT, '-name', name,
            '-logFile', os.path.join(OUT, name + '.log')]
    for key, value in options.items():
        args += ['-' + key, str(value)]
    started = time.time()
    game = subprocess.Popen(args)
    try:
        code = game.wait(timeout=60 * 30)
    except subprocess.TimeoutExpired:
        game.kill()
        print(name, 'TIMED OUT', flush=True)
        return False
    count = len(glob.glob(os.path.join(OUT, name, '*', '*.png')))
    print(name, 'game exit', code, 'took', int(time.time() - started), 's,', count, 'stills', flush=True)
    return count > 0


def main():
    os.makedirs(OUT, exist_ok=True)
    names = sys.argv[1:] or list(TAKES)
    failed = [name for name in names if not shoot(name, TAKES[name])]
    print('FINISHED', 'failed:', failed, flush=True)
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
