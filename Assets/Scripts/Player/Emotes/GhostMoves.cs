using System;
using System.Collections.Generic;
using UnityEngine;
using static PodiumActs;

/// <summary>
/// The catalogue of ghost emote versions: the body's part in code, and for some a hand clip (GhostEmotes.fbx) whose
/// length is the move's.
/// Every move starts and ends at rest, except entrances, which start hidden
/// </summary>
public static class GhostMoves
{
    static readonly GhostMove[] none = new GhostMove[0];
    static readonly GhostMove[] all = Build();
    static readonly Dictionary<GhostAction, GhostMove[]> byAction = Group(all);

    /// <summary>The versions of an action (empty when it has none yet)</summary>
    public static GhostMove[] For(GhostAction action)
    {
        GhostMove[] moves;
        return byAction.TryGetValue(action, out moves) ? moves : none;
    }

    /// <summary>Every version of every action</summary>
    public static IEnumerable<GhostMove> All
    {
        get { return all; }
    }

    static Dictionary<GhostAction, GhostMove[]> Group(GhostMove[] moves)
    {
        Dictionary<GhostAction, List<GhostMove>> lists = new Dictionary<GhostAction, List<GhostMove>>();
        foreach (GhostMove move in moves)
        {
            if (!lists.ContainsKey(move.Action))
                lists[move.Action] = new List<GhostMove>();
            lists[move.Action].Add(move);
        }

        Dictionary<GhostAction, GhostMove[]> result = new Dictionary<GhostAction, GhostMove[]>();
        foreach (KeyValuePair<GhostAction, List<GhostMove>> pair in lists)
            result[pair.Key] = pair.Value.ToArray();
        return result;
    }

    // 0 to 1 over the span from..to, clamped
    static float Span(float t, float from, float to)
    {
        return Mathf.Clamp01((t - from) / (to - from));
    }

    // Whole turns: eased from 0 to that many degrees, and back to exactly 0 at the end (the same look as the full turn)
    static float Turn(float degrees, float t)
    {
        return t >= 1f ? 0f : degrees * Smooth(t);
    }

    // Eased 0 to 1 over the span from..to (0 before, 1 after)
    static float In(float t, float from, float to)
    {
        return Smooth(Span(t, from, to));
    }

    // One hump over the span from..to: 0 outside it
    static float Bump(float t, float from, float to)
    {
        return t <= from || t >= to ? 0f : Arc(Span(t, from, to));
    }

    // Up over the first share of from..to, held, down over the last: 0 outside it
    static float Window(float t, float from, float to, float rise, float fall)
    {
        return Hold(Span(t, from, to), rise, fall);
    }

    // Side to side: +1 at "at" and every period after, -1 half a period after each
    static float Swing(float t, float at, float period)
    {
        return Mathf.Cos(2f * Mathf.PI * (t - at) / period);
    }

    // The part 1 turn round to the camera: turned by "turned", back from "back", the head leading: it starts each
    // turn first (round and back) and the body follows a beat later, both there by "turned" and home by the end
    static GhostPose Round(float t, float s, float turned, float back)
    {
        float lag = 0.35f;
        float head = In(t, 0f, turned * (1f - lag)) * (1f - In(t, back, 1f - (1f - back) * lag));
        float body = In(t, turned * lag, turned) * (1f - In(t, back + (1f - back) * lag, 1f));
        return P(yaw: s * 100f * body, headYaw: s * 60f * head);
    }

    // A version with a hand clip: its state is GE_<spec id>_<name without spaces>, and its length is the clip's
    static GhostMove Clip(GhostAction action, string id, string name, float length, Func<float, float, GhostPose> at,
        bool entrance = false)
    {
        return new GhostMove(action, name, length, at, entrance, GhostExtra.None, "GE_" + id + "_" + name.Replace(" ", ""));
    }

    static GhostPose P(float lift = 0f, float push = 0f, float shift = 0f, float pitch = 0f, float yaw = 0f,
        float roll = 0f, float tall = 1f, float wide = 1f, float headYaw = 0f, float headPitch = 0f,
        float headRoll = 0f, float headPop = 0f, float tailUp = 0f, float tailWag = 0f, float hatLift = 0f,
        float hatSpin = 0f, float hatTip = 0f, float fade = 0f, float hatFade = 0f)
    {
        return new GhostPose
        {
            Lift = lift, Push = push, Shift = shift, Pitch = pitch, Yaw = yaw, Roll = roll, Tall = tall, Wide = wide,
            HeadYaw = headYaw, HeadPitch = headPitch, HeadRoll = headRoll, HeadPop = headPop,
            TailUp = tailUp, TailWag = tailWag, HatLift = hatLift, HatSpin = hatSpin, HatTip = hatTip,
            Fade = fade, HatFade = hatFade
        };
    }

    static GhostMove[] Build()
    {
        return new[]
        {
            /// <summary>1a: shoulders bounce, head tips back and the body shakes side to side</summary>
            new GhostMove(GhostAction.Cackle, "Shoulder Shake", 1.7f, (t, s) =>
                P(lift: 0.2f * Bounces(t, 5), headPitch: -35f * Hold(t, .15f, .2f), roll: 10f * Wave(t, 6))),

            /// <summary>1c: turns round to the camera and cackles with little hops</summary>
            new GhostMove(GhostAction.Cackle, "Turn And Cackle", 2.2f, (t, s) =>
            {
                float h = Hold(t, .2f, .2f);
                return P(yaw: s * 100f * h, headYaw: s * 60f * h, lift: 0.06f * h * Mathf.Abs(Mathf.Sin(10f * Mathf.PI * t)));
            }),

            /// <summary>2a: pumps a fist overhead three times, bouncing with each pump (hand clip)</summary>
            new GhostMove(GhostAction.FistShake, "Fist Pump", 1.7f, (t, s) =>
                P(lift: 0.12f * Bounces(t, 3)), false, GhostExtra.None, "GE_2a_FistPump"),

            /// <summary>5b: the hat floats up and spins once, then settles</summary>
            new GhostMove(GhostAction.Wheee, "Floating Hat", 1.5f, (t, s) =>
                P(hatLift: 1.5f * Hold(t, .25f, .2f), hatSpin: Turn(360f, t))),

            /// <summary>6a: head rolls round in a circle, dizzy</summary>
            new GhostMove(GhostAction.SeeingStars, "Head Rolls", 2.0f, (t, s) =>
            {
                float e = Hold(t, .1f, .3f);
                return P(headRoll: 35f * Mathf.Sin(4f * Mathf.PI * t) * e,
                    headPitch: 25f * (1f - Mathf.Cos(4f * Mathf.PI * t)) * 0.5f * e,
                    roll: 10f * Mathf.Sin(4f * Mathf.PI * t) * e);
            }),

            /// <summary>6b: sways side to side like it might fall over</summary>
            new GhostMove(GhostAction.SeeingStars, "Sway", 2.0f, (t, s) =>
                P(roll: 28f * Wave(t, 2))),

            /// <summary>6c: the hat spins round twice above the head</summary>
            new GhostMove(GhostAction.SeeingStars, "Hat Spin", 1.7f, (t, s) =>
                P(hatSpin: Turn(720f, t), hatLift: 0.4f * Arc(t), hatTip: 20f * Mathf.Sin(4f * Mathf.PI * t) * Arc(t))),

            /// <summary>6d: the head pops up and wobbles like a bobblehead</summary>
            new GhostMove(GhostAction.SeeingStars, "Bobble Head", 1.8f, (t, s) =>
                P(headPop: 1.0f * Hold(t, .08f, .3f), headRoll: 35f * Wave(t, 4))),

            /// <summary>7c: turns round to check nobody is behind</summary>
            new GhostMove(GhostAction.TreasureHold, "Check Behind", 2.0f, (t, s) =>
            {
                float h = Hold(t, .2f, .25f);
                return P(yaw: s * 100f * h, headYaw: s * 60f * h);
            }),

            /// <summary>8a: leaps up stretched, lands by 85%, then squashes on landing</summary>
            new GhostMove(GhostAction.JumpScare, "Leap", 1.5f, (t, s) =>
            {
                float air = Arc(Span(t, 0f, .85f));
                return P(lift: 2.5f * air, tall: 1f + 0.15f * air - 0.12f * Land(t), wide: 1f + 0.12f * Land(t),
                    tailUp: 60f * air);
            }),

            /// <summary>8c: sinks low and squashes down to hide</summary>
            new GhostMove(GhostAction.JumpScare, "Hide", 2.5f, (t, s) =>
            {
                float h = Hold(t, .12f, .25f);
                return P(lift: -0.55f * h, tall: 1f - 0.35f * h);
            }),

            /// <summary>9d: slumps over and snores, with floating z letters</summary>
            new GhostMove(GhostAction.Bored, "Nap", 2.5f, (t, s) =>
            {
                float h = Hold(t, .15f, .15f);
                return P(pitch: 30f * h, headPitch: 40f * h, tall: 1f + 0.12f * Mathf.Sin(4f * Mathf.PI * t) * h);
            }, false, GhostExtra.Zzz),

            /// <summary>10b: leans side to side with the hands waggling by the tail, the tail wagging (hand clip)</summary>
            Clip(GhostAction.Taunt, "10b", "Bum Wiggle", 2.2f, (t, s) =>
                P(roll: -15f * Window(t, .15f, .85f, .15f, .15f) * Swing(t, .27f, .3f), tailWag: 60f * Wave(t, 4))),

            /// <summary>11a: a big wave with one hand high overhead, the body swaying with it (hand clip)</summary>
            new GhostMove(GhostAction.BigWave, "Overhead Wave", 2.2f, (t, s) =>
                P(roll: 6f * Wave(t, 2)), false, GhostExtra.None, "GE_11a_OverheadWave"),

            /// <summary>12b: spins all the way round with a little hop</summary>
            new GhostMove(GhostAction.HatShowOff, "Spin Round", 1.7f, (t, s) =>
                P(yaw: Turn(360f, t), lift: 0.25f * Arc(t))),

            /// <summary>12c: the hat jumps up and does a full flip</summary>
            new GhostMove(GhostAction.HatShowOff, "Hat Flip", 1.5f, (t, s) =>
                P(hatLift: 0.9f * Arc(t), hatTip: Turn(360f, t))),

            /// <summary>13a: fades in from a thin wisp, then shakes like a wet dog</summary>
            new GhostMove(GhostAction.Materialise, "Wisp", 2.2f, (t, s) =>
            {
                // 0..1 over the first 60%: fade in and fill out
                float a = Smooth(Span(t, 0f, .6f));
                return P(fade: 1f - a, hatFade: 1f - a, tall: 0.4f + 0.6f * a, roll: 10f * Wave(Span(t, .6f, 1f), 4));
            }, true),

            /// <summary>13c: drops in from above, squashes on landing and wags its tail</summary>
            new GhostMove(GhostAction.Materialise, "Drop In", 1.7f, (t, s) =>
            {
                // Hidden only at the very first instant, then falls in
                float hidden = t <= 0f ? 1f : 0f;
                // Squash bump from 50% to 70%
                float b = Mathf.Sin(Mathf.PI * Span(t, .5f, .7f));
                return P(lift: 4f * (1f - Smooth(Span(t, 0f, .5f))), tall: 1f - 0.3f * b, wide: 1f + 0.25f * b,
                    tailUp: 30f * Wave(Span(t, .5f, 1f), 2), fade: hidden, hatFade: hidden);
            }, true),

            /// <summary>14a: both hands stretch up overhead, the body standing tall and leaning back (hand clip)</summary>
            new GhostMove(GhostAction.WarmUp, "Big Stretch", 2.5f, (t, s) =>
            {
                float h = Hold(t, .25f, .3f);
                return P(tall: 1f + 0.08f * h, pitch: -8f * h);
            }, false, GhostExtra.None, "GE_14a_BigStretch"),

            /// <summary>14b: rolls its head round slowly like loosening a stiff neck</summary>
            new GhostMove(GhostAction.WarmUp, "Neck Roll", 2.2f, (t, s) =>
            {
                float e = Hold(t, .1f, .1f);
                return P(headRoll: 30f * Mathf.Sin(2f * Mathf.PI * t) * e,
                    headPitch: 25f * (1f - Mathf.Cos(2f * Mathf.PI * t)) * 0.5f * e, roll: 12f * Wave(t, 2));
            }),

            /// <summary>15a: two bounces, the second higher, each stretching up and squashing once it lands</summary>
            new GhostMove(GhostAction.RevBounce, "Bounce", 1.5f, (t, s) =>
            {
                // In the air over 0-40% and 50-88%; on the ground (squashing) over 40-50% and 88-100%
                float hop1 = Arc(Span(t, 0f, .4f)), hop2 = Arc(Span(t, .5f, .88f));
                float land1 = Bump(t, .4f, .5f), land2 = Bump(t, .88f, 1f);
                return P(lift: 0.35f * hop1 + 0.6f * hop2, tall: 1f + 0.06f * hop1 + 0.09f * hop2 - 0.1f * land1 - 0.12f * land2,
                    wide: 1f + 0.08f * land1 + 0.09f * land2);
            }),

            /// <summary>15b: rocks side to side</summary>
            new GhostMove(GhostAction.RevBounce, "Rock", 1.5f, (t, s) =>
                P(roll: 26f * Wave(t, 3))),

            /// <summary>18a: sinks down and fades out, then pops back with a little overshoot</summary>
            new GhostMove(GhostAction.FadeAway, "Sink", 2.5f, (t, s) =>
            {
                // 1 while gone: out over the first 40%, hidden to 75%, back by the end
                float k = Smooth(Span(t, 0f, .4f)) - Smooth(Span(t, .75f, 1f));
                float pop = 0.1f * Mathf.Sin(Mathf.PI * Span(t, .75f, 1f));
                return P(lift: -0.6f * k, tall: 1f - 0.2f * k + pop, fade: k, hatFade: k);
            }),

            /// <summary>18b: the body fades away and the hat drops and bounces, then it all comes back</summary>
            new GhostMove(GhostAction.FadeAway, "Hat Left Behind", 2.5f, (t, s) =>
            {
                // Body gone by 30%, back over the last 20%
                float k = Smooth(Span(t, 0f, .3f)) - Smooth(Span(t, .8f, 1f));
                // The hat drops over 45-55%, bounces, and goes back over the last 20%
                float hat = -0.6f * Smooth(Span(t, .45f, .55f)) + 0.08f * Mathf.Sin(Mathf.PI * Span(t, .55f, .7f));
                hat *= 1f - Smooth(Span(t, .8f, 1f));
                return P(fade: k, hatLift: hat);
            }),

            /// <summary>18c: a gust blows it away, then it drops back in and squashes</summary>
            new GhostMove(GhostAction.FadeAway, "Blown Away", 2.5f, (t, s) =>
            {
                // The gust, eased to nothing while hidden between 45% and 80%
                float u = Mathf.Clamp01(t / .45f);
                float w = 1f - Smooth(Span(t, .45f, .8f));
                float shift = 6f * u * u * w;
                // Leans with the wind: the gust blows it to its right (+shift), so it leans right too
                float roll = 25f * Smooth(Span(t, 0f, .15f)) * w;
                float lift = 0.8f * u * w;
                // Gone from 45% to 80%, then the drop in
                float fade = Smooth(Span(t, .3f, .45f)) * (1f - Smooth(Span(t, .8f, .84f)));
                lift += 3f * (1f - Smooth(Span(t, .8f, .92f))) * (t >= .8f ? 1f : 0f);
                float b = Mathf.Sin(Mathf.PI * Span(t, .92f, 1f));
                return P(lift: lift, shift: shift, roll: roll, tall: 1f - 0.3f * b, wide: 1f + 0.25f * b,
                    fade: fade, hatFade: fade);
            }),

            /// <summary>20a: slowly turns to look back over its shoulder, then snaps round</summary>
            new GhostMove(GhostAction.SideEye, "Look Back", 2.2f, (t, s) =>
            {
                float h = Hold(t, .35f, .1f);
                return P(yaw: s * 45f * h, headYaw: s * 60f * h);
            }),

            /// <summary>20b: a quick glance to the side, then a hop round for a proper look</summary>
            new GhostMove(GhostAction.SideEye, "Double Take", 2.1f, (t, s) =>
            {
                float glance = s * 50f * Arc(Mathf.Clamp01(t / .2f));
                float h = Hold(Span(t, .45f, .85f), .3f, .3f);
                return P(yaw: s * 100f * h, headYaw: glance + s * 60f * h, lift: 0.2f * Arc(Span(t, .45f, .6f)));
            }),

            /// <summary>22b: points down at its tail with its right hand and waves overhead, leaning toward the pointing hand (hand clip)</summary>
            Clip(GhostAction.Raspberry, "22b", "Tail Point", 1.8f, (t, s) =>
            {
                float k = Window(t, .15f, .8f, .25f, .25f);
                return P(roll: 8f * k, headRoll: 10f * k);
            }),

            /// <summary>24b: cracks its neck one way, then the other, with a small hop at each</summary>
            new GhostMove(GhostAction.KnuckleCrack, "Neck Crack", 1.5f, (t, s) =>
            {
                // Snap to -40 at 10-20%, to +40 at 45-55%, then back to 0 over 75-100%
                float roll = -40f * Smooth(Span(t, .1f, .2f)) + 80f * Smooth(Span(t, .45f, .55f))
                    - 40f * Smooth(Span(t, .75f, 1f));
                float bump = 0.15f * (Arc(Span(t, .1f, .2f)) + Arc(Span(t, .45f, .55f)));
                return P(headRoll: roll, lift: bump);
            }),

            // ---- Hand clip versions (GhostEmotes.fbx; the tables are tools/ghost-clips/clips.py). Yaw, Roll and
            // HeadRoll here: + is the ghost's right

            /// <summary>1b: holds the parcel overhead and shakes it side to side, leaning with it</summary>
            Clip(GhostAction.Cackle, "1b", "Parcel Shake", 2.1f, (t, s) =>
                P(roll: -10f * Window(t, .12f, .86f, .2f, .2f) * Swing(t, .27f, .3f))).Holding(GhostProp.Parcel),

            /// <summary>1d: spins the parcel on one raised finger, leaning away from it</summary>
            Clip(GhostAction.Cackle, "1d", "Parcel Spin", 2.2f, (t, s) =>
                P(roll: -8f * Hold(t, .2f, .2f))).Holding(GhostProp.ParcelSpin),

            /// <summary>2b: flails both arms overhead, bobbing</summary>
            Clip(GhostAction.FistShake, "2b", "Arm Flail", 1.7f, (t, s) =>
                P(lift: 0.1f * Bounces(Span(t, .1f, .8f), 4))),

            /// <summary>
            /// 2c: throws its hat down onto the seat to its right: it drops, spinning, and is gone by 65%; it goes back on
            /// while out of sight (65-80%) and fades back in by 95%
            /// </summary>
            Clip(GhostAction.FistShake, "2c", "Hat Throw", 1.7f, (t, s) =>
                P(roll: 15f * In(t, .4f, .55f) * (1f - In(t, .7f, .95f)), lift: 0.15f * Bump(t, .4f, .6f),
                    hatLift: -0.6f * In(t, .45f, .65f) * (1f - In(t, .68f, .78f)),
                    hatSpin: t < .66f ? 360f * In(t, .45f, .65f) : 0f,
                    hatFade: In(t, .5f, .65f) * (1f - In(t, .8f, .95f)))),

            /// <summary>2d: lunges forward grabbing at the air</summary>
            Clip(GhostAction.FistShake, "2d", "Lunge Grab", 1.8f, (t, s) =>
                P(pitch: 20f * In(t, .1f, .5f) * (1f - In(t, .6f, .9f)))),

            /// <summary>3a: a sweeping bow with a flourish of the hand</summary>
            Clip(GhostAction.DeliveryFlourish, "3a", "Sweeping Bow", 2.2f, (t, s) =>
                P(pitch: 25f * In(t, .4f, .7f) * (1f - In(t, .75f, .95f)), roll: 6f * Window(t, .1f, .95f, .3f, .3f))),

            /// <summary>3b: a fist high in the air with a hop</summary>
            Clip(GhostAction.DeliveryFlourish, "3b", "High Fist", 1.7f, (t, s) =>
                P(lift: 0.3f * Bump(t, .2f, .45f))),

            /// <summary>3c: lifts its hat high, leaning back</summary>
            Clip(GhostAction.DeliveryFlourish, "3c", "Hat Lift", 2.1f, (t, s) =>
                P(pitch: -10f * In(t, .3f, .5f) * (1f - In(t, .65f, .9f)),
                    hatLift: 2.5f * In(t, .2f, .6f) * (1f - In(t, .6f, .8f)))),

            /// <summary>3d: brushes its hands off, done</summary>
            Clip(GhostAction.DeliveryFlourish, "3d", "Brush Off", 2.1f, (t, s) =>
                P(lift: 0.06f * (Bump(t, .15f, .25f) + Bump(t, .35f, .45f)))),

            /// <summary>4a: clamps its hat on, hunched forward, buffeted</summary>
            Clip(GhostAction.HoldYourHat, "4a", "Hat Clamp", 1.7f, (t, s) =>
                P(pitch: 8f * Hold(t, .2f, .2f), roll: 8f * Wave(t, 3))),

            /// <summary>4b: arms out like a plane, banking (left hand up at 30%, right at 50%)</summary>
            Clip(GhostAction.HoldYourHat, "4b", "Plane Arms", 2.1f, (t, s) =>
                P(roll: 20f * Window(t, .1f, .85f, .25f, .3f) * Swing(t, .3f, .4f))),

            /// <summary>4c: one fist forward like a flying superhero</summary>
            Clip(GhostAction.HoldYourHat, "4c", "Superhero", 2.0f, (t, s) =>
            {
                float k = In(t, .15f, .3f) * (1f - In(t, .8f, 1f));
                return P(pitch: 10f * k, lift: 0.2f * k);
            }),

            /// <summary>5a: both arms up, bouncing</summary>
            Clip(GhostAction.Wheee, "5a", "Arms Up", 2.1f, (t, s) =>
                P(lift: 0.15f * Bounces(t, 3))),

            /// <summary>5c: a starfish jump, arms and hands spread, squashing on landing</summary>
            Clip(GhostAction.Wheee, "5c", "Starfish", 1.7f, (t, s) =>
            {
                float up = In(t, .12f, .3f) * (1f - In(t, .6f, .85f));
                return P(lift: 0.5f * up, tall: 1f + 0.08f * up - 0.12f * Land(t), wide: 1f + 0.1f * Land(t));
            }),

            /// <summary>7a: holds the parcel overhead and sways it slowly, leaning with it (to its left at 30% and 66%, right at 48%)</summary>
            Clip(GhostAction.TreasureHold, "7a", "Parcel Overhead", 2.2f, (t, s) =>
                P(roll: -6f * Window(t, .15f, .8f, .2f, .2f) * Swing(t, .3f, .36f))).Holding(GhostProp.Parcel),

            /// <summary>7b: hugs the parcel, twisting side to side</summary>
            Clip(GhostAction.TreasureHold, "7b", "Parcel Hug", 2.5f, (t, s) =>
                P(yaw: s * 25f * Wave(Span(t, .15f, .65f), 2))).Holding(GhostProp.Parcel),

            /// <summary>7d: presents the parcel out to its left, then over to its right, turning with it</summary>
            Clip(GhostAction.TreasureHold, "7d", "Presenting", 2.5f, (t, s) =>
                P(yaw: -30f * In(t, .15f, .5f) + 60f * In(t, .5f, .8f) - 30f * In(t, .92f, 1f))).Holding(GhostProp.Parcel),

            /// <summary>8b: pops up, the hat flies off and it grabs it back</summary>
            Clip(GhostAction.JumpScare, "8b", "Hat Grab", 2.0f, (t, s) =>
                P(lift: 0.6f * In(t, 0f, .2f) * (1f - In(t, .2f, .6f)),
                    hatLift: 1.5f * In(t, 0f, .25f) * (1f - In(t, .4f, .8f)))),

            /// <summary>9a: a big stretch up, leaning back, then slumps</summary>
            Clip(GhostAction.Bored, "9a", "Big Stretch Up", 2.5f, (t, s) =>
                P(pitch: -10f * In(t, .2f, .55f) + 25f * In(t, .7f, .85f) - 15f * In(t, .85f, 1f))),

            /// <summary>9b: slumps over to its right, swaying, arms dangling</summary>
            Clip(GhostAction.Bored, "9b", "Slump Over", 2.5f, (t, s) =>
            {
                float k = In(t, .15f, .4f) * (1f - In(t, .85f, 1f));
                return P(roll: k * (25f + 4f * Mathf.Sin(6f * Mathf.PI * t)));
            }),

            /// <summary>9c: turns round to the camera and shrugs</summary>
            Clip(GhostAction.Bored, "9c", "Shrug", 2.5f, (t, s) => Round(t, s, .2f, .85f)),

            /// <summary>10a: waggles its hands by its ears, head bobbing</summary>
            Clip(GhostAction.Taunt, "10a", "Ear Waggle", 2.2f, (t, s) =>
                P(headRoll: 8f * Wave(t, 4))),

            /// <summary>10c: points back over its right shoulder, turning and leaning that way</summary>
            Clip(GhostAction.Taunt, "10c", "Point Back", 2.1f, (t, s) =>
            {
                float k = Window(t, .15f, .8f, .25f, .25f);
                return P(yaw: 20f * k, roll: 8f * k);
            }),

            /// <summary>10d: beckons "come at me", leaning in, head cocked</summary>
            Clip(GhostAction.Taunt, "10d", "Come At Me", 2.5f, (t, s) =>
            {
                float k = Window(t, .15f, .9f, .2f, .2f);
                return P(pitch: 10f * k, headRoll: 12f * k);
            }),

            /// <summary>11b: turns round to the camera and waves</summary>
            Clip(GhostAction.BigWave, "11b", "Turn And Wave", 2.5f, (t, s) => Round(t, s, .2f, .85f)),

            /// <summary>11c: reaches back for a high five with its right hand, leaning back towards it</summary>
            Clip(GhostAction.BigWave, "11c", "High Five", 1.5f, (t, s) =>
            {
                float k = Window(t, .15f, .7f, .3f, .3f);
                return P(pitch: -10f * k, roll: 10f * k);
            }),

            /// <summary>12a: twirls its hat round on one finger, high overhead</summary>
            Clip(GhostAction.HatShowOff, "12a", "Hat Twirl", 2.5f, (t, s) =>
                P(hatLift: 2.5f * In(t, .2f, .35f) * (1f - In(t, .7f, .85f)), hatSpin: Turn(720f, Span(t, .2f, .75f)))),

            /// <summary>13b: appears with a hop and a "ta da", arms up</summary>
            Clip(GhostAction.Materialise, "13b", "Ta Da", 2.5f, (t, s) =>
            {
                float hidden = t <= 0f ? 1f : 0f;
                return P(lift: 0.25f * Bump(t, .1f, .3f), fade: hidden, hatFade: hidden);
            }, true),

            /// <summary>14c: shadow boxes high punches, twisting to each (left at 10%, right at 22%, and on)</summary>
            Clip(GhostAction.WarmUp, "14c", "Shadow Box", 2.2f, (t, s) =>
                P(yaw: -12f * Window(t, .03f, .8f, .1f, .15f) * Swing(t, .1f, .24f))),

            /// <summary>15c: raises a hand like a race starter and chops it down at 36% (go), the body lunging with the chop</summary>
            Clip(GhostAction.RevBounce, "15c", "Race Starter", 2.2f, (t, s) =>
                P(pitch: 15f * In(t, .32f, .4f) * (1f - In(t, .5f, .75f)))),

            /// <summary>19a: hunches over rubbing its hands</summary>
            Clip(GhostAction.Scheme, "19a", "Hand Rub", 2.0f, (t, s) =>
                P(pitch: 12f * Window(t, .1f, .85f, .2f, .2f))),

            /// <summary>19b: turns round to the camera rubbing its hands slyly</summary>
            Clip(GhostAction.Scheme, "19b", "Sly Rub", 2.5f, (t, s) => Round(t, s, .2f, .9f)),

            /// <summary>19c: steeples its fingers: excellent</summary>
            Clip(GhostAction.Scheme, "19c", "Excellent", 2.5f, (t, s) => GhostPose.Rest),

            /// <summary>20c: clutches the parcel and pulls it away to its left, turning and leaning with it</summary>
            Clip(GhostAction.SideEye, "20c", "Parcel Clutch", 2.5f, (t, s) =>
            {
                float k = In(t, .3f, .45f) * (1f - In(t, .7f, .85f));
                return P(yaw: -25f * k, roll: -10f * k);
            }).Holding(GhostProp.Parcel),

            /// <summary>21a: a drum solo in the air, bobbing on the strokes</summary>
            Clip(GhostAction.Impatient, "21a", "Drum Solo", 2.2f, (t, s) =>
                P(lift: 0.08f * Bounces(Span(t, .1f, .9f), 12))),

            /// <summary>21b: checks the watch on its left wrist, head tilted to it</summary>
            Clip(GhostAction.Impatient, "21b", "Watch Check", 2.5f, (t, s) =>
                P(headRoll: -10f * Window(t, .15f, .75f, .25f, .25f))),

            /// <summary>21c: a big yawn, stretching back</summary>
            Clip(GhostAction.Impatient, "21c", "Big Yawn", 2.5f, (t, s) =>
            {
                float k = In(t, .1f, .45f) * (1f - In(t, .45f, .6f));
                return P(pitch: -12f * k, headPitch: -25f * k);
            }),

            /// <summary>22a: turns round with its thumbs in its ears, fingers waggling</summary>
            Clip(GhostAction.Raspberry, "22a", "Thumbs In Ears", 2.0f, (t, s) => Round(t, s, .12f, .9f)),

            /// <summary>23a: admires itself, leaning back</summary>
            Clip(GhostAction.MirrorCheck, "23a", "Admire", 2.5f, (t, s) =>
                P(pitch: -8f * Window(t, .15f, .85f, .2f, .2f))),

            /// <summary>23b: looks in a hand mirror, head tilting</summary>
            Clip(GhostAction.MirrorCheck, "23b", "Hand Mirror", 2.5f, (t, s) =>
                P(headRoll: 10f * Wave(Span(t, .3f, .75f), 2))),

            /// <summary>24a: laces its fingers overhead and pushes them out, three pops</summary>
            Clip(GhostAction.KnuckleCrack, "24a", "Lace And Push", 2.5f, (t, s) =>
                P(lift: 0.05f * (Bump(t, .35f, .45f) + Bump(t, .49f, .59f) + Bump(t, .63f, .73f)))),

            /// <summary>16a: holds the crown up overhead (the crown: GhostProps)</summary>
            Clip(GhostAction.Victory, "16a", "Crown Overhead", 2.5f, (t, s) => GhostPose.Rest).Holding(GhostProp.Crown, .15f, .85f),

            /// <summary>16b: pumps its arms in a dance, bouncing and swaying</summary>
            Clip(GhostAction.Victory, "16b", "Arm Pump Dance", 2.5f, (t, s) =>
                P(lift: 0.12f * Bounces(Span(t, .1f, .9f), 10), roll: 6f * Wave(Span(t, .1f, .9f), 5))),

            /// <summary>16c: spins round with its arms out wide</summary>
            Clip(GhostAction.Victory, "16c", "Arms Out Spin", 2.5f, (t, s) =>
                P(yaw: Turn(360f, Span(t, .15f, .85f)), lift: 0.2f * Arc(t))),

            /// <summary>16d: raises the roof, bobbing on each push</summary>
            Clip(GhostAction.Victory, "16d", "Raise The Roof", 2.5f, (t, s) =>
                P(lift: 0.1f * Bounces(Span(t, .15f, .8f), 4))),

            /// <summary>17a: pounds its fists, hopping on each</summary>
            Clip(GhostAction.Tantrum, "17a", "Fist Pound", 2.5f, (t, s) =>
                P(lift: 0.12f * Bounces(Span(t, .12f, .9f), 10))),

            /// <summary>17b: flops over backwards, arms splayed, and gets up</summary>
            Clip(GhostAction.Tantrum, "17b", "Flop Back", 2.5f, (t, s) =>
                P(pitch: -35f * In(t, .15f, .5f) * (1f - In(t, .8f, 1f)))),

            /// <summary>17c: sobs into its hands, shoulders heaving</summary>
            Clip(GhostAction.Tantrum, "17c", "Sob", 2.5f, (t, s) =>
            {
                float k = Window(t, .2f, .85f, .15f, .2f);
                return P(tall: 1f + 0.06f * k * Mathf.Sin(2f * Mathf.PI * (t - .2f) / .16f), pitch: 10f * k);
            }),

            /// <summary>25a: polishes the crown held out at its side (the crown: GhostProps)</summary>
            Clip(GhostAction.CrownPolish, "25a", "Polish", 2.5f, (t, s) => GhostPose.Rest).Holding(GhostProp.CrownLeft, .15f, .78f)
        };
    }
}
