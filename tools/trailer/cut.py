"""Cuts each recorded take's best moments into their own MP4s, using its .events.csv. Stream copy: the picture and sound aren't
re-encoded, graded, or changed in any way (cuts land on the takes' half-second keyframes). Writes clips/<take>/ and INDEX.md
beside the takes. Usage: python tools/trailer/cut.py"""
import csv
import glob
import os
import subprocess

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, '..', '..', '..', 'TrailerFootage'))

BEFORE, AFTER = 4.0, 4.0
MAX_CLIPS = 12

# How much a moment is worth filming, and what to call it
WEIGHT = {
    'LastStanding': (12, 'last_standing'), 'LastCaught': (12, 'last_caught'), 'PotatoExploded': (10, 'potato_explodes'),
    'golden_steal': (10, 'golden_steal'), 'mother_lode': (9, 'mother_lode_appears'), 'CrownTaken': (8, 'crown_taken'),
    'Infected': (8, 'infected'), 'steal': (7, 'steal'), 'PotatoPassed': (6, 'potato_passed'), 'clash': (5, 'clash'),
    'fell_in_water': (4, 'water_fall'), 'bump': (3, 'bump'), 'delivery': (2, 'delivery'),
}
# Shown whatever else happened: the start and end of each stage of a match
STATES = {'GoldenCutscene': (9, 'golden_cutscene', 0, 10), 'FinalPackage': (9, 'golden_round', 0, 15), 'Results': (9, 'results_podium', 0, 16)}


def duration(path):
    return float(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', path]))


def moments(path):
    found = []
    with open(path, newline='') as f:
        for row in csv.DictReader(f):
            at = float(row['seconds'])
            if row['event'] == 'state':
                if row['detail'] in STATES:
                    weight, name, before, after = STATES[row['detail']]
                    found.append(dict(at=at, weight=weight, name=name, before=before, after=after))
            elif row['event'] in WEIGHT:
                weight, name = WEIGHT[row['event']]
                found.append(dict(at=at, weight=weight, name=name, before=BEFORE, after=AFTER))
    return found


def choose(found):
    # Best first, no two overlapping, then a few of each kind so one kind doesn't fill the take
    picked, per_kind = [], {}
    for moment in sorted(found, key=lambda m: -m['weight']):
        start, end = moment['at'] - moment['before'], moment['at'] + moment['after']
        if per_kind.get(moment['name'], 0) >= 3 or any(start < p['end'] and p['start'] < end for p in picked):
            continue
        picked.append(dict(moment, start=max(0.0, start), end=end))
        per_kind[moment['name']] = per_kind.get(moment['name'], 0) + 1
        if len(picked) >= MAX_CLIPS:
            break
    return sorted(picked, key=lambda p: p['start'])


def stamp(seconds):
    return '%02d:%02d' % (seconds // 60, seconds % 60)


def main():
    lines = ['# Trailer footage', '',
             'Raw game captures: 60 fps, H.264 + AAC, HUD and game sound as played, no grading or music. Bots drive every seat. Full screen takes are 2560x1440; split screen takes are 1920x1080.',
             'Takes are whole matches; `clips/` holds moments cut from them without re-encoding.', '']
    for video in sorted(glob.glob(os.path.join(OUT, '*.mp4'))):
        take = os.path.splitext(os.path.basename(video))[0]
        events = os.path.join(OUT, take + '.events.csv')
        length = duration(video)
        lines += ['## %s (%s, %.1f GB)' % (take, stamp(length), os.path.getsize(video) / 1e9), '']
        if not os.path.exists(events):
            continue
        folder = os.path.join(OUT, 'clips', take)
        os.makedirs(folder, exist_ok=True)
        for clip in choose(moments(events)):
            end = min(clip['end'], length)
            if end - clip['start'] < 2:
                continue
            name = '%s_%s.mp4' % (stamp(clip['at']).replace(':', 'm'), clip['name'])
            subprocess.check_call(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', '-ss', '%.2f' % clip['start'], '-i', video,
                                   '-t', '%.2f' % (end - clip['start']), '-c', 'copy', '-avoid_negative_ts', 'make_zero',
                                   '-movflags', '+faststart', os.path.join(folder, name)])
            lines.append('- %s: %s (`clips/%s/%s`)' % (stamp(clip['at']), clip['name'].replace('_', ' '), take, name))
        lines.append('')
    with open(os.path.join(OUT, 'INDEX.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines))
    print('wrote', os.path.join(OUT, 'INDEX.md'))


if __name__ == '__main__':
    main()
