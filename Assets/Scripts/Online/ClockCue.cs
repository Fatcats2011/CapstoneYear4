/// <summary>
/// The host's match clock's one-shots, online: a new wave's bells, and time up at the end of the main game (the whistle,
/// and every scooter stops). The host's clock rings them, and every client plays them as its own (OnlineCues,
/// OrderManager.FollowHostClockCue)
/// </summary>
public enum ClockCue : byte { WaveBells, TimeUp }
