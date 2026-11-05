# Trailer capture

Records bots playing the game to MP4, for the trailer. None of this goes in the game: it builds a private copy.

- `python tools/trailer/build.py`: snapshots the project to `../_doa_capture`, adds the capture scripts, and builds a Windows player. In that copy only: Steam is off (bots can't earn achievements on your account), and bot-driven seats spectate like people when they're out.
- `python tools/trailer/record.py [take names]`: plays each take in `takes.json`, one game launch per take, into `../TrailerFootage`. A game window opens on the desktop. Don't press Space or Enter in it, or a keyboard player joins. Nothing plays through the speakers.
- `python tools/trailer/cut.py`: cuts each take's best moments into `clips/` using its `.events.csv`, without re-encoding, and writes `INDEX.md`.

Takes are set in `takes.json`: `seats` (1 full screen with CPU rivals; 2 or 4 split screen), `mode`, `speed`, `lowgravity`, `waves`, `length`, `round`, `golden`, `hud`, `menus`, `seconds`, `width`, `height`.

Each take's log ends with `TRAILER wrote N frames, R repeated`: R is how many frames the game drew too slowly for 60 fps. Record with nothing else heavy running, or R climbs. Needs `ffmpeg` on the PATH and an NVIDIA GPU (h264_nvenc).
