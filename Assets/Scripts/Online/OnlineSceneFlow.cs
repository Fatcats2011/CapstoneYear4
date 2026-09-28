using System;
using System.Collections.Generic;

/// <summary>
/// An online session's side of the scene flow (OnlineMatch): the host's requests to every client, and clients' reports
/// to the host
/// </summary>
public interface IMatchLink
{
    /// <summary>Whether this machine hosts</summary>
    bool IsHost { get; }

    /// <summary>Clients: the host wants a match scene loaded, held behind the loading screen</summary>
    event Action<MatchScene> LoadRequested;

    /// <summary>Clients: every machine has the scene loaded: show it</summary>
    event Action<MatchScene> ShowRequested;

    /// <summary>Clients: the host is taking everyone back to the menu</summary>
    event Action ReturnRequested;

    /// <summary>Host: a client has a scene loaded (its Netcode client id)</summary>
    event Action<ulong, MatchScene> MachineLoaded;

    /// <summary>Host: asks every client to load a match scene</summary>
    void RequestLoad(MatchScene scene);

    /// <summary>Host: asks every client to show the scene it loaded</summary>
    void RequestShow(MatchScene scene);

    /// <summary>Host: takes every client back to the menu</summary>
    void RequestReturn();

    /// <summary>Client: tells the host it has a scene loaded</summary>
    void ReportLoaded(MatchScene scene);
}

/// <summary>
/// The scene flow while online (OnlineGame sets it as SceneFlow.Current). See docs/online.md.
/// - The host starts every load. Each machine loads the scene behind its loading screen (the local loader's held loads,
///   IMatchLoader), and the host shows it on every machine once all of them have it, or have left. There's no "press A".
/// - The host takes everyone back to the menu. A client going back on its own leaves the session first: it can't come
///   back into the host's match.
/// - While a client's new scene comes up (Changing), OnlineGame holds the host's states until SceneChanged.
/// - Netcode's scene management stays off.
/// </summary>
public class OnlineSceneFlow : ISceneFlow
{
    readonly ISceneFlow local;
    readonly IMatchLoader loader;
    IMatchLink link;
    Func<IEnumerable<ulong>> machines; // host: the machines in the session now, the host included
    ulong self;                       // this machine's Netcode client id
    Action leave;                     // client: leaves the session
    LoadRound round;                  // host: the load in progress
    MatchScene loading;               // client: the scene the host asked for

    /// <param name="localFlow">The local loader: it takes this machine back to the menu</param>
    /// <param name="matchLoader">The local loader's held loads (null loads nothing, as in tests without the menu scene)</param>
    public OnlineSceneFlow(ISceneFlow localFlow, IMatchLoader matchLoader)
    {
        local = localFlow;
        loader = matchLoader;
    }

    /// <summary>Client: the host showed a scene or took everyone back, and that scene isn't up here yet</summary>
    public bool Changing { get; private set; }

    /// <summary>Client: the scene the host showed (or the menu) is up here</summary>
    public event Action SceneChanged;

    /// <summary>
    /// Follows a session's match: the host's requests on a client, clients' reports on the host
    /// </summary>
    /// <param name="sessionMachines">Host: the machines in the session now, the host included</param>
    /// <param name="selfId">This machine's Netcode client id</param>
    /// <param name="leaveSession">Client: leaves the session</param>
    public void Link(IMatchLink matchLink, Func<IEnumerable<ulong>> sessionMachines, ulong selfId, Action leaveSession)
    {
        Unlink();
        link = matchLink;
        machines = sessionMachines;
        self = selfId;
        leave = leaveSession;

        link.LoadRequested += LoadForHost;
        link.ShowRequested += ShowForHost;
        link.ReturnRequested += ReturnForHost;
        link.MachineLoaded += MachineHasScene;
        if (loader != null)
        {
            loader.HeldSceneReady += HeldSceneReady;
            loader.SceneUp += SceneUp;
        }
    }

    /// <summary>Stops following the match (the session ended)</summary>
    public void Unlink()
    {
        if (link == null)
            return;

        link.LoadRequested -= LoadForHost;
        link.ShowRequested -= ShowForHost;
        link.ReturnRequested -= ReturnForHost;
        link.MachineLoaded -= MachineHasScene;
        if (loader != null)
        {
            loader.HeldSceneReady -= HeldSceneReady;
            loader.SceneUp -= SceneUp;
        }
        link = null;
        round = null;
        Changing = false;
    }

    bool IsHost { get { return link != null && link.IsHost; } }

    public void LoadGameScene()
    {
        Begin(MatchScene.Game);
    }

    public void LoadFinalOrderScene()
    {
        Begin(MatchScene.FinalOrder);
    }

    public void ReturnToMenu()
    {
        if (IsHost)
            link.RequestReturn();
        else if (link != null && leave != null)
            leave();

        if (local != null)
            local.ReturnToMenu();
    }

    public event Action OnReturnToMenu
    {
        add
        {
            if (local != null)
                local.OnReturnToMenu += value;
        }
        remove
        {
            if (local != null)
                local.OnReturnToMenu -= value;
        }
    }

    /// <summary>Online there's no "press A" on the loading screen</summary>
    public bool WaitingForConfirm { get { return false; } }

    public void ConfirmLoad()
    {
    }

    /// <summary>Host: a machine left the session: a load in progress stops waiting for it</summary>
    public void MachineLeft(ulong machine)
    {
        if (round == null)
            return;

        round.Left(machine);
        ShowIfEveryoneHasIt();
    }

    // Host: a match scene loads on every machine. A client's own countdown or clock starts nothing: the host decides
    void Begin(MatchScene scene)
    {
        if (!IsHost || loader == null)
            return;

        round = new LoadRound(scene, machines());
        link.RequestLoad(scene);
        loader.LoadHeld(scene);
    }

    // This machine has the scene loaded: the host counts itself, a client tells the host
    void HeldSceneReady()
    {
        if (IsHost)
        {
            if (round == null)
                return;
            round.Loaded(self);
            ShowIfEveryoneHasIt();
        }
        else if (link != null)
            link.ReportLoaded(loading);
    }

    // Host: a client has the scene loaded
    void MachineHasScene(ulong machine, MatchScene scene)
    {
        if (round == null || scene != round.Scene)
            return;

        round.Loaded(machine);
        ShowIfEveryoneHasIt();
    }

    void ShowIfEveryoneHasIt()
    {
        if (round == null || !round.Ready)
            return;

        MatchScene scene = round.Scene;
        round = null;
        link.RequestShow(scene);
        loader.ShowHeld();
    }

    // Client: the host asked for a scene
    void LoadForHost(MatchScene scene)
    {
        if (loader == null)
            return;

        loading = scene;
        loader.LoadHeld(scene);
    }

    // Client: every machine has it: show it
    void ShowForHost(MatchScene scene)
    {
        if (loader == null)
            return;

        Changing = true;
        loader.ShowHeld();
    }

    // Client: the host is taking everyone back to the menu
    void ReturnForHost()
    {
        Changing = true;
        if (local != null)
            local.ReturnToMenu();
    }

    void SceneUp()
    {
        if (!Changing)
            return;

        Changing = false;
        SceneChanged?.Invoke();
    }
}
