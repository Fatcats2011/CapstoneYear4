using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// A NetworkTransform its owner moves (Netcode's own is moved by the host). Online, each machine moves its own scooter's
/// proxies (OnlineScooter), and the other machines follow them, smoothed. On those other machines a pose that isn't numbers
/// (a modified game's) isn't applied: the proxy holds its last good pose until good ones return (PoseHold)
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    readonly PoseHold hold = new PoseHold();

    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

    protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
    {
        base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
        hold.Received(newState.GetPosition(), newState.GetRotation(), Time.realtimeSinceStartup);
    }

    protected override void Update()
    {
        // Netcode applies (and interpolates towards) the latest state here: a bad one, and the frames that blend from it,
        // are skipped
        if (!hold.Following(Time.realtimeSinceStartup))
            return;

        base.Update();
    }
}
