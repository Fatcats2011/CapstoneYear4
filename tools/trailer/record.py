"""Records trailer takes with the private build (tools/trailer/build.py), one game launch per take, then puts each take's
sound into its MP4. Nothing else touches the picture or sound. Usage: python tools/trailer/record.py [take names...]"""
import json
import os
import shutil
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, '..', '..'))
GAME = os.path.abspath(os.path.join(PROJECT, '..', '_doa_capture', 'CapstoneYear4', 'TrailerBuild', 'DeadOnArrival.exe'))
OUT = os.path.abspath(os.path.join(PROJECT, '..', 'TrailerFootage'))

with open(os.path.join(HERE, 'takes.json')) as f:
    TAKES = json.load(f)


def record(name, options):
    video = os.path.join(OUT, name + '.video.mp4')
    final = os.path.join(OUT, name + '.mp4')
    args = [GAME, '-trailer', '-multiinstance', '-out', OUT, '-name', name, '-ffmpeg', shutil.which('ffmpeg'),
            '-logFile', os.path.join(OUT, name + '.log')]
    for key, value in options.items():
        args += ['-' + key, str(value)]
    started = time.time()
    limit = 60 * 45
    game = subprocess.Popen(args)
    try:
        code = game.wait(timeout=limit)
    except subprocess.TimeoutExpired:
        game.kill()
        print(name, 'TIMED OUT after', limit, 's', flush=True)
        return False
    print(name, 'game exit', code, 'took', int(time.time() - started), 's', flush=True)
    if not os.path.exists(video) or os.path.getsize(video) == 0:
        print(name, 'NO VIDEO', flush=True)
        return False

    sound = os.path.join(OUT, name + '.f32le')
    info = os.path.join(OUT, name + '.audio.txt')
    if os.path.exists(sound) and os.path.getsize(sound) > 0 and os.path.exists(info):
        with open(info) as f:
            rate, channels = f.read().split()
        subprocess.check_call(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', '-i', video, '-f', 'f32le', '-ar', rate,
                               '-ac', channels, '-i', sound, '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac',
                               '-b:a', '320k', '-movflags', '+faststart', final])
        os.remove(video)
        os.remove(sound)
        os.remove(info)
    else:
        os.replace(video, final)
        print(name, 'NO SOUND', flush=True)
    print(name, 'done', final, flush=True)
    return True


def main():
    os.makedirs(OUT, exist_ok=True)
    names = sys.argv[1:] or list(TAKES)
    failed = [name for name in names if not record(name, TAKES[name])]
    print('FINISHED', 'failed:', failed, flush=True)
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
