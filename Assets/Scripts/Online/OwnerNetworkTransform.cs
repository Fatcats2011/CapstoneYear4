using Unity.Netcode.Components;

/// <summary>
/// A NetworkTransform its owner moves (Netcode's own is moved by the host). Online, each machine moves its own scooter's
/// proxies (OnlineScooter), and the other machines follow them, smoothed
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
