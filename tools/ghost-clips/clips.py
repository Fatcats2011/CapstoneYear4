"""
The ghost's hand clips, one table each. See ghost_clips.py for the units:
    pose = {bone: ((fwd, left, up) in U, (roll, pitch, yaw) in degrees)}   for Head, Hand.Left, Hand.Right
           {finger: (x, y, z) local bend in degrees}                         for Finger.Left, Finger.Right
    keys = [(share 0..1, pose), ...]  every clip starts and ends at REST (the Animator blends it in and out)
Rules (spec: docs/superpowers/specs/2026-11-19-ghost-animations-design.md): from behind the body hides the hands, so a
hand only reads above the horns (up >= ~2.2 U) or out past the sides (|left| >= ~1.3 U).
The lobby's close camera leaves only ~0.5 U over the horns, so the lobby moves (12-14, 21, 23, 24) keep the hands at
up <= LOBBY_UP and spread them out past the sides instead.
"""

REST = {}
LOBBY_UP = 2.2   # the highest a lobby move's hand goes: the horn tips (hands rest 0.44 U over the Torso, horns 2.62)

# Handy poses, in U from each hand's rest spot (rest: low at the front, at the body's sides)
OVERHEAD_R = ((-1.0, -0.5, 2.4), (0, -70, 0))     # right hand straight up above the head
OVERHEAD_L = ((-1.0, 0.5, 2.4), (0, -70, 0))
HIGH_R = ((-0.8, -0.3, 2.0), (0, -50, 0))         # a bit lower than overhead
HIGH_L = ((-0.8, 0.3, 2.0), (0, -50, 0))
WIDE_R = ((-1.0, -0.9, 1.4), (0, -30, -40))       # out and up at the side, past the body
WIDE_L = ((-1.0, 0.9, 1.4), (0, -30, 40))
FIST = (0, 0, -80)                                # finger curled into a fist (local bend)


def pose(**bones):
    """pose(Hand_Right=..., Finger_Right=...) -> {'Hand.Right': ...}"""
    return {name.replace("_", "."): value for name, value in bones.items()}


def wobble(base, d_pos=(0, 0, 0), d_rot=(0, 0, 0), k=1.0):
    (p, r) = base
    return ((p[0] + d_pos[0] * k, p[1] + d_pos[1] * k, p[2] + d_pos[2] * k),
            (r[0] + d_rot[0] * k, r[1] + d_rot[1] * k, r[2] + d_rot[2] * k))


CLIPS = [
    # Not an emote: the rest pose, so a Unity test can check the bones came through the FBX round trip unchanged
    {"id": "0", "name": "Rest Check", "length": 1.5, "keys": [(0.0, REST), (1.0, REST)]},
    # 2a Fist Shake: one fist pumps up and down above the head
    {"id": "2a", "name": "Fist Pump", "length": 1.7, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((-1.0, -0.5, 2.6), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.3, pose(Hand_Right=((-1.0, -0.5, 3.4), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.42, pose(Hand_Right=((-1.0, -0.5, 2.6), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.54, pose(Hand_Right=((-1.0, -0.5, 3.4), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.66, pose(Hand_Right=((-1.0, -0.5, 2.6), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.78, pose(Hand_Right=((-1.0, -0.5, 3.4), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Finger_Right=FIST)),
        (0.88, pose(Hand_Right=((-1.0, -0.5, 2.6), (0, -70, 0)), Hand_Left=((-0.8, 0.5, 1.2), (0, -30, 0)), Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 11a Big Wave: both arms wave overhead
    {"id": "11a", "name": "Overhead Wave", "length": 2.2, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.5, 0), (25, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, 0.5, 0), (25, 0, 0)))),
        (0.35, pose(Hand_Left=wobble(OVERHEAD_L, (0, -0.5, 0), (-25, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.5, 0), (-25, 0, 0)))),
        (0.55, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.5, 0), (25, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, 0.5, 0), (25, 0, 0)))),
        (0.75, pose(Hand_Left=wobble(OVERHEAD_L, (0, -0.5, 0), (-25, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.5, 0), (-25, 0, 0)))),
        (1.0, REST),
    ]},
    # 14a Warm Up: big stretch, arms up to the horn tips (LOBBY_UP) and out, then wide in a Y past the sides, then out
    {"id": "14a", "name": "Big Stretch", "length": 2.5, "keys": [
        (0.0, REST),
        (0.3, pose(Hand_Left=((-1.0, 1.2, 2.2), (0, -60, 20)), Hand_Right=((-1.0, -1.2, 2.2), (0, -60, -20)), Head=((0, 0, 0), (0, -20, 0)))),
        (0.55, pose(Hand_Left=((-1.0, 1.9, 2.0), (0, -50, 40)), Hand_Right=((-1.0, -1.9, 2.0), (0, -50, -40)), Head=((0, 0, 0), (0, -25, 0)))),
        (0.8, pose(Hand_Left=WIDE_L, Hand_Right=WIDE_R)),
        (1.0, REST),
    ]},
    # ---- Batch A ----
    # 1b Cackle "Parcel Shake": both hands together overhead shaking a box side to side
    {"id": "1b", "name": "Parcel Shake", "length": 2.1, "keys": [
        (0.0, REST),
        (0.12, pose(Hand_Left=((-0.8, -0.80, 2.4), (0, -60, 0)), Hand_Right=((-0.8, 0.80, 2.4), (0, -60, 0)))),
        (0.27, pose(Hand_Left=((-0.8, 0.70, 3.0), (25, -60, 0)), Hand_Right=((-0.8, 2.30, 3.0), (25, -60, 0)))),
        (0.42, pose(Hand_Left=((-0.8, -2.10, 3.0), (-25, -60, 0)), Hand_Right=((-0.8, -0.70, 3.0), (-25, -60, 0)))),
        (0.57, pose(Hand_Left=((-0.8, 0.70, 3.0), (25, -60, 0)), Hand_Right=((-0.8, 2.30, 3.0), (25, -60, 0)))),
        (0.72, pose(Hand_Left=((-0.8, -2.10, 3.0), (-25, -60, 0)), Hand_Right=((-0.8, -0.70, 3.0), (-25, -60, 0)))),
        (0.86, pose(Hand_Left=((-0.8, -0.80, 2.4), (0, -60, 0)), Hand_Right=((-0.8, 0.80, 2.4), (0, -60, 0)))),
        (1.0, REST),
    ]},
    # 1d Cackle "Parcel Spin": right hand high, finger up, left hand flicking beside it
    {"id": "1d", "name": "Parcel Spin", "length": 2.2, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=OVERHEAD_R, Finger_Right=(0, 0, 10), Hand_Left=((-0.6, 1.0, 1.8), (0, -40, 0)))),
        (0.3, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.3), (60, 0, 0)), Hand_Left=((-0.6, 1.4, 2.4), (-30, -40, 30)))),
        (0.45, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.0), (-60, 0, 0)), Hand_Left=((-0.6, 0.9, 1.8), (30, -40, -10)))),
        (0.6, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.3), (60, 0, 0)), Hand_Left=((-0.6, 1.4, 2.4), (-30, -40, 30)))),
        (0.75, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.0), (-60, 0, 0)), Hand_Left=((-0.6, 0.9, 1.8), (30, -40, -10)))),
        (0.88, pose(Hand_Right=OVERHEAD_R, Hand_Left=((-0.6, 1.0, 1.8), (0, -40, 0)))),
        (1.0, REST),
    ]},
    # 2b FistShake "Arm Flail": both hands flail up, alternating
    {"id": "2b", "name": "Arm Flail", "length": 1.7, "keys": [
        (0.0, REST),
        (0.1, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.3, 0.4)), Hand_Right=((-0.8, -0.5, 1.4), (0, -30, 0)))),
        (0.22, pose(Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.3, 0.4)))),
        (0.34, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.3, 0.4)), Hand_Right=((-0.8, -0.5, 1.4), (0, -30, 0)))),
        (0.46, pose(Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.3, 0.4)))),
        (0.58, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.3, 0.4)), Hand_Right=((-0.8, -0.5, 1.4), (0, -30, 0)))),
        (0.7, pose(Hand_Left=((-0.8, 0.5, 1.4), (0, -30, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.3, 0.4)))),
        (0.82, pose(Hand_Left=OVERHEAD_L, Hand_Right=OVERHEAD_R)),
        (1.0, REST),
    ]},
    # 2c FistShake "Hat Throw": right hand to the hat, then slammed down out past the side
    {"id": "2c", "name": "Hat Throw", "length": 1.7, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Right=((-1.25, 0.85, 1.6), (0, -50, 0)), Finger_Right=FIST)),
        (0.35, pose(Hand_Right=((-1.25, 0.85, 2.3), (0, -60, 0)), Finger_Right=FIST)),
        (0.5, pose(Hand_Right=((-0.3, -0.9, 0.6), (0, -10, -50)), Finger_Right=FIST)),
        (0.58, pose(Hand_Right=((-0.3, -1.0, 0.3), (0, -10, -50)), Finger_Right=(0, 0, 0))),
        (0.8, pose(Hand_Right=((-0.3, -0.9, 0.4), (0, -10, -50)))),
        (1.0, REST),
    ]},
    # 2d FistShake "Lunge Grab": both hands thrust forward and out past the sides at shoulder height, grab, sag (body: lunge forward, tail up)
    {"id": "2d", "name": "Lunge Grab", "length": 1.8, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Left=((1.0, 0.9, 1.6), (0, -10, 20)), Hand_Right=((1.0, -0.9, 1.6), (0, -10, -20)))),
        (0.32, pose(Hand_Left=((1.4, 1.5, 1.9), (0, -10, 30)), Hand_Right=((1.4, -1.5, 1.9), (0, -10, -30)))),
        (0.45, pose(Hand_Left=((1.4, 1.5, 1.9), (0, -10, 30)), Hand_Right=((1.4, -1.5, 1.9), (0, -10, -30)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.55, pose(Hand_Left=((1.4, 1.2, 1.8), (0, -10, 30)), Hand_Right=((1.4, -1.2, 1.8), (0, -10, -30)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.65, pose(Hand_Left=((1.4, 1.5, 1.9), (0, -10, 30)), Hand_Right=((1.4, -1.5, 1.9), (0, -10, -30)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.8, pose(Hand_Left=((0.4, 1.0, -0.2), (0, 20, 0)), Hand_Right=((0.4, -1.0, -0.2), (0, 20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 3a DeliveryFlourish "Sweeping Bow": right hand arcs wide and sweeps out past the right side, left on belly
    {"id": "3a", "name": "Sweeping Bow", "length": 2.2, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Right=((-0.5, -0.8, 2.2), (0, -50, -30)), Hand_Left=((-0.7, -0.8, 0.4), (0, 0, 0)))),
        (0.4, pose(Hand_Right=((-0.5, -1.2, 1.8), (0, -30, -60)), Hand_Left=((-0.7, -0.8, 0.4), (0, 0, 0)))),
        (0.6, pose(Hand_Right=((0.2, -1.3, 0.3), (0, 0, -70)), Hand_Left=((-0.7, -0.8, 0.4), (0, 0, 0)))),
        (0.8, pose(Hand_Right=((0.2, -1.3, 0.3), (0, 0, -70)), Hand_Left=((-0.7, -0.8, 0.4), (0, 0, 0)))),
        (1.0, REST),
    ]},
    # 3b DeliveryFlourish "High Fist": one quick fist pump up, held
    {"id": "3b", "name": "High Fist", "length": 1.7, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((-0.5, -0.3, 1.2), (0, -40, 0)), Finger_Right=FIST)),
        (0.3, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.7)), Finger_Right=FIST)),
        (0.4, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.2)), Finger_Right=FIST)),
        (0.8, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.4)), Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 3c DeliveryFlourish "Hat Lift": hand to the hat, up in a big arc, back
    {"id": "3c", "name": "Hat Lift", "length": 2.1, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Right=((-1.25, 0.85, 1.5), (0, -50, 0)))),
        (0.4, pose(Hand_Right=((-1.25, 0.2, 2.9), (0, -60, -30)))),
        (0.6, pose(Hand_Right=((-1.25, -0.6, 3.0), (0, -50, -50)))),
        (0.8, pose(Hand_Right=((-1.25, 0.85, 1.5), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 3d DeliveryFlourish "Brush Off": two claps overhead, then dust off wide
    {"id": "3d", "name": "Brush Off", "length": 2.1, "keys": [
        (0.0, REST),
        (0.1, pose(Hand_Left=((-0.8, -0.3, 2.4), (0, -60, 0)), Hand_Right=((-0.8, 0.3, 2.4), (0, -60, 0)))),
        (0.2, pose(Hand_Left=((-0.8, -0.9, 2.5), (0, -60, 0)), Hand_Right=((-0.8, 0.9, 2.5), (0, -60, 0)))),
        (0.3, pose(Hand_Left=((-0.8, -0.3, 2.4), (0, -60, 0)), Hand_Right=((-0.8, 0.3, 2.4), (0, -60, 0)))),
        (0.4, pose(Hand_Left=((-0.8, -0.9, 2.5), (0, -60, 0)), Hand_Right=((-0.8, 0.9, 2.5), (0, -60, 0)))),
        (0.55, pose(Hand_Left=((-0.4, 0.8, 1.0), (30, 0, 40)), Hand_Right=((-0.4, -0.8, 1.0), (-30, 0, -40)))),
        (0.7, pose(Hand_Left=((-0.4, 0.8, 0.5), (-30, 0, 40)), Hand_Right=((-0.4, -0.8, 0.5), (30, 0, -40)))),
        (0.85, pose(Hand_Left=((-0.4, 0.8, 1.0), (30, 0, 40)), Hand_Right=((-0.4, -0.8, 1.0), (-30, 0, -40)))),
        (1.0, REST),
    ]},
    # 4a HoldYourHat "Hat Clamp": right hand clamped on head, left out back
    {"id": "4a", "name": "Hat Clamp", "length": 1.7, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((-1.25, 0.85, 2.0), (0, -50, 0)), Hand_Left=((-1.0, 1.0, 0.6), (0, 0, 20)))),
        (0.3, pose(Hand_Right=((-1.25, 0.85, 1.9), (0, -50, 0)), Hand_Left=((-1.2, 1.4, 0.8), (0, 0, 30)))),
        (0.5, pose(Hand_Right=((-1.25, 0.85, 2.0), (0, -50, 0)), Hand_Left=((-1.0, 1.1, 0.5), (0, 0, 20)))),
        (0.7, pose(Hand_Right=((-1.25, 0.85, 1.9), (0, -50, 0)), Hand_Left=((-1.2, 1.4, 0.8), (0, 0, 30)))),
        (0.85, pose(Hand_Right=((-1.25, 0.85, 2.0), (0, -50, 0)), Hand_Left=((-1.0, 1.0, 0.6), (0, 0, 20)))),
        (1.0, REST),
    ]},
    # 4b HoldYourHat "Plane Arms": arms out, banking
    {"id": "4b", "name": "Plane Arms", "length": 2.1, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.8, 1.0, 1.0), (0, 0, 0)), Hand_Right=((-0.8, -1.0, 1.0), (0, 0, 0)))),
        (0.3, pose(Hand_Left=((-0.8, 1.0, 1.8), (0, 0, 0)), Hand_Right=((-0.8, -1.0, 0.2), (0, 0, 0)))),
        (0.5, pose(Hand_Left=((-0.8, 1.0, 0.2), (0, 0, 0)), Hand_Right=((-0.8, -1.0, 1.8), (0, 0, 0)))),
        (0.7, pose(Hand_Left=((-0.8, 1.0, 1.8), (0, 0, 0)), Hand_Right=((-0.8, -1.0, 0.2), (0, 0, 0)))),
        (0.85, pose(Hand_Left=((-0.8, 1.0, 1.0), (0, 0, 0)), Hand_Right=((-0.8, -1.0, 1.0), (0, 0, 0)))),
        (1.0, REST),
    ]},
    # 4c HoldYourHat "Superhero": right fist forward and up, out past the right side, left back along the side
    {"id": "4c", "name": "Superhero", "length": 2.0, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((0.5, -0.4, 1.6), (0, -40, 0)), Hand_Left=((-1.2, 0.2, 0.4), (0, 0, 0)), Finger_Right=FIST)),
        (0.3, pose(Hand_Right=((1.2, -1.3, 2.4), (0, -70, 0)), Hand_Left=((-1.6, 0.4, 0.8), (0, 0, 30)), Finger_Right=FIST)),
        (0.5, pose(Hand_Right=((1.3, -1.3, 2.5), (0, -70, 0)), Hand_Left=((-1.6, 0.5, 0.9), (0, 0, 30)), Finger_Right=FIST)),
        (0.8, pose(Hand_Right=((1.2, -1.3, 2.4), (0, -70, 0)), Hand_Left=((-1.6, 0.4, 0.8), (0, 0, 30)), Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 5a Wheee "Arms Up": both straight up, alternating bob
    {"id": "5a", "name": "Arms Up", "length": 2.1, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0, 0.7)), Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.7)))),
        (0.3, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.5, 1.1), (20, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, 0.0, 0.4), (-20, 0, 0)))),
        (0.45, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.0, 0.4), (-20, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.5, 1.1), (20, 0, 0)))),
        (0.6, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.5, 1.1), (20, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, 0.0, 0.4), (-20, 0, 0)))),
        (0.75, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0.0, 0.4), (-20, 0, 0)), Hand_Right=wobble(OVERHEAD_R, (0, -0.5, 1.1), (20, 0, 0)))),
        (0.88, pose(Hand_Left=wobble(OVERHEAD_L, (0, 0, 0.7)), Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.7)))),
        (1.0, REST),
    ]},
    # 5c Wheee "Starfish": hands splay out wide at the sides (not up: 5a is the arms-up one), then snap back
    {"id": "5c", "name": "Starfish", "length": 1.7, "keys": [
        (0.0, REST),
        (0.12, pose(Hand_Left=((-0.8, 0.9, 1.0), (0, -20, 30)), Hand_Right=((-0.8, -0.9, 1.0), (0, -20, -30)))),
        (0.3, pose(Hand_Left=((-0.8, 2.2, 1.3), (0, -10, 60)), Hand_Right=((-0.8, -2.2, 1.3), (0, -10, -60)))),
        (0.42, pose(Hand_Left=((-0.8, 2.0, 1.2), (0, -10, 60)), Hand_Right=((-0.8, -2.0, 1.2), (0, -10, -60)))),
        (0.7, pose(Hand_Left=((-0.8, 2.2, 1.3), (0, -10, 60)), Hand_Right=((-0.8, -2.2, 1.3), (0, -10, -60)))),
        (0.85, pose(Hand_Left=((-0.8, 0.9, 1.0), (0, -20, 30)), Hand_Right=((-0.8, -0.9, 1.0), (0, -20, -30)))),
        (1.0, REST),
    ]},
    # ---- Batch B ----
    # 7a TreasureHold "Parcel Overhead": both hands together high overhead holding a box, slow proud sway side to side (body: none)
    {"id": "7a", "name": "Parcel Overhead", "length": 2.2, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.8, -0.8, 2.6), (0, -60, 0)), Hand_Right=((-0.8, 0.8, 2.6), (0, -60, 0)))),
        (0.3, pose(Hand_Left=((-0.8, -0.4, 2.9), (0, -60, 0)), Hand_Right=((-0.8, 1.2, 2.9), (0, -60, 0)))),
        (0.48, pose(Hand_Left=((-0.8, -1.2, 2.9), (0, -60, 0)), Hand_Right=((-0.8, 0.4, 2.9), (0, -60, 0)))),
        (0.66, pose(Hand_Left=((-0.8, -0.4, 2.9), (0, -60, 0)), Hand_Right=((-0.8, 1.2, 2.9), (0, -60, 0)))),
        (0.8, pose(Hand_Left=((-0.8, -0.8, 2.6), (0, -60, 0)), Hand_Right=((-0.8, 0.8, 2.6), (0, -60, 0)))),
        (0.9, pose(Hand_Left=((-0.8, -0.8, 1.4), (0, -40, 0)), Hand_Right=((-0.8, 0.8, 1.4), (0, -40, 0)))),
        (1.0, REST),
    ]},
    # 7b TreasureHold "Parcel Hug": box pressed to the left cheek, poking out past the side, squeeze pulses (twist is code), pop wide at the end
    {"id": "7b", "name": "Parcel Hug", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.5, 0.2, 1.1), (0, -20, 0)), Hand_Right=((-0.5, 1.2, 1.1), (0, -20, 0)))),
        (0.25, pose(Hand_Left=((-0.7, 0.5, 1.6), (0, -20, 0)), Hand_Right=((-0.7, 1.9, 1.6), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.35, pose(Hand_Left=((-0.7, 0.6, 1.7), (0, -20, 0)), Hand_Right=((-0.7, 2.0, 1.7), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.45, pose(Hand_Left=((-0.7, 0.5, 1.6), (0, -20, 0)), Hand_Right=((-0.7, 1.9, 1.6), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.55, pose(Hand_Left=((-0.7, 0.6, 1.7), (0, -20, 0)), Hand_Right=((-0.7, 2.0, 1.7), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.63, pose(Hand_Left=((-0.7, 0.5, 1.6), (0, -20, 0)), Hand_Right=((-0.7, 1.9, 1.6), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.72, pose(Hand_Left=((-0.8, 1.3, 2.2), (0, -30, 40)), Hand_Right=((-0.8, -1.3, 2.2), (0, -30, -40)))),
        (0.84, pose(Hand_Left=((-0.8, 1.1, 1.8), (0, -30, 40)), Hand_Right=((-0.8, -1.1, 1.8), (0, -30, -40)))),
        (1.0, REST),
    ]},
    # 7d TreasureHold "Presenting": box held out to the left side, then swung over to the right (body: turn to each side)
    {"id": "7d", "name": "Presenting", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((0.3, 0.4, 0.8), (0, -10, 0)), Hand_Right=((0.3, 1.5, 0.8), (0, -10, 0)))),
        (0.3, pose(Hand_Left=((0.2, 1.5, 1.0), (0, -10, 20)), Hand_Right=((0.2, 2.8, 1.0), (0, -10, 20)))),
        (0.5, pose(Hand_Left=((0.2, 1.6, 1.0), (0, -10, 20)), Hand_Right=((0.2, 2.9, 1.0), (0, -10, 20)))),
        (0.65, pose(Hand_Left=((0.5, -0.5, 2.4), (0, -30, 0)), Hand_Right=((0.5, 0.8, 2.4), (0, -30, 0)))),
        (0.8, pose(Hand_Left=((0.2, -2.8, 1.0), (0, -10, -20)), Hand_Right=((0.2, -1.5, 1.0), (0, -10, -20)))),
        (0.92, pose(Hand_Left=((0.2, -2.9, 1.0), (0, -10, -20)), Hand_Right=((0.2, -1.6, 1.0), (0, -10, -20)))),
        (1.0, REST),
    ]},
    # 8b JumpScare "Hat Grab": hands shoot up above the head, catch the popped hat, bring it down to the head (body: pop up 0-0.2)
    {"id": "8b", "name": "Hat Grab", "length": 2.0, "keys": [
        (0.0, REST),
        (0.14, pose(Hand_Left=((-1.0, -0.6, 3.2), (0, -60, 0)), Hand_Right=((-1.0, 0.6, 3.2), (0, -60, 0)))),
        (0.25, pose(Hand_Left=((-1.0, -0.3, 3.6), (0, -60, 0)), Hand_Right=((-1.0, 0.3, 3.6), (0, -60, 0)))),
        (0.4, pose(Hand_Left=((-1.0, -0.5, 3.0), (0, -60, 0)), Hand_Right=((-1.0, 0.5, 3.0), (0, -60, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.6, pose(Hand_Left=((-1.25, -0.85, 1.9), (0, -50, 0)), Hand_Right=((-1.25, 0.85, 1.9), (0, -50, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.8, pose(Hand_Left=((-1.25, -0.85, 1.8), (0, -50, 0)), Hand_Right=((-1.25, 0.85, 1.8), (0, -50, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 9a Bored "Big Stretch Up": arms up high, hold, flop down (body: lean back 0.2-0.5)
    {"id": "9a", "name": "Big Stretch Up", "length": 2.5, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Left=((-1.0, 0.3, 2.8), (0, -70, 0)), Hand_Right=((-1.0, -0.3, 2.8), (0, -70, 0)))),
        (0.4, pose(Hand_Left=((-1.0, 0.1, 3.2), (0, -70, 0)), Hand_Right=((-1.0, -0.1, 3.2), (0, -70, 0)))),
        (0.6, pose(Hand_Left=((-1.0, 0.1, 3.2), (0, -70, 0)), Hand_Right=((-1.0, -0.1, 3.2), (0, -70, 0)))),
        (0.76, pose(Hand_Left=((-0.4, 0.5, -0.3), (0, 40, 0)), Hand_Right=((-0.4, -0.5, -0.3), (0, 40, 0)))),
        (0.85, pose(Hand_Left=((-0.3, 0.3, 0.2), (0, 20, 0)), Hand_Right=((-0.3, -0.3, 0.2), (0, 20, 0)))),
        (1.0, REST),
    ]},
    # 9b Bored "Slump Over": arms dangle limp over the right side, swaying (body: lean to the right 0.15-0.85)
    {"id": "9b", "name": "Slump Over", "length": 2.5, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Left=((-0.3, 0.5, -0.5), (20, 60, 0)), Hand_Right=((-0.3, -1.1, -0.4), (20, 60, 0)))),
        (0.4, pose(Hand_Left=((-0.4, 0.2, -0.5), (-20, 60, 0)), Hand_Right=((-0.4, -1.5, -0.4), (-20, 60, 0)))),
        (0.6, pose(Hand_Left=((-0.3, 0.5, -0.5), (20, 60, 0)), Hand_Right=((-0.3, -1.1, -0.4), (20, 60, 0)))),
        (0.8, pose(Hand_Left=((-0.4, 0.2, -0.5), (-20, 60, 0)), Hand_Right=((-0.4, -1.5, -0.4), (-20, 60, 0)))),
        (0.9, pose(Hand_Left=((-0.3, 0.5, -0.5), (20, 60, 0)), Hand_Right=((-0.3, -1.1, -0.4), (20, 60, 0)))),
        (1.0, REST),
    ]},
    # 9c Bored "Shrug": hands out wide and up, palms up, big shrug held (body: turn round 0-0.2, hold, turn back 0.85-1)
    {"id": "9c", "name": "Shrug", "length": 2.5, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Left=((0.4, 1.0, 1.0), (60, 0, 30)), Hand_Right=((0.4, -1.0, 1.0), (-60, 0, -30)))),
        (0.35, pose(Hand_Left=((0.4, 1.2, 1.9), (70, 0, 30)), Hand_Right=((0.4, -1.2, 1.9), (-70, 0, -30)))),
        (0.5, pose(Hand_Left=((0.4, 1.2, 1.5), (70, 0, 30)), Hand_Right=((0.4, -1.2, 1.5), (-70, 0, -30)))),
        (0.7, pose(Hand_Left=((0.4, 1.2, 1.9), (70, 0, 30)), Hand_Right=((0.4, -1.2, 1.9), (-70, 0, -30)))),
        (0.85, pose(Hand_Left=((0.4, 1.2, 1.9), (70, 0, 30)), Hand_Right=((0.4, -1.2, 1.9), (-70, 0, -30)))),
        (1.0, REST),
    ]},
    # 10a Taunt "Ear Waggle": hands out past the sides of the head, by the ears, fingers waggling (body: turn to camera 0.15, back 0.85)
    {"id": "10a", "name": "Ear Waggle", "length": 2.2, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-1.0, 1.3, 1.5), (0, -40, 30)), Hand_Right=((-1.0, -1.3, 1.5), (0, -40, -30)))),
        (0.3, pose(Hand_Left=((-1.0, 1.5, 1.7), (30, -40, 30)), Hand_Right=((-1.0, -1.5, 1.7), (-30, -40, -30)), Finger_Left=(0, 0, 40), Finger_Right=(0, 0, 40))),
        (0.45, pose(Hand_Left=((-1.0, 1.4, 1.6), (-30, -40, 30)), Hand_Right=((-1.0, -1.4, 1.6), (30, -40, -30)), Finger_Left=(0, 0, -40), Finger_Right=(0, 0, -40))),
        (0.6, pose(Hand_Left=((-1.0, 1.5, 1.7), (30, -40, 30)), Hand_Right=((-1.0, -1.5, 1.7), (-30, -40, -30)), Finger_Left=(0, 0, 40), Finger_Right=(0, 0, 40))),
        (0.75, pose(Hand_Left=((-1.0, 1.4, 1.6), (-30, -40, 30)), Hand_Right=((-1.0, -1.4, 1.6), (30, -40, -30)), Finger_Left=(0, 0, -40), Finger_Right=(0, 0, -40))),
        (0.88, pose(Hand_Left=((-1.0, 1.3, 1.5), (0, -40, 30)), Hand_Right=((-1.0, -1.3, 1.5), (0, -40, -30)))),
        (1.0, REST),
    ]},
    # 10c Taunt "Point Back": right hand out past the right shoulder pointing behind, two jabs (body: turns and looks that way)
    {"id": "10c", "name": "Point Back", "length": 2.1, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Right=((-2.2, -1.4, 2.4), (0, -70, 0)), Finger_Right=(0, 0, 0))),
        (0.35, pose(Hand_Right=((-2.8, -1.7, 3.0), (0, -70, 0)))),
        (0.5, pose(Hand_Right=((-2.2, -1.4, 2.4), (0, -70, 0)))),
        (0.65, pose(Hand_Right=((-2.8, -1.7, 3.0), (0, -70, 0)))),
        (0.8, pose(Hand_Right=((-2.2, -1.4, 2.4), (0, -70, 0)))),
        (1.0, REST),
    ]},
    # 10d Taunt "Come At Me": both hands out past the sides at shoulder height beckoning, fingers curling in and out
    {"id": "10d", "name": "Come At Me", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.8, 1.4, 1.6), (0, -60, 0)), Hand_Right=((-0.8, -1.4, 1.6), (0, -60, 0)), Finger_Left=(0, 0, 0), Finger_Right=(0, 0, 0))),
        (0.28, pose(Hand_Left=((-0.6, 1.2, 1.7), (0, -20, 0)), Hand_Right=((-0.6, -1.2, 1.7), (0, -20, 0)), Finger_Left=(0, 0, -80), Finger_Right=(0, 0, -80))),
        (0.41, pose(Hand_Left=((-0.8, 1.4, 1.6), (0, -70, 0)), Hand_Right=((-0.8, -1.4, 1.6), (0, -70, 0)), Finger_Left=(0, 0, 0), Finger_Right=(0, 0, 0))),
        (0.54, pose(Hand_Left=((-0.6, 1.2, 1.7), (0, -20, 0)), Hand_Right=((-0.6, -1.2, 1.7), (0, -20, 0)), Finger_Left=(0, 0, -80), Finger_Right=(0, 0, -80))),
        (0.67, pose(Hand_Left=((-0.8, 1.4, 1.6), (0, -70, 0)), Hand_Right=((-0.8, -1.4, 1.6), (0, -70, 0)), Finger_Left=(0, 0, 0), Finger_Right=(0, 0, 0))),
        (0.8, pose(Hand_Left=((-0.6, 1.2, 1.7), (0, -20, 0)), Hand_Right=((-0.6, -1.2, 1.7), (0, -20, 0)), Finger_Left=(0, 0, -80), Finger_Right=(0, 0, -80))),
        (0.9, pose(Hand_Left=((-0.8, 1.4, 1.6), (0, -60, 0)), Hand_Right=((-0.8, -1.4, 1.6), (0, -60, 0)))),
        (1.0, REST),
    ]},
    # 11b BigWave "Turn And Wave": one arm waves big overhead (body: turn round 0-0.2, hold, turn back 0.85-1)
    {"id": "11b", "name": "Turn And Wave", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=wobble(OVERHEAD_R, (0, -0.9, 0.4), (-30, 0, 0)))),
        (0.3, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0.9, 0.4), (30, 0, 0)))),
        (0.45, pose(Hand_Right=wobble(OVERHEAD_R, (0, -0.9, 0.4), (-30, 0, 0)))),
        (0.6, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0.9, 0.4), (30, 0, 0)))),
        (0.75, pose(Hand_Right=wobble(OVERHEAD_R, (0, -0.9, 0.4), (-30, 0, 0)))),
        (0.88, pose(Hand_Right=wobble(OVERHEAD_R, (0, 0, 0.2)))),
        (1.0, REST),
    ]},
    # 11c BigWave "High Five": one hand reaches high and back toward the camera, out to the side, slaps at 0.42, held (body: lean back-right and look up at it 0.15-0.7, hop on the slap)
    {"id": "11c", "name": "High Five", "length": 1.5, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Right=((-1.3, -1.6, 2.6), (0, -70, -20)))),
        (0.32, pose(Hand_Right=((-1.5, -1.9, 3.4), (0, -80, -20)))),
        (0.42, pose(Hand_Right=((-1.8, -2.1, 3.8), (0, -85, -20)))),
        (0.52, pose(Hand_Right=((-1.5, -1.9, 3.4), (0, -80, -20)))),
        (0.62, pose(Hand_Right=((-1.5, -1.9, 3.4), (0, -80, -20)))),
        (0.8, pose(Hand_Right=((-1.3, -1.6, 2.6), (0, -70, -20)))),
        (1.0, REST),
    ]},
    # 10b Taunt "Bum Wiggle" (rework): hands on the hips pumping left/right past the sides (body: wiggle hips/roll in time, 0.15-0.85)
    {"id": "10b", "name": "Bum Wiggle", "length": 2.2, "keys": [
        (0.0, REST),
        (0.12, pose(Hand_Left=((-0.6, 0.4, 0.5), (0, 0, 30)), Hand_Right=((-0.6, -0.4, 0.5), (0, 0, -30)))),
        (0.27, pose(Hand_Left=((-0.6, 1.3, 0.7), (0, 0, 30)), Hand_Right=((-0.6, 0.5, 0.7), (0, 0, 30)))),
        (0.42, pose(Hand_Left=((-0.6, -0.5, 0.7), (0, 0, -30)), Hand_Right=((-0.6, -1.3, 0.7), (0, 0, -30)))),
        (0.57, pose(Hand_Left=((-0.6, 1.3, 0.7), (0, 0, 30)), Hand_Right=((-0.6, 0.5, 0.7), (0, 0, 30)))),
        (0.72, pose(Hand_Left=((-0.6, -0.5, 0.7), (0, 0, -30)), Hand_Right=((-0.6, -1.3, 0.7), (0, 0, -30)))),
        (0.86, pose(Hand_Left=((-0.6, 0.4, 0.5), (0, 0, 30)), Hand_Right=((-0.6, -0.4, 0.5), (0, 0, -30)))),
        (1.0, REST),
    ]},
    # 22b Raspberry "Tail Point" (rework): right hand points down at the tail out past the right side, left waves overhead
    # (body: turns toward the point, looks down at the tail and leans that way 0.15-0.8)
    {"id": "22b", "name": "Tail Point", "length": 1.8, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((-1.8, -1.1, -0.2), (0, 70, 0)), Hand_Left=wobble(OVERHEAD_L, (0, 0.6, 0.2)))),
        (0.3, pose(Hand_Right=((-2.2, -1.3, -0.4), (0, 70, 0)), Hand_Left=wobble(OVERHEAD_L, (0, -0.6, 0.2)))),
        (0.45, pose(Hand_Right=((-1.8, -1.1, -0.2), (0, 70, 0)), Hand_Left=wobble(OVERHEAD_L, (0, 0.6, 0.2)))),
        (0.6, pose(Hand_Right=((-2.2, -1.3, -0.4), (0, 70, 0)), Hand_Left=wobble(OVERHEAD_L, (0, -0.6, 0.2)))),
        (0.8, pose(Hand_Right=((-1.8, -1.1, -0.2), (0, 70, 0)), Hand_Left=wobble(OVERHEAD_L, (0, 0.6, 0.2)))),
        (1.0, REST),
    ]},
]


# ---- Batch C ----
def _mirror(p):
    """Same pose on the other hand: left/right swap, roll and yaw flip"""
    (a, b, c), (r, pi, y) = p
    return ((a, -b, c), (-r, pi, -y))


def _lr(l):
    """Left-hand pose l and its mirror for the right hand"""
    return pose(Hand_Left=l, Hand_Right=_mirror(l))


def _circle(c, rad, n, phase=0.0, axes=(1, 2)):
    """n points around a circle about c, in the two chosen axes (0 fwd, 1 left, 2 up)"""
    import math as m
    pts = []
    for i in range(n):
        a = phase + 2 * m.pi * i / n
        p = [c[0], c[1], c[2]]
        p[axes[0]] += rad * m.cos(a)
        p[axes[1]] += rad * m.sin(a)
        pts.append(tuple(p))
    return pts


def _seq(start, step, poses):
    return [(round(start + i * step, 3), p) for i, p in enumerate(poses)]


def _twirl():
    """The hat twirled on the right hand out past the right side, at the horns' height (the lobby: LOBBY_UP)"""
    pts = _circle((-1.0, -1.3, 1.2), 0.3, 4)   # low, so the hat on top of the finger stays under the lobby frame
    ks = []
    for i in range(12):
        ks.append(pose(Hand_Right=(pts[i % 4], ((15, 0, -15)[i % 3], -60, 0))))
    return _seq(0.3, 0.04, ks)


def _rub(center_l, rad, n_keys, start, step, tilt):
    pts_l = _circle(center_l, rad, 4, 0.0, axes=(0, 2))
    pts_r = _circle(_mirror((center_l, (0, 0, 0)))[0], rad, 4, 3.14159, axes=(0, 2))
    ks = []
    for i in range(n_keys):
        ks.append(pose(Hand_Left=(pts_l[i % 4], (0, tilt, 0)), Hand_Right=(pts_r[i % 4], (0, tilt, 0))))
    return _seq(start, step, ks)


def _rub_wide(center_l, n_keys, start, step, tilt):
    """Rub strokes: the hands slide past each other (one forward, one back) together in the middle, and pull apart
    to about 0.75 U between strokes"""
    (f, l, u) = center_l
    ks = []
    for i in range(n_keys):
        if i % 2 == 0:
            d = 0.3 if (i // 2) % 2 == 0 else -0.3
            left = ((f + d, l, u + 0.1), (0, tilt, 0))
            right = _mirror(((f - d, l, u + 0.1), (0, tilt, 0)))
        else:
            left = ((f, l + 0.25, u - 0.1), (0, tilt, 0))
            right = _mirror(left)
        ks.append(pose(Hand_Left=left, Hand_Right=right))
    return _seq(start, step, ks)


def _drum():
    hi = ((-0.3, 1.4, 2.2), (0, -50, 20))   # out past its own side at the horns' height (LOBBY_UP)
    lo = ((0.3, 0.0, -0.1), (0, 30, 0))
    ks = []
    for i in range(11):
        left_up = (i % 2 == 0)
        l = hi if left_up else lo
        r = lo if left_up else hi
        ks.append(pose(Hand_Left=l, Hand_Right=_mirror(r)))
    return _seq(0.12, 0.075, ks)


# 22a: just out past the sides of the head at eye height (the head is about 1.05 U across each side of its middle,
# which is 0.98 U in from each hand's rest), so the hands sit at the ears whichever way the head turns
EAR = 0.5


def _waggle(n, start, step):
    ks = []
    for i in range(n):
        s = 1 if i % 2 else -1
        ks.append(pose(Hand_Left=((-1.1, EAR + 0.1 * s, 0.8), (25 * s, -40, 0)), Hand_Right=((-1.1, -EAR - 0.1 * s, 0.8), (-25 * s, -40, 0)),
                       Finger_Left=(0, 0, 40 * s), Finger_Right=(0, 0, 40 * s)))
    return _seq(start, step, ks)


def _boxing():
    guard_l = ((-0.6, 0.4, 1.9), (0, -40, 0))
    punch_l = ((0.6, 1.4, 2.2), (0, -20, 30))   # up and out past its own side (LOBBY_UP)
    ks = []
    for who, t in [("L", 0.16), ("R", 0.28), ("L", 0.4), ("R", 0.52), ("L", 0.64), ("R", 0.76)]:
        for tt, ph in ((t - 0.05, "g"), (t, "p")):
            l = punch_l if (who == "L" and ph == "p") else guard_l
            r = _mirror(punch_l) if (who == "R" and ph == "p") else _mirror(guard_l)
            ks.append((round(tt, 3), pose(Hand_Left=l, Hand_Right=r, Finger_Left=FIST, Finger_Right=FIST)))
    return ks


CLIPS += [
    # 12a: hat lifted off, twirled out past the right side at the horns' height, put back (body: the hat follows the hand)
    {"id": "12a", "name": "Hat Twirl", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, pose(Hand_Right=((-1.25, 0.85, 1.7), (0, -50, 0)))),
        (0.22, pose(Hand_Right=((-1.0, -0.6, 1.8), (0, -60, 0)))),
    ] + _twirl() + [
        (0.82, pose(Hand_Right=((-1.0, -0.6, 1.7), (0, -60, 0)))),
        (0.9, pose(Hand_Right=((-1.25, 0.85, 1.8), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 13b: arms fling out wide past the sides at the horns' height, hold, settle (body: small hop/puff chest 0.1-0.3)
    {"id": "13b", "name": "Ta Da", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, _lr(((-0.6, -0.3, 0.8), (0, 0, 0)))),
        (0.22, _lr(((-0.8, 1.9, 2.0), (0, -30, 50)))),
        (0.3, _lr(((-0.8, 1.7, 1.9), (0, -30, 50)))),
        (0.5, _lr(((-0.8, 1.8, 2.1), (0, -30, 50)))),
        (0.7, _lr(((-0.8, 1.7, 1.9), (0, -30, 50)))),
        (0.86, _lr(((-0.8, 1.0, 1.4), (0, -20, 30)))),
        (1.0, REST),
    ]},
    # 14c: alternating high punches (body: small twist to the punching side per punch, 0.1-0.75)
    {"id": "14c", "name": "Shadow Box", "length": 2.2, "keys": [(0.0, REST)] + _boxing() + [
        (0.84, _lr(((-0.6, 0.4, 1.8), (0, -40, 0))) ), (1.0, REST)]},
    # 15c: right hand raised, left counts three beats overhead, right cocks back and chops at share 0.36 = Go at 0.8 s (body: lean forward 0.5-0.7)
    {"id": "15c", "name": "Race Starter", "length": 2.2, "keys": [
        (0.0, REST),
        (0.08, pose(Hand_Right=((-0.8, -0.3, 2.6), (0, -50, 0)), Hand_Left=((-0.8, 0.3, 2.4), (0, -60, 0)))),
        (0.15, pose(Hand_Right=((-1.0, -0.5, 3.0), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.9), (0, -60, 0)))),
        (0.185, pose(Hand_Right=((-1.0, -0.5, 3.05), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.4), (0, -60, 0)))),
        (0.22, pose(Hand_Right=((-1.0, -0.5, 3.1), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.9), (0, -60, 0)))),
        (0.255, pose(Hand_Right=((-1.0, -0.5, 3.15), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.4), (0, -60, 0)))),
        (0.29, pose(Hand_Right=((-1.0, -0.5, 3.2), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.9), (0, -60, 0)))),
        (0.32, pose(Hand_Right=((-1.1, -0.5, 3.4), (0, -70, 0)), Hand_Left=((-0.8, 0.3, 2.6), (0, -60, 0)))),
        (0.36, pose(Hand_Right=((-0.2, -1.5, 1.0), (0, 40, -30)), Hand_Left=((-0.7, 0.5, 1.6), (0, -40, 0)))),
        (0.42, pose(Hand_Right=((-0.2, -1.5, 1.0), (0, 40, -30)), Hand_Left=((-0.5, 0.8, 1.0), (0, -20, 0)))),
        (0.75, pose(Hand_Right=((-0.2, -1.5, 0.8), (0, 40, -30)), Hand_Left=((-0.5, 0.8, 1.0), (0, -20, 0)))),
        (1.0, REST),
    ]},
    # 19a: rub strokes above the head, the hands sliding past each other and pulling apart between strokes
    # (body: hunched up and forward, turned a little left, jiggling with the strokes, 0.1-0.85)
    {"id": "19a", "name": "Hand Rub", "length": 2.0, "keys": [
        (0.0, REST),
        (0.1, _lr(((-0.9, -0.85, 2.2), (0, -40, 0)))),
    ] + _rub_wide((-0.9, -0.85, 2.4), 16, 0.16, 0.05, -40) + [
        (0.92, _lr(((-0.9, -0.85, 2.2), (0, -40, 0)))),
        (1.0, REST),
    ]},
    # 19b: slow rub clear above the hat, then one sly finger tap (body: turn to camera 0-0.2, hold, turn back 0.9-1)
    {"id": "19b", "name": "Sly Rub", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, _lr(((-0.9, -0.85, 2.6), (0, -30, 0)))),
    ] + _rub((-0.9, -0.85, 3.0), 0.4, 8, 0.17, 0.08, -30) + [
        (0.84, pose(Hand_Left=((-0.9, -0.85, 3.0), (0, -30, 0)), Hand_Right=((-1.0, 0.3, 3.2), (0, -60, 0)))),
        (0.9, pose(Hand_Left=((-0.9, -0.85, 3.0), (0, -30, 0)), Hand_Right=((-1.0, 0.3, 2.9), (0, -30, 0)), Finger_Right=(0, 0, -50))),
        (0.95, pose(Hand_Left=((-0.9, -0.85, 3.0), (0, -30, 0)), Hand_Right=((-1.0, 0.3, 3.2), (0, -60, 0)))),
        (1.0, REST),
    ]},
    # 19c: fingertips tap together above the head, slowly, swinging in from wide each time (body: sly lean in, head tilt)
    {"id": "19c", "name": "Excellent", "length": 2.5, "keys": [
        (0.0, REST),
        (0.12, _lr(((-0.9, 0.4, 2.6), (0, -50, 20)))),
    ] + _seq(0.22, 0.10, [_lr(((-0.9, -0.7, 2.9), (0, -50, 0)) if i % 2 == 0 else ((-0.9, 1.2, 2.6), (0, -50, 40)))
                          for i in range(7)]) + [
        (0.92, _lr(((-0.9, -0.3, 2.7), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 20c: box hugged tight to the chest, a little to the left, squeezed in at the middle (body: hunch, turn away to the left and look back, 0.3-0.8)
    {"id": "20c", "name": "Parcel Clutch", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.5, -0.95, 0.6), (0, -20, 0)), Hand_Right=((-0.5, 0.95, 0.6), (0, -20, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.35, pose(Hand_Left=((-0.4, 0.1, 1.3), (0, -10, 20)), Hand_Right=((-0.4, 1.5, 1.3), (0, -10, 20)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.5, pose(Hand_Left=((-0.4, 0.0, 1.3), (0, -10, 20)), Hand_Right=((-0.4, 1.4, 1.3), (0, -10, 20)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.65, pose(Hand_Left=((-0.4, 0.1, 1.3), (0, -10, 20)), Hand_Right=((-0.4, 1.5, 1.3), (0, -10, 20)), Finger_Left=FIST, Finger_Right=FIST)),
        (0.8, pose(Hand_Left=((-0.3, 0.1, 0.9), (0, -10, 0)), Hand_Right=((-0.3, 1.5, 0.9), (0, -10, 0)), Finger_Left=FIST, Finger_Right=FIST)),
        (1.0, REST),
    ]},
    # 21a: alternating drum strokes, each lifts to the horns' height out past its own side then slams down (body: bob 0.1-0.9)
    {"id": "21a", "name": "Drum Solo", "length": 2.2, "keys": [(0.0, REST)] + _drum() + [(1.0, REST)]},
    # 21b: left wrist raised at the side, right finger taps it twice, hands drop (body: slight head tilt to the watch)
    {"id": "21b", "name": "Watch Check", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, pose(Hand_Left=((-0.4, 0.9, 1.5), (0, -40, 10)), Hand_Right=((-1.0, 1.0, 2.2), (0, -60, 0)))),
        (0.15, pose(Hand_Left=((-0.4, 1.4, 1.8), (0, -40, 20)), Hand_Right=((-0.6, 2.7, 1.7), (0, -30, 0)))),
        (0.3, pose(Hand_Left=((-0.4, 1.4, 1.9), (0, -40, 20)), Hand_Right=((-0.6, 2.8, 2.2), (0, -60, 0)))),
        (0.4, pose(Hand_Left=((-0.4, 1.4, 1.9), (0, -40, 20)), Hand_Right=((-0.6, 2.8, 1.85), (0, -30, 0)), Finger_Right=(0, 0, -40))),
        (0.5, pose(Hand_Left=((-0.4, 1.4, 1.9), (0, -40, 20)), Hand_Right=((-0.6, 2.8, 2.2), (0, -60, 0)))),
        (0.6, pose(Hand_Left=((-0.4, 1.4, 1.9), (0, -40, 20)), Hand_Right=((-0.6, 2.8, 1.85), (0, -30, 0)), Finger_Right=(0, 0, -40))),
        (0.72, pose(Hand_Left=((-0.4, 1.4, 1.9), (0, -40, 20)), Hand_Right=((-0.6, 2.8, 2.2), (0, -60, 0)))),
        (0.86, pose(Hand_Left=((-0.4, 0.6, 0.8), (0, 0, 0)), Hand_Right=((-0.4, 1.0, 0.8), (0, 0, 0)))),
        (1.0, REST),
    ]},
    # 21c: one hand stretches up and out past its side for a yawn while the other pats the mouth, drops (body: lean back and stretch tall 0.1-0.45, head tipped far back)
    {"id": "21c", "name": "Big Yawn", "length": 2.5, "keys": [
        (0.0, REST),
        (0.2, pose(Hand_Left=((-0.9, 1.2, 2.1), (0, -60, 30)), Hand_Right=((0.3, 0.9, 0.5), (0, -20, 0)))),
        (0.4, pose(Hand_Left=((-0.9, 1.6, 2.2), (0, -60, 40)), Hand_Right=((0.3, 0.9, 0.5), (0, -20, 0)))),
        (0.52, pose(Hand_Left=((-0.9, 1.6, 2.2), (0, -60, 40)), Hand_Right=((0.3, 0.9, 0.5), (0, -20, 0)))),
        (0.62, pose(Hand_Left=((-0.4, 0.5, 0.2), (0, 20, 0)), Hand_Right=((0.4, 0.9, 0.7), (0, -30, 0)))),
        (0.7, pose(Hand_Left=((-0.4, 0.5, 0.2), (0, 20, 0)), Hand_Right=((0.3, 0.9, 0.5), (0, -20, 0)))),
        (0.78, pose(Hand_Left=((-0.4, 0.5, 0.2), (0, 20, 0)), Hand_Right=((0.4, 0.9, 0.7), (0, -30, 0)))),
        (0.9, pose(Hand_Left=((-0.2, 0.3, 0.1), (0, 10, 0)), Hand_Right=((0.1, 0.3, 0.3), (0, 10, 0)))),
        (1.0, REST),
    ]},
    # 22a: hands at the sides of the head by the ears, fingers waggling fast (body: turn to camera 0-0.12, hold, turn back 0.9-1)
    {"id": "22a", "name": "Thumbs In Ears", "length": 2.0, "keys": [
        (0.0, REST),
        (0.12, pose(Hand_Left=((-1.1, EAR, 0.8), (0, -40, 0)), Hand_Right=((-1.1, -EAR, 0.8), (0, -40, 0)))),
    ] + _waggle(14, 0.18, 0.055) + [
        (0.94, pose(Hand_Left=((-1.1, EAR, 0.8), (0, -40, 0)), Hand_Right=((-1.1, -EAR, 0.8), (0, -40, 0)))),
        (1.0, REST),
    ]},
    # 23a: right hand straightens the hat, left flourishes out at the side (body: slight proud lean back)
    {"id": "23a", "name": "Admire", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Right=((-1.25, 0.85, 1.9), (0, -50, 0)), Hand_Left=((-0.6, 1.0, 1.4), (0, -30, 20)))),
        (0.3, pose(Hand_Right=((-1.25, 0.75, 2.15), (15, -50, 0)), Hand_Left=((-0.6, 1.7, 1.9), (0, -30, 40)))),
        (0.45, pose(Hand_Right=((-1.25, 0.95, 1.9), (-15, -50, 0)), Hand_Left=((-0.6, 1.0, 1.4), (0, -30, 20)))),
        (0.6, pose(Hand_Right=((-1.25, 0.75, 2.15), (15, -50, 0)), Hand_Left=((-0.6, 1.8, 2.1), (0, -30, 40)))),
        (0.75, pose(Hand_Right=((-1.25, 0.95, 1.9), (-15, -50, 0)), Hand_Left=((-0.6, 1.2, 1.6), (0, -30, 20)))),
        (0.88, pose(Hand_Right=((-1.25, 0.85, 1.7), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 23b: left hand held out past the left side at face height like a mirror, small tilt (body: turn to the left, face peeking round at it, head tilt side to side)
    {"id": "23b", "name": "Hand Mirror", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, pose(Hand_Left=((-0.3, 1.2, 1.6), (0, -40, 20)))),
        (0.3, pose(Hand_Left=((-0.2, 1.6, 1.9), (0, -40, 20)))),
        (0.45, pose(Hand_Left=((-0.2, 1.6, 1.9), (-8, -40, 20)))),
        (0.6, pose(Hand_Left=((-0.2, 1.6, 1.9), (8, -40, 20)))),
        (0.75, pose(Hand_Left=((-0.2, 1.6, 1.9), (-5, -40, 20)))),
        (0.88, pose(Hand_Left=((-0.4, 0.7, 1.0), (0, -10, 0)))),
        (1.0, REST),
    ]},
    # 24a: hands laced over the head (no higher than the horn tips: LOBBY_UP), push up, three pops, then push out past
    # the sides, release
    # (body: a lift, stretch and hat bump on each pop, a widening on the push out)
    {"id": "24a", "name": "Lace And Push", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, _lr(((-0.9, -0.8, 1.9), (0, -60, 0)))),
        (0.3, _lr(((-0.9, -0.8, 1.9), (0, -60, 0)))),
        (0.4, _lr(((-0.9, -0.85, 2.2), (0, -70, 0)))),
        (0.46, _lr(((-0.9, -0.85, 1.95), (0, -70, 0)))),
        (0.54, _lr(((-0.9, -0.85, 2.2), (0, -70, 0)))),
        (0.6, _lr(((-0.9, -0.85, 1.95), (0, -70, 0)))),
        (0.68, _lr(((-0.9, -0.85, 2.2), (0, -70, 0)))),
        (0.78, _lr(((-0.9, 1.5, 2.1), (0, -40, 50)))),
        (0.86, _lr(((-0.9, 1.5, 2.0), (0, -40, 50)))),
        (0.93, _lr(((-0.8, 0.8, 1.2), (0, -20, 20)))),
        (1.0, REST),
    ]},
]


# ---- Batch D ----
def _pump():
    up = ((-0.8, 0.3, 3.0), (0, -60, 0))
    lo = ((-0.8, 0.5, 1.6), (0, -40, 0))
    ks = []
    for i in range(10):
        left_up = (i % 2 == 0)
        ks.append(pose(Hand_Left=up if left_up else lo, Hand_Right=_mirror(lo if left_up else up)))
    return _seq(0.14, 0.075, ks)


def _roof():
    hi = ((-0.8, 0.4, 3.3), (0, -70, 0))
    lo = ((-0.8, 0.4, 2.2), (0, -50, 0))
    ks = []
    for i in range(8):
        ks.append(_lr(lo if i % 2 == 0 else hi))
    return _seq(0.16, 0.085, ks)


def _pound():
    wind = ((-0.6, 0.3, 2.8), (0, -50, 0))
    slam = ((-0.3, 1.3, 0.1), (0, 20, 40))
    ks = []
    for i in range(10):
        left_wind = (i % 2 == 0)
        l = wind if left_wind else slam
        r = slam if left_wind else wind
        ks.append(pose(Hand_Left=l, Hand_Right=_mirror(r), Finger_Left=FIST, Finger_Right=FIST))
    return _seq(0.12, 0.08, ks)


def _polish():
    pts = _circle((-0.4, 3.0, 1.7), 0.3, 4, 0.0, axes=(0, 2))
    ks = []
    for i in range(16):
        ks.append(pose(Hand_Left=((-0.6, 1.6, 1.8), (0, -30, 20)), Hand_Right=(pts[i % 4], (0, -30, 0))))
    return _seq(0.3, 0.035, ks)


def _sob():
    """Hands over the face, shaking up and down with each sob"""
    ks = []
    for i in range(5):
        s = 1 if i % 2 else -1
        ks.append(_lr(((-0.5, -0.5, 0.75 + 0.1 * s), (0, -20, 0))))
    return _seq(0.25, 0.125, ks)


CLIPS += [
    # 16a: both hands lift the crown high overhead, hold still, lower slowly (body: none; crown follows the midpoint of the hands, 0.15-0.85, in the hands' pose)
    {"id": "16a", "name": "Crown Overhead", "length": 2.5, "keys": [
        (0.0, REST),
        (0.18, _lr(((-0.8, -0.8, 2.6), (0, -60, 0)))),
        (0.35, _lr(((-0.8, -0.8, 3.3), (0, -60, 0)))),
        (0.6, _lr(((-0.8, -0.8, 3.3), (0, -60, 0)))),
        (0.75, _lr(((-0.8, -0.8, 3.3), (0, -60, 0)))),
        (0.9, _lr(((-1.25, -0.85, 1.8), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 16b: alternating arm pumps above the head (body: bounce on each pump, 0.1-0.9; optional sway)
    {"id": "16b", "name": "Arm Pump Dance", "length": 2.5, "keys": [(0.0, REST)] + _pump() + [
        (0.92, _lr(((-0.8, 0.5, 1.6), (0, -40, 0)))), (1.0, REST)]},
    # 16c: arms straight out wide, level (body: spin on the spot 0.15-0.85, hold arms through it)
    {"id": "16c", "name": "Arms Out Spin", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, _lr(((-0.4, 1.0, 0.8), (0, -10, 30)))),
        (0.25, _lr(((-0.6, 1.8, 1.0), (0, -10, 45)))),
        (0.5, _lr(((-0.6, 1.8, 1.0), (0, -10, 45)))),
        (0.8, _lr(((-0.6, 1.8, 1.0), (0, -10, 45)))),
        (0.9, _lr(((-0.4, 1.0, 0.8), (0, -10, 30)))),
        (1.0, REST),
    ]},
    # 16d: palms push up overhead four times (body: small knee-bob on each push, 0.15-0.8)
    {"id": "16d", "name": "Raise The Roof", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, _lr(((-0.8, 0.4, 1.8), (0, -50, 0)))),
    ] + _roof() + [
        (0.88, _lr(((-0.8, 0.4, 1.8), (0, -50, 0)))),
        (1.0, REST),
    ]},
    # 17a: alternating fist pounds, wind-up above the horns, slam out past the sides (body: stomp/hop each pound 0.12-0.9)
    {"id": "17a", "name": "Fist Pound", "length": 2.5, "keys": [(0.0, REST)] + _pound() + [
        (0.94, pose(Hand_Left=((-0.3, 1.0, 0.0), (0, 20, 40)), Hand_Right=((-0.3, -1.0, 0.0), (0, 20, -40)))), (1.0, REST)]},
    # 17b: arms fling up and back, splayed, limp (body: flop backwards 0.15-0.5, lie there, get up 0.85-1)
    {"id": "17b", "name": "Flop Back", "length": 2.5, "keys": [
        (0.0, REST),
        (0.12, _lr(((-0.8, 0.8, 2.0), (0, -30, 30)))),
        (0.22, _lr(((-1.6, 1.6, 3.0), (0, -40, 50)))),
        (0.4, _lr(((-1.6, 1.6, 3.0), (0, -40, 50)))),
        (0.55, _lr(((-1.4, 1.8, 2.6), (20, -20, 50)))),
        (0.8, _lr(((-1.4, 1.8, 2.7), (10, -20, 50)))),
        (0.92, _lr(((-0.6, 1.0, 1.0), (0, -10, 20)))),
        (1.0, REST),
    ]},
    # 17c: face in the hands, shaking sob (body: head bowed, shoulders heave big, 0.2-0.85, bob in time with the shake)
    {"id": "17c", "name": "Sob", "length": 2.5, "keys": [
        (0.0, REST),
        (0.15, _lr(((-0.4, -0.4, 0.7), (0, -20, 0)))),
    ] + _sob() + [
        (0.9, _lr(((-0.4, -0.4, 0.7), (0, -20, 0)))),
        (1.0, REST),
    ]},
    # 25a: left holds the crown out at the side at head height, right rubs it, then crown back on (body: none; crown follows Hand.Left 0.15-0.78, then glides to the head)
    {"id": "25a", "name": "Polish", "length": 2.5, "keys": [
        (0.0, REST),
        (0.1, pose(Hand_Left=((-0.8, -0.85, 1.7), (0, -50, 0)))),
        (0.16, pose(Hand_Left=((-0.7, 0.4, 1.75), (0, -40, 10)), Hand_Right=((-1.1, 1.0, 2.6), (0, -60, 0)))),
        (0.22, pose(Hand_Left=((-0.6, 1.6, 1.8), (0, -30, 20)), Hand_Right=((-0.4, 3.0, 1.7), (0, -30, 0)))),
    ] + _polish() + [
        (0.9, pose(Hand_Left=((-1.25, -0.85, 1.8), (0, -50, 0)), Hand_Right=((-0.4, -0.5, 1.0), (0, 0, 0)))),
        (1.0, REST),
    ]},
]
