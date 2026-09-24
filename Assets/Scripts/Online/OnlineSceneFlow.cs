using System;
using UnityEngine;

/// <summary>
/// The scene flow while online (OnlineGame sets it as SceneFlow.Current). Starting a match online, with every machine
/// loading the host's scenes, comes with roadmap Task 3.4 (Phase 3C): until then the ready-up countdown ends with a
/// warning and nothing loads. Going back to the menu uses the local loader
/// </summary>
public class OnlineSceneFlow : ISceneFlow
{
    /// <summary>Logged instead of loading</summary>
    public const string NOT_YET = "Online: starting an online match isn't in yet (roadmap Task 3.4). Leave the session to play a local match.";

    readonly ISceneFlow local;

    /// <param name="localFlow">The local loader, for going back to the menu</param>
    public OnlineSceneFlow(ISceneFlow localFlow)
    {
        local = localFlow;
    }

    public void LoadGameScene()
    {
        Debug.LogWarning(NOT_YET);
    }

    public void LoadFinalOrderScene()
    {
        Debug.LogWarning(NOT_YET);
    }

    public void ReturnToMenu()
    {
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
}
