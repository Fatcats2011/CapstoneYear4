using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools → Dead on Arrival → Online: hosts or joins a session between two editors on this computer (ParrelSync) before
/// the online menus exist, and can make the connection bad on purpose. Play Mode only. See docs/online.md
/// </summary>
public static class OnlineTestMenu
{
    const string MENU = "Tools/Dead on Arrival/Online/";
    const string BAD_CONNECTION_ITEM = MENU + "Bad Connection (150 ms, 1% Loss)";
    const string BAD_CONNECTION_KEY = "DoA.Online.BadConnection";
    const string THIS_COMPUTER = "127.0.0.1";

    /// <summary>Delay and packet loss the Bad Connection toggle adds (the roadmap's lag test)</summary>
    internal const int BAD_DELAY_MS = 150;
    internal const int BAD_LOSS_PERCENT = 1;

    static OnlineSession session;

    /// <summary>Whether the next Host or Join simulates a bad connection (remembered until the editor closes)</summary>
    internal static bool BadConnection
    {
        get { return SessionState.GetBool(BAD_CONNECTION_KEY, false); }
        set { SessionState.SetBool(BAD_CONNECTION_KEY, value); }
    }

    [MenuItem(MENU + "Host", false, 1)]
    static void Host()
    {
        if (!Prepare().HostDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT))
            Debug.LogWarning("Online: couldn't host. Is another editor hosting on port " + OnlineSession.DIRECT_PORT + "?");
    }

    [MenuItem(MENU + "Join This Computer", false, 2)]
    static void Join()
    {
        Prepare().JoinDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT);
    }

    [MenuItem(MENU + "Leave", false, 3)]
    static void Leave()
    {
        session.Leave();
    }

    [MenuItem(MENU + "Host", true)]
    [MenuItem(MENU + "Join This Computer", true)]
    static bool CanStart()
    {
        return Application.isPlaying && (session == null || !session.IsRunning);
    }

    [MenuItem(MENU + "Leave", true)]
    static bool CanLeave()
    {
        return Application.isPlaying && session != null && session.IsRunning;
    }

    [MenuItem(BAD_CONNECTION_ITEM, false, 20)]
    static void ToggleBadConnection()
    {
        BadConnection = !BadConnection;
    }

    [MenuItem(BAD_CONNECTION_ITEM, true)]
    static bool ShowBadConnection()
    {
        Menu.SetChecked(BAD_CONNECTION_ITEM, BadConnection);
        return true;
    }

    /// <summary>
    /// The session the menu uses (made on first use), set up with or without a bad connection for its next start
    /// </summary>
    internal static OnlineSession Prepare()
    {
        if (session == null)
            session = OnlineSession.Create(Application.version);

        bool bad = BadConnection;
        session.Direct.SetDebugSimulatorParameters(bad ? BAD_DELAY_MS : 0, 0, bad ? BAD_LOSS_PERCENT : 0);
        return session;
    }
}
