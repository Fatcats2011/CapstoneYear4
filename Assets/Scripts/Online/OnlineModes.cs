using UnityEngine;

/// <summary>
/// Kings Crown, Infection and Hot Potato online. The host runs ModeMatch (ModeReferee) and shares every change
/// (OnlineMatch.ShareMode, a whole snapshot each time); a client shows the latest snapshot once its version moves on,
/// counting the fuse down on its own clock. Scores travel as always (OnlineOrders). OnlineGame adds it next to OnlineCues
/// </summary>
public class OnlineModes : MonoBehaviour
{
    OnlineSession session;
    int shownVersion;

    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        ModeMatch.Changed += Share;
    }

    void OnDestroy()
    {
        ModeMatch.Changed -= Share;
    }

    // Host: every change goes out; no match (the menus, the results) goes out as Standard, so a client's ends too
    void Share()
    {
        OnlineMatch match = session != null ? session.Match : null;
        if (match == null || !match.IsServer)
            return;
        ModeMatch current = ModeMatch.Current;
        match.ShareMode(current != null ? current.Snapshot(Time.time) : default);
    }

    void Update()
    {
        OnlineMatch match = session != null ? session.Match : null;
        if (match == null || match.IsServer || match.ModeVersion == shownVersion)
            return;
        shownVersion = match.ModeVersion;
        ModeMatch.Show(match.ModeShared, Time.time);
    }
}
