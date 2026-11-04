using System;
using UnityEngine;

/// <summary>
/// What a match of Kings Crown, Infection or Hot Potato is doing (docs/game-modes-ideas.md): who holds the crown or the
/// potato, who is infected, who is out. The authority (this PC offline, the host online) runs it: Begin as the match
/// starts, Strike when a boosting player hits another (OrderHandler.StealFrom), Tick every frame. Other machines Show
/// the host's snapshot (OnlineModes). ModeVisuals draws it on every machine. Numbers: TimedModeRules
/// </summary>
public class ModeMatch
{
    public const int NONE = -1;

    /// <summary>The match being played, or null outside a timed mode</summary>
    public static ModeMatch Current { get; private set; }

    /// <summary>Raised on every machine when anything shown changes (a new holder, someone infected or out)</summary>
    public static event Action Changed;

    /// <summary>A player was caught, passed the potato, took the crown or went out: for the highlight reel and feats (authority only)</summary>
    public static event Action<ModeMoment> Happened;

    public readonly GameMode Mode;
    public int Players { get; private set; }
    public int CrownSeat { get; private set; } = NONE;
    public Vector3 CrownSpot { get; private set; }
    public int PotatoSeat { get; private set; } = NONE;
    public int FirstHunter { get; private set; } = NONE;
    public int Infected { get; private set; }
    public int Out { get; private set; }
    /// <summary>When (this machine's Time.time) the potato was lit and explodes</summary>
    public float FuseLit { get; private set; }
    public float FuseEnd { get; private set; }

    readonly System.Random random;
    float safeUntil;
    int safeSeat = NONE;
    float nextPotatoAt;
    readonly float[] carry = new float[Constants.MAX_PLAYERS];

    ModeMatch(GameMode mode, int players, System.Random random)
    {
        Mode = mode;
        Players = players;
        this.random = random;
    }

    /// <summary>Authority, as a timed match begins: the crown lies at crownSpot; the first hunter is picked; the potato comes after a delay</summary>
    public static ModeMatch Begin(GameMode mode, int players, Vector3 crownSpot, float now, System.Random random = null)
    {
        if (!TimedModeRules.IsTimed(mode))
        {
            End();
            return null;
        }

        ModeMatch match = new ModeMatch(mode, players, random ?? new System.Random());
        match.CrownSpot = crownSpot;
        if (mode == GameMode.Infection)
        {
            match.FirstHunter = match.RandomSeat(players);
            match.Infected = match.FirstHunter == NONE ? 0 : 1 << match.FirstHunter;
        }
        match.nextPotatoAt = now + TimedModeRules.POTATO_DELAY;
        Current = match;
        Changed?.Invoke();
        return match;
    }

    /// <summary>No timed match any more (the menus, a rematch, another mode)</summary>
    public static void End()
    {
        if (Current == null)
            return;
        Current = null;
        Changed?.Invoke();
    }

    /// <summary>Tests: the lit fuse runs out at this moment</summary>
    internal void BurnFuse(float now) { FuseEnd = now; }

    public bool IsInfected(int seat) => seat >= 0 && (Infected & 1 << seat) != 0;
    public bool IsOut(int seat) => seat >= 0 && (Out & 1 << seat) != 0;
    public bool IsOver => TimedModeRules.EndsEarly(Mode, Players, Infected, Out);

    /// <summary>
    /// A player left mid-match: they hold nothing and count as gone. The potato waits POTATO_DELAY before it's lit again;
    /// in Infection a new hunter is picked when the last one left, so the thieves can still be caught
    /// </summary>
    public void Leave(int seat, float now)
    {
        if (seat < 0)
            return;
        Players &= ~(1 << seat);
        Infected &= ~(1 << seat);
        if (CrownSeat == seat)
            CrownSeat = NONE; // back to its spot on the road
        if (PotatoSeat == seat)
        {
            PotatoSeat = NONE;
            nextPotatoAt = now + TimedModeRules.POTATO_DELAY;
        }
        if (Mode == GameMode.Infection && Infected == 0 && TimedModeRules.Count(Players) > 1)
            Infected = 1 << RandomSeat(Players);
        Changed?.Invoke();
    }

    /// <summary>
    /// Authority: a boosting player hit another (who wasn't boosting). True when the mode used the hit (nothing else
    /// changes hands): the crown or potato moves, or a thief is caught
    /// </summary>
    public bool Strike(int attacker, int victim, float now)
    {
        if (attacker < 0 || victim < 0 || attacker == victim || IsOut(attacker) || IsOut(victim))
            return true;

        switch (Mode)
        {
            case GameMode.KingsCrown:
                if (CrownSeat == victim && !(safeSeat == victim && now < safeUntil))
                    Give(attacker, now, ModeMomentKind.CrownTaken, victim);
                return true;
            case GameMode.Infection:
                if (IsInfected(attacker) && !IsInfected(victim))
                {
                    Infected |= 1 << victim;
                    Happened?.Invoke(new ModeMoment(ModeMomentKind.Infected, attacker, victim, TimedModeRules.Count(Infected) / (float)Mathf.Max(1, TimedModeRules.Count(Players))));
                    if (IsOver)
                        Happened?.Invoke(new ModeMoment(ModeMomentKind.LastCaught, attacker, victim));
                    Changed?.Invoke();
                }
                return true;
            case GameMode.HotPotato:
                if (PotatoSeat == attacker && !(safeSeat == attacker && now < safeUntil))
                    Give(victim, now, ModeMomentKind.PotatoPassed, attacker);
                return true;
        }
        return false;
    }

    void Give(int seat, float now, ModeMomentKind kind, int from)
    {
        if (Mode == GameMode.KingsCrown)
            CrownSeat = seat;
        else
            PotatoSeat = seat;
        safeSeat = seat;
        safeUntil = now + (Mode == GameMode.KingsCrown ? TimedModeRules.CROWN_SAFE_SECONDS : TimedModeRules.NO_TAG_BACKS);
        float strength = kind == ModeMomentKind.PotatoPassed ? TimedModeRules.FuseProgress(now, FuseLit, FuseEnd) : 0f;
        Happened?.Invoke(new ModeMoment(kind, kind == ModeMomentKind.PotatoPassed ? from : seat, kind == ModeMomentKind.PotatoPassed ? seat : from, strength));
        Changed?.Invoke();
    }

    /// <summary>
    /// Authority, every frame of the match: picks up the crown, lights and explodes the potato, and pays points through
    /// addScore(seat, points). ballOf(seat) is where a seat's ball is (null: not in the match)
    /// </summary>
    public void Tick(float now, float deltaTime, Func<int, Vector3?> ballOf, Action<int, int> addScore)
    {
        switch (Mode)
        {
            case GameMode.KingsCrown:
                if (CrownSeat == NONE)
                    PickUpCrown(now, ballOf);
                if (CrownSeat != NONE)
                    Pay(CrownSeat, TimedModeRules.CROWN_POINTS_PER_SECOND, deltaTime, addScore);
                break;
            case GameMode.Infection:
                foreach (int seat in TimedModeRules.Seats(Players & ~Infected))
                    Pay(seat, TimedModeRules.THIEF_POINTS_PER_SECOND, deltaTime, addScore);
                break;
            case GameMode.HotPotato:
                if (PotatoSeat == NONE && now >= nextPotatoAt && !IsOver)
                    Light(now);
                else if (PotatoSeat != NONE && now >= FuseEnd)
                    Explode(now);
                foreach (int seat in TimedModeRules.Seats(Players & ~Out))
                    Pay(seat, TimedModeRules.SURVIVOR_POINTS_PER_SECOND, deltaTime, addScore);
                break;
        }
    }

    /// <summary>Infection: a catch's points go to the hunter (called by whoever pays scores, with the moment)</summary>
    public static int CatchPoints(ModeMoment moment) => moment.Kind == ModeMomentKind.Infected ? TimedModeRules.CATCH_POINTS : 0;

    void PickUpCrown(float now, Func<int, Vector3?> ballOf)
    {
        float best = TimedModeRules.CROWN_PICKUP_REACH;
        int taker = NONE;
        foreach (int seat in TimedModeRules.Seats(Players))
        {
            Vector3? ball = ballOf(seat);
            if (ball == null)
                continue;
            float distance = Vector3.Distance(ball.Value, CrownSpot);
            if (distance <= best)
            {
                best = distance;
                taker = seat;
            }
        }
        if (taker != NONE)
            Give(taker, now, ModeMomentKind.CrownTaken, NONE);
    }

    void Light(float now)
    {
        int seat = RandomSeat(Players & ~Out);
        if (seat == NONE)
            return;
        PotatoSeat = seat;
        FuseLit = now;
        FuseEnd = now + TimedModeRules.FUSE_MIN + (float)random.NextDouble() * (TimedModeRules.FUSE_MAX - TimedModeRules.FUSE_MIN);
        safeSeat = NONE;
        Changed?.Invoke();
    }

    void Explode(float now)
    {
        int seat = PotatoSeat;
        Out |= 1 << seat;
        PotatoSeat = NONE;
        nextPotatoAt = now + TimedModeRules.POTATO_DELAY;
        Happened?.Invoke(new ModeMoment(ModeMomentKind.PotatoExploded, seat, NONE));
        if (IsOver)
        {
            foreach (int winner in TimedModeRules.Seats(Players & ~Out))
                Happened?.Invoke(new ModeMoment(ModeMomentKind.LastStanding, winner, seat));
        }
        Changed?.Invoke();
    }

    void Pay(int seat, float perSecond, float deltaTime, Action<int, int> addScore)
    {
        carry[seat] += perSecond * deltaTime;
        int points = Mathf.FloorToInt(carry[seat]);
        if (points <= 0)
            return;
        carry[seat] -= points;
        addScore(seat, points);
    }

    int RandomSeat(int mask)
    {
        int count = TimedModeRules.Count(mask);
        if (count == 0)
            return NONE;
        int pick = random.Next(count);
        foreach (int seat in TimedModeRules.Seats(mask))
            if (pick-- == 0)
                return seat;
        return NONE;
    }

    /// <summary>The state other machines need, packed for the network (OnlineModes)</summary>
    public ModeSnapshot Snapshot(float now)
    {
        return new ModeSnapshot(Mode, Players, CrownSeat, PotatoSeat, FirstHunter, Infected, Out, CrownSpot,
            PotatoSeat == NONE ? 0f : FuseEnd - now, PotatoSeat == NONE ? 0f : FuseEnd - FuseLit);
    }

    /// <summary>Another machine: shows the host's snapshot (this machine's clock for the fuse)</summary>
    public static void Show(ModeSnapshot snapshot, float now)
    {
        if (!TimedModeRules.IsTimed(snapshot.Mode))
        {
            End();
            return;
        }
        ModeMatch match = Current != null && Current.Mode == snapshot.Mode ? Current : new ModeMatch(snapshot.Mode, snapshot.Players, new System.Random());
        match.Players = snapshot.Players;
        match.CrownSeat = snapshot.CrownSeat;
        match.PotatoSeat = snapshot.PotatoSeat;
        match.FirstHunter = snapshot.FirstHunter;
        match.Infected = snapshot.Infected;
        match.Out = snapshot.Out;
        match.CrownSpot = snapshot.CrownSpot;
        match.FuseEnd = now + snapshot.FuseLeft;
        match.FuseLit = match.FuseEnd - snapshot.FuseLength;
        Current = match;
        Changed?.Invoke();
    }
}

public enum ModeMomentKind { CrownTaken, Infected, LastCaught, PotatoPassed, PotatoExploded, LastStanding }

/// <summary>Something a timed mode's players did: Seat did it (to Other, or NONE)</summary>
public readonly struct ModeMoment
{
    public readonly ModeMomentKind Kind;
    public readonly int Seat;
    public readonly int Other;
    /// <summary>0 to 1: how far Infection has spread after a catch; how close a passed potato was to exploding</summary>
    public readonly float Strength;

    public ModeMoment(ModeMomentKind kind, int seat, int other, float strength = 0f)
    {
        Kind = kind;
        Seat = seat;
        Other = other;
        Strength = strength;
    }

    public override string ToString() => Kind + " " + Seat + (Other >= 0 ? " → " + Other : "");
}
