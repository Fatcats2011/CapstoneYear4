# Dead on Arrival's changes to this package

This is a copy of `com.community.netcode.transport.steamnetworkingsockets` from
`Unity-Technologies/multiplayer-community-contributions` at commit `d862504b148f6c3a31763797900eb5a54d4625a5`
(MIT, see `LICENSE.md`), embedded so it can be patched (Phase 4C, `docs/online-safety.md`). Every change is marked
`// DoA` in `Runtime/SteamNetworkingSocketsTransport.cs`:

1. **Only accepted peers connect.** `public Func<ulong, bool> AcceptPeer` is asked, with the joiner's Steam ID, before a
   connection is accepted on the server. A refused peer is closed ("Not in this lobby") before Netcode sees them, and logged
   once per Steam ID (the ID itself is never logged). The game sets it to "is this Steam user in my lobby?"
   (`OnlineSession.AcceptsPeer`, `SteamPeerRules.Accepts`).
2. **Empty and huge messages are dropped.** A message of 1 byte or less (only the channel byte) used to throw
   (`new byte[m_cbSize - 1]`) and leak the message; one over 256 KB is now dropped too. `Deliverable` here must match
   `SteamPeerRules.Deliverable` in the game.
3. **Steam's message is always released**, in a `finally`.
4. **A peer not accepted yet waits, briefly.** The decision is `ConnectionGate` (`Runtime/ConnectionGate.cs`, a new
   file). A connection the game doesn't accept yet waits up to 3 s (`ConnectionGate.GRACE`), asked again each poll,
   since the host's copy of the lobby can lag a friend's join; then it's refused.
5. **One connection per Steam ID.** A second connection from a Steam ID that's already connected is refused, so one
   player can't take several seats.
6. **A kicked peer stays out (Phase 5A).** With `BanOnNextDisconnect` set (the game sets it in `OnlineSession.Kick`),
   `DisconnectRemoteClient` bans that Steam ID in `ConnectionGate` for the session. Steam lobbies have no kick, so the
   player is still a lobby member, but every new connection from them is refused. `ClearBans` forgets the bans (each new
   hosting session).
7. **The kick reason arrives (Phase 5A).** `DisconnectRemoteClient` closes with linger on, so the reason message Netcode
   queued just before is delivered before the connection closes.

The game also sets the public `options` to relay-only (`SteamRelay.Options()`), so no peer learns another's IP; that
needs no change here.

**Updating:** copy the new upstream package over this folder, then re-apply the changes above (search for `DoA`) and keep `Runtime/ConnectionGate.cs`.
