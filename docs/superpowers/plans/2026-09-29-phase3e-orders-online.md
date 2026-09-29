# Phase 3E — Orders Online Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Online players share one set of orders. Every machine sees the same orders appear, get picked up by whichever scooter reaches them, ride on that scooter, get delivered or dropped, and score the same points. The golden round's growing value shows everywhere. Local split-screen stays identical.

**Architecture:**
- **One ID per order, the same on every machine.** `OrderBook` keys each scene order by its scene, value, pickup point and dropoff point, and finds it by that key.
- **The host decides; every machine replays.** The host runs the orders exactly as a local match does:
  - Spawns, pickups and deliveries in its own physics, for every scooter. Another machine's scooter now counts in a beacon there.
  - Each change to an order (`OrderChange`: spawn, pickup, delivery, drop, erase) goes out over `OnlineMatch`, in order, with the game states.
  - Each client replays it through the same game code (`AddOrder`, `DeliverOrder`, …). So its orders, beacons, compass markers and its own scooter's golden-order slowdown come out as in a local match.
- **`OrderSync` holds the rules.** Orders change on the host, offline, or on a client only while it replays a host change. Only the outermost change goes out: what a change does on the way (a delivery erasing the golden order) is part of it.
- **A client's player falling in water** asks the host to drop their orders. The host picks how high they fly, then drops them for everyone.
- **Scores and the golden value are the host's.** Scores travel on each `OnlinePlayer`, the golden order's value on `OnlineMatch`.
- **`OnlineOrders`** is the game's glue (added by `OnlineGame`, like `OnlineDriving`).
- **Scene changes:** a client holds the host's order changes, like its states, while its new scene comes up (`HostQueue`).

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (ClientRpc/ServerRpc, `NetworkVariable`, `INetworkSerializable`) · DOTween · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- **Task 3.5 bullets 1–3:** host-owned order state (state, holder, value); only the host runs pickups, deliveries and spawning while clients show them; the host picks drop heights and clients get the landing spot. Rulings 1–4 say how.
- **Task 3.5 bullet 4 (the tutorial online) is Phase 3F** (Ruling 6).
- **Task 3.3 bullet 1's deferred score** (Ruling 5).
- **Task 3.8 bullet 1, in part:** a leaving player's orders go back to the pool (Ruling 7).

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**; Steamworks.NET stays **2025.164.1** (a version bump is a Steam API change: it goes to `main`, after asking).
- **Players:** online is one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before:**
  - `LocalMatchSmokeTest` passes after every task.
  - Offline, `OrderSync` never raises anything, and every order rule runs as today.
- **Online rules:**
  - No mid-match joining (`JoinRules`).
  - The host leaving ends the match (`OnlineSession.HOST_LEFT`).
  - Netcode's scene management stays off (Phase 3C).
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs`.
- **The user may have Unity editors open on the real project** (their editor plus a ParrelSync clone that shares `Assets/`):
  - Never edit an existing scene, prefab or ProjectSettings file.
  - Everything in this plan is code.
- **No git commits:** your human partner commits and pushes.
- **Meta files:** every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`.
- **Line endings:**
  - Files marked `i/crlf` in `git ls-files --eol` must stay CRLF, so edit them with the Edit tool only. In this plan: `Order.cs` and `PlayerInstantiate.cs`.
  - Never use `sed -i` on scripts.
  - New files may be LF.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]`. They run in the mirror (`../_doa_test_mirror/CapstoneYear4`).
  - Unity takes 2–3 minutes to start. A network test that loads the game scene takes a minute or two, so run long ones in the background.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
  - Check `ListAgents` before a long run: another local Claude session could share the mirror.
- **Play Mode tests:**
  - A test resumes in a reloaded script domain after `yield return new EnterPlayMode()`.
  - Never write a lambda that captures a local of the test method: after the reload, **even assigning** such a local throws a bare `NullReferenceException`. Use static helper methods, and recorder classes whose methods are subscribed as method groups.
  - Wait by time, never by frame count.
  - Two-machine tests call `log.MachinesLeave()` before the first machine leaves (`LogCollector.CLOSED_PORT`).
- **The tests can't see `internal` members** (game code is `Assembly-CSharp`, tests are `Assembly-CSharp-Editor`). What tests call is `public`; private state is read through `Reflect`.
- **Branch:** `steam-phase1a`. Phase 3D should be committed before this starts, so each phase stays one commit.

## Rulings (decided while planning)

1. **No NetworkObject per order** (roadmap bullet 1 says one). One stream of order changes on `OnlineMatch`, keyed by `OrderBook`, does the job:
   - Scene-placed NetworkObjects need Netcode's scene management, which stays off (Phase 3C), or a manual spawn after every load.
   - Orders are reparented onto scooters and onto the order manager, which lives under `GameManager` (DontDestroyOnLoad). Netcode doesn't allow that for spawned objects.
   - Cost if wrong: none for players. A Netcode-native version would replace this layer only.
2. **The key is the order's scene + value + pickup point + dropoff point** (FNV-1a over their exact bits).
   - A 2026-09-29 probe of both match scenes found every order's pickup and dropoff distinct: 28 orders in the game scene, 1 in the golden round.
   - The two scenes' golden orders share both points, so the scene is part of the key.
   - Hierarchy paths were rejected: a DontDestroyOnLoad `Awake` can shift root indices mid-load.
   - Cost if wrong: a scene edit that gives two orders the same points. Then `OrderBook` logs an error, and `OrderBookSceneTests` fails.
3. **The host decides every scooter's pickups and deliveries in its own physics** (roadmap bullet 2).
   - Another machine's scooter's ball now counts in a beacon, which only acts on the host.
   - The client sees its pickup after the host saw its scooter there: about a round trip plus Netcode's smoothing.
   - Clients replay each change through the same game code, so everything a pickup does happens on every machine: the beacon, compass, arrow, the rider's golden-order slowdown and the boost recharge.
   - Cost if wrong: on a bad connection pickups feel late. A client-side request with a host check (as Task 3.6 does for steals) would fix that.
4. **Drops:** the host picks each order's flight height (`Order.DropHeight`); whoever drops picks the landing spots (roadmap bullet 3).
   - A client's respawn asks the host (`OnlineMatch.AskDrop`) with its respawn point's order spots.
   - The host only drops a seat's orders for that seat's own machine.
   - Which respawn point to use stays each owner's choice until Task 3.6.
   - Cost if wrong: none.
5. **Scores are the host's.**
   - They travel on each `OnlinePlayer` (`Score`, host-written). A client never changes a score itself.
   - The golden order's growing value travels on `OnlineMatch` (`GoldenValue`).
   - Cost if wrong: a score can show a tick after its delivery.
6. **The tutorial stays skipped online: it's Phase 3F** (roadmap bullet 4).
   - It adds the tutorial orders, the cardboard cutouts' steals, and a finish that counts every machine's players.
   - Orders must work first.
   - Cost if wrong: online matches keep starting in the city.
7. **A player whose machine leaves mid-match:** their orders go back to the pool.
   - The host erases them, and every machine hears it.
   - Every machine first takes them off the scooter being destroyed, so the objects survive.
   - Pulled in from Task 3.8: client pickups make it reachable, and a destroyed order would later break the host's spawner.
   - Cost if wrong: none.
8. **Steals and clashes stay off online (Task 3.6).** A known limit of this phase: the golden round has no stealing online yet.

## Review Focus

- **A client's own scooter in a beacon** → nothing happens there until the host says so; never a pickup on the client alone. (Task 6 `Joining_…`: "picks up nothing by itself".)
- **A drop request for someone else's seat** (a buggy or hostile client) → ignored. (Task 5 `Hosting_…`.)
- **The golden order's first change reaching a client whose golden-round scene is still coming up** → it waits, then shows. (Task 6 `HostQueueTests`; Task 3 `InitOrder_OnAClient_WaitsForTheHost` keeps the client's own `FinalOrder.Start` from spawning it first.)
- **A delivery replayed on a client** → the score stays the host's, never counted twice. (Task 6 `Joining_…`: "a delivery doesn't add to them here".)
- **A client holding orders when the host takes everyone back to the menu, and host changes that arrive after an order's scene went** → the orders go with the game scene; none rides into the menu on the player's scooter. A late change for an order this machine no longer has is ignored, with no error. (Task 3 `ReturnHome_…`; Task 6 `Joining_…`, its last two steps.)

## File Structure

- Create `Assets/Scripts/World/OrderBook.cs`: order keys, and the orders of the loaded scenes by key.
- Create `Assets/Scripts/Online/OrderChange.cs`: one change to an order, as it travels.
- Create `Assets/Scripts/Online/OrderSync.cs`: who may change orders, and the host's changes going out.
- Create `Assets/Scripts/Online/OnlineOrders.cs`: the session's orders, scores and golden value on this machine.
- Create `Assets/Scripts/Online/HostQueue.cs`: a client's host messages, held while its scene changes.
- Modify `Assets/Scripts/World/Order.cs` (`i/crlf`): key, points, gates and changes, drop height, `FinishMoves`, `ReturnHome`.
- Modify `Assets/Scripts/Player/OrderHandler.cs`: gates and changes, `DropHeld`, `ReleaseOrders`, `ShowHostScore`, host-only scores.
- Modify `Assets/Scripts/World/OrderBeacon.cs`: every scooter counts on the host; holders without a compass.
- Modify `Assets/Scripts/Online/OnlineMatch.cs`: order changes, drop requests, the golden value.
- Modify `Assets/Scripts/Online/OnlinePlayer.cs`: the score.
- Modify `Assets/Scripts/Online/OnlineGame.cs`: adds `OnlineOrders`; the host queue for states and order changes.
- Modify `Assets/Scripts/Player/PlayerInstantiate.cs` (`i/crlf`): a leaving player's orders.
- Tests, all under `Assets/Tests/Editor/`:
  - Create `OrderBookTests`, `OrderBookSceneTests`, `OrderChangeTests`, `OrderSyncTests`, `OrderRulesTests` and `HostQueueTests`.
  - Create the network tests `OrderMessagesNetworkTests` (port 7798) and `OnlineOrdersNetworkTests` (port 7799).
  - Modify `RemoteScooterRulesTests`.
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: Order keys

**Files:**
- Create: `Assets/Scripts/World/OrderBook.cs`
- Modify: `Assets/Scripts/World/Order.cs` (`i/crlf`: Edit tool only)
- Test: `Assets/Tests/Editor/OrderBookTests.cs`, `Assets/Tests/Editor/OrderBookSceneTests.cs`

**Interfaces:**
- Consumes: `Constants.OrderValue`.
- Produces:

```csharp
public static class OrderBook
{
    public const int NONE = 0;                                     // no order; never a key
    public static int KeyOf(string scene, Constants.OrderValue value, Vector3 pickup, Vector3 dropoff);
    public static int KeyOf(Order order);                          // its scene's name, value and PickupPoint/DropoffPoint positions (Vector3.zero when unset)
    public static void Add(Order order);                           // under order.Key
    public static void Remove(Order order);                        // only when it's the one under its key
    public static Order Find(int key);                             // null when none, or it was destroyed
    public static void Clear();                                    // tests
}
// Order: public int Key (field `key`, set in Awake), public Transform PickupPoint, public Transform DropoffPoint
```

- [ ] **Step 1: Write the failing tests** (`namespace DoA.Tests`; `OrderBookTests` uses `TestObjects`, and `TearDown` calls `OrderBook.Clear()` then `objects.DestroyAll()`)

```csharp
Order OrderWithKey(int key) { Order order = objects.Add<Order>(); Reflect.SetField(order, "key", key); return order; }

[Test] public void KeyOf_TheSameOrder_IsTheSameOnEveryMachine()
{   // FNV-1a over exact bits: nothing that differs between runs, such as string.GetHashCode
    Assert.AreEqual(742737598, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy,
        new Vector3(3.1f, 4.43f, -11.5f), new Vector3(-89.8f, 4.43f, 41.6f)));
}

[Test] public void KeyOf_AnyDifference_GivesAnotherKey()
{
    Vector3 pickup = new Vector3(3.1f, 4.43f, -11.5f), dropoff = new Vector3(-89.8f, 4.43f, 41.6f);
    int key = OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup, dropoff);
    Assert.AreNotEqual(key, OrderBook.KeyOf("FinalAreaScene", Constants.OrderValue.Easy, pickup, dropoff), "another scene");
    Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Medium, pickup, dropoff), "another value");
    Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup + Vector3.right, dropoff), "another pickup");
    Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup, dropoff + Vector3.right), "another dropoff");
}

[Test] public void Find_GivesTheOrderAddedUnderAKey_AndNothingElse()
{
    Order order = OrderWithKey(5);
    OrderBook.Add(order);
    Assert.AreSame(order, OrderBook.Find(5));
    Assert.IsNull(OrderBook.Find(6), "another key");
    Assert.IsNull(OrderBook.Find(OrderBook.NONE), "no order");
}

[Test] public void Remove_TakesOnlyThatOrderOut()
{
    Order first = OrderWithKey(5), second = OrderWithKey(5);
    OrderBook.Add(first);
    OrderBook.Remove(second);
    Assert.AreSame(first, OrderBook.Find(5), "another order under its key stays");
    OrderBook.Remove(first);
    Assert.IsNull(OrderBook.Find(5));
}

[Test] public void Add_TwoLiveOrdersUnderOneKey_IsAnError_AndTheFirstStays()
{
    Order first = OrderWithKey(5), second = OrderWithKey(5);
    OrderBook.Add(first);
    LogAssert.Expect(LogType.Error, new Regex("share the key 5"));
    OrderBook.Add(second);
    Assert.AreSame(first, OrderBook.Find(5));
}

[Test] public void Add_TheKeyOfAnOrderThatsGone_IsTakenOver()
{
    Order gone = OrderWithKey(5), next = OrderWithKey(5);
    OrderBook.Add(gone);
    Object.DestroyImmediate(gone.gameObject);
    OrderBook.Add(next); // no error: its scene unloaded
    Assert.AreSame(next, OrderBook.Find(5));
}
```

`OrderBookSceneTests` (opens scenes like `GameSceneSpawnTests`; `TearDown` closes the scene):

```csharp
[TestCase("Assets/Scenes/Design Scene(Main).unity", 28)]  // 19 in the waves, 4 tutorial, 4 cardboard-cutout, 1 unused golden
[TestCase("Assets/Scenes/FinalAreaScene.unity", 1)]
public void EveryOrderInAMatchScene_HasItsOwnKey(string path, int orders)
{
    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    List<int> keys = new List<int>();
    foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Order order in root.GetComponentsInChildren<Order>(true))
            keys.Add(OrderBook.KeyOf(order));

    Assert.AreEqual(orders, keys.Count, "orders in the scene");
    CollectionAssert.AllItemsAreUnique(keys);
    CollectionAssert.DoesNotContain(keys, OrderBook.NONE);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "OrderBook"`
Expected: `NO RESULTS` with `error CS0103: The name 'OrderBook' does not exist`.

- [ ] **Step 3: Implement `OrderBook`, and register orders**
  - `KeyOf`: FNV-1a 32-bit: basis `2166136261`, prime `16777619`, `h = (h ^ b) * prime` per byte, over these bytes in order:
    - Each char of `scene`: its low byte, then its high byte.
    - `(int)value`, as 4 little-endian bytes.
    - The bits of `pickup.x, .y, .z, dropoff.x, .y, .z` (`BitConverter.SingleToInt32Bits`), 4 little-endian bytes each.
    - The result is `unchecked((int)h)`. A 0 becomes 1, so `NONE` is never a key.
  - `Add`:
    - A live order already under that key (not itself) → `Debug.LogError("Orders " + a.name + " and " + b.name + " share the key " + key + ": online, the second isn't shared")` and keep the first.
    - A destroyed one (Unity null) is replaced.
  - `Order.cs`:
    - `private int key; public int Key { get { return key; } }`.
    - `PickupPoint` / `DropoffPoint` return the `pickup` / `dropoff` fields.
    - `Awake` ends with `key = OrderBook.KeyOf(this); OrderBook.Add(this);`.
    - A new `private void OnDestroy() { OrderBook.Remove(this); }`.
  - Metas for the new files.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "OrderBook"`
Expected: `tests: 8 total, 8 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: Run the smoke test** (orders now register as they load)

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed`.

- [ ] **Step 6: No commit** (your human partner commits).

---

### Task 2: Order changes and who may make them

**Files:**
- Create: `Assets/Scripts/Online/OrderChange.cs`, `Assets/Scripts/Online/OrderSync.cs`
- Test: `Assets/Tests/Editor/OrderChangeTests.cs`, `Assets/Tests/Editor/OrderSyncTests.cs`

**Interfaces:**
- Consumes: `GameAuthority` (Phase 2B), `PlayerInstantiate.Instance.Roster` (`Players`, `PlayerSlot.Index`, `PlayerSlot.Player`), `OrderBook.NONE` (Task 1).
- Produces:

```csharp
public enum OrderChangeKind : byte { Spawn, Pickup, Deliver, Drop, Erase, EraseGold }

public struct OrderChange : INetworkSerializable
{
    public OrderChangeKind Kind;
    public int Order;      // OrderBook key (NONE when none)
    public int Seat;       // Pickup, Deliver, Drop: the player's seat; -1 otherwise
    public bool Flag;      // Spawn: joins the order manager's active list (InitOrder's shouldAdd); Drop: the player spins out
    public int Order2;     // Drop: the order in the second slot (NONE when empty)
    public Vector3 Spot;   // Drop: where the first order lands
    public float Height;   // Drop: how high it flies first
    public Vector3 Spot2;
    public float Height2;
    public static OrderChange Spawn(int order, bool addToActive);
    public static OrderChange Pickup(int order, int seat);
    public static OrderChange Deliver(int order, int seat);
    public static OrderChange Drop(int seat, int order1, Vector3 spot1, float height1, int order2, Vector3 spot2, float height2, bool spinOut);
    public static OrderChange Erase(int order);
    public static OrderChange EraseGold(int order);
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter;   // every field, declaration order
    public override string ToString();                                                        // e.g. "Pickup 742737598 seat 1"
}

public static class OrderSync
{
    public static bool MayChange { get; }       // GameAuthority.IsAuthority || Showing
    public static bool Showing { get; }         // client: replaying one of the host's changes
    public static event Action<OrderChange> Changed;                              // online host: each outermost change
    public static event Action<OrderHandler, Vector3, Vector3, bool> DropAsked;   // online client: its player's drop (spot1, spot2, spinOut)
    public static Scope Change(OrderChange change);   // using (OrderSync.Change(...)) { the change }
    public struct Scope : IDisposable { public void Dispose(); }
    public static void Show(Action change);            // runs it with Showing set
    public static void AskDrop(OrderHandler handler, Vector3 spot1, Vector3 spot2, bool spinOut);
    public static int SeatOf(Component part);          // the roster slot whose Player holds it; -1
    public static OrderHandler HandlerIn(int seat);    // null for an empty or unknown seat
    public static void Reset();                        // tests: listeners and nesting
}
```

- [ ] **Step 1: Write the failing tests**

`OrderChangeTests`:

```csharp
static IEnumerable<OrderChange> EveryKind()
{
    yield return OrderChange.Spawn(11, true);
    yield return OrderChange.Pickup(11, 2);
    yield return OrderChange.Deliver(11, 2);
    yield return OrderChange.Drop(2, 11, new Vector3(1, 2, 3), 4.5f, 12, new Vector3(-1, 0, 7), 9f, true);
    yield return OrderChange.Erase(11);
    yield return OrderChange.EraseGold(13);
}

[TestCaseSource(nameof(EveryKind))]
public void AChange_ArrivesAsItWasSent(OrderChange sent)
{
    using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
    {
        writer.WriteNetworkSerializable(sent);
        using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
        {
            reader.ReadNetworkSerializable(out OrderChange received);
            Assert.AreEqual(sent, received);
        }
    }
}

[Test] public void Drop_CarriesBothOrdersSpotsAndHeights()
{
    OrderChange drop = OrderChange.Drop(2, 11, new Vector3(1, 2, 3), 4.5f, 12, new Vector3(-1, 0, 7), 9f, true);
    Assert.AreEqual(OrderChangeKind.Drop, drop.Kind);
    Assert.AreEqual(2, drop.Seat);
    Assert.AreEqual(11, drop.Order);
    Assert.AreEqual(12, drop.Order2);
    Assert.AreEqual(new Vector3(1, 2, 3), drop.Spot);
    Assert.AreEqual(4.5f, drop.Height);
    Assert.AreEqual(new Vector3(-1, 0, 7), drop.Spot2);
    Assert.AreEqual(9f, drop.Height2);
    Assert.IsTrue(drop.Flag, "spins out");
}

[Test] public void ChangesWithoutAPlayer_HaveNoSeat()
{
    Assert.AreEqual(-1, OrderChange.Spawn(11, false).Seat);
    Assert.AreEqual(-1, OrderChange.Erase(11).Seat);
    Assert.AreEqual(-1, OrderChange.EraseGold(11).Seat);
}
```

`OrderSyncTests`:
- `SetUp` subscribes `OrderSync.Changed += told.Add` (a `List<OrderChange> told`).
- `TearDown` runs `OrderSync.Reset()`, sets `GameAuthority.Role = NetworkRole.Offline`, runs `Reflect.SetSingleton<PlayerInstantiate>(null)`, `objects.DestroyAll()` and `told.Clear()`.

```csharp
[TestCase(NetworkRole.Offline)] [TestCase(NetworkRole.Host)]
public void MayChange_OfflineAndOnTheHost_Always(NetworkRole role)
{ GameAuthority.Role = role; Assert.IsTrue(OrderSync.MayChange); }

[Test] public void MayChange_OnAClient_OnlyWhileShowingTheHostsChange()
{
    GameAuthority.Role = NetworkRole.Client;
    bool during = false;
    OrderSync.Show(() => during = OrderSync.MayChange);
    Assert.IsTrue(during, "replaying the host's change");
    Assert.IsFalse(OrderSync.MayChange, "on its own");
}

[Test] public void Change_OnAnOnlineHost_GoesOutOnce_WithWhatItDoesOnTheWay()
{
    GameAuthority.Role = NetworkRole.Host;
    using (OrderSync.Change(OrderChange.Deliver(7, 1)))
    using (OrderSync.Change(OrderChange.Erase(7))) { }
    CollectionAssert.AreEqual(new[] { OrderChange.Deliver(7, 1) }, told);
}

[Test] public void Change_OneAfterAnother_EachGoesOut()
{
    GameAuthority.Role = NetworkRole.Host;
    using (OrderSync.Change(OrderChange.Spawn(7, true))) { }
    using (OrderSync.Change(OrderChange.Pickup(7, 0))) { }
    CollectionAssert.AreEqual(new[] { OrderChange.Spawn(7, true), OrderChange.Pickup(7, 0) }, told);
}

[TestCase(NetworkRole.Offline)] [TestCase(NetworkRole.Client)]
public void Change_OfflineOrOnAClient_GoesNowhere(NetworkRole role)
{
    GameAuthority.Role = role;
    OrderSync.Show(EraseSeven);   // a client replaying the host's change doesn't send it back
    EraseSeven();
    CollectionAssert.IsEmpty(told);
}

static void EraseSeven() { using (OrderSync.Change(OrderChange.Erase(7))) { } }

[Test] public void Change_ThatThrows_StillEnds_SoTheNextGoesOut()
{
    GameAuthority.Role = NetworkRole.Host;
    Assert.Throws<InvalidOperationException>(ThrowInsideAChange);
    using (OrderSync.Change(OrderChange.Erase(8))) { }
    CollectionAssert.AreEqual(new[] { OrderChange.Erase(7), OrderChange.Erase(8) }, told);
}

static void ThrowInsideAChange() { using (OrderSync.Change(OrderChange.Erase(7))) throw new InvalidOperationException(); }

[Test] public void AskDrop_TellsTheListener_WhoDropsAndWhere()
{
    OrderHandler handler = objects.Add<OrderHandler>(), asked = null;
    Vector3 first = default, second = default; bool spinOut = true;
    OrderSync.DropAsked += (h, a, b, s) => { asked = h; first = a; second = b; spinOut = s; };
    OrderSync.AskDrop(handler, Vector3.up, Vector3.right, false);
    Assert.AreSame(handler, asked);
    Assert.AreEqual(Vector3.up, first);
    Assert.AreEqual(Vector3.right, second);
    Assert.IsFalse(spinOut);
}

[Test] public void SeatOf_AndHandlerIn_FollowTheRoster()
{
    PlayerInstantiate players = objects.Add<PlayerInstantiate>();
    Reflect.SetSingleton(players);
    GameObject avatar = objects.NewGameObject("P3");
    GameObject control = new GameObject("Control");
    control.transform.SetParent(avatar.transform);
    OrderHandler handler = control.AddComponent<OrderHandler>();
    players.Roster.JoinRemoteAt(avatar, 7, 2);

    Assert.AreEqual(2, OrderSync.SeatOf(handler));
    Assert.AreSame(handler, OrderSync.HandlerIn(2));
    Assert.IsNull(OrderSync.HandlerIn(0), "an empty seat");
    Assert.AreEqual(-1, OrderSync.SeatOf(objects.Add<OrderHandler>()), "nobody's");
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "OrderChangeTests|OrderSyncTests"`
Expected: `NO RESULTS` with `error CS0246` for `OrderChange` / `OrderSync`.

- [ ] **Step 3: Implement both**
  - `OrderSync.Change`:
    - Raises `Changed` first when all hold: nothing else is changing (a nesting counter), `GameAuthority.IsOnline`, `GameAuthority.IsAuthority`, and not `Showing`.
    - Then counts itself in. `Scope.Dispose` counts it out.
  - `Show`: a second counter, in `try`/`finally`.
  - `SeatOf`: the first roster slot whose `Player` is the part's object or an ancestor of it (`part.transform.IsChildOf(slot.Player.transform)`), or -1 without `PlayerInstantiate.Instance`.
  - `HandlerIn`: `slot.Player.GetComponentInChildren<OrderHandler>(true)`.
  - Metas.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "OrderChangeTests|OrderSyncTests"`
Expected: `tests: 18 total, 18 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: No commit.**

---

### Task 3: Orders follow the rules

**Files:**
- Modify: `Assets/Scripts/World/Order.cs` (`i/crlf`), `Assets/Scripts/Player/OrderHandler.cs`, `Assets/Scripts/World/OrderBeacon.cs`
- Test: `Assets/Tests/Editor/OrderRulesTests.cs` (new), `Assets/Tests/Editor/RemoteScooterRulesTests.cs` (modify)

**Interfaces:**
- Consumes: Task 1 `Order.Key`; Task 2 `OrderSync` (`MayChange`, `Showing`, `Change`, `AskDrop`, `SeatOf`) and `OrderChange`.
- Produces:
  - `Order`:
    - `public const float DROP_HEIGHT_MIN = 1f, DROP_HEIGHT_MAX = 10f;`
    - `public static float DropHeight()`: `Random.Range(min, max)`, the range `Drop` used.
    - `public void Drop(Vector3 newPosition, float height)`: the height is now a parameter.
    - `public void FinishMoves()`: `DOTween.Complete(transform, true)`, so a drop's fall or a delivery's throw ends now, callbacks included.
    - `public void ReturnHome()`: takes the order off whatever carries it and back into the scene it loaded with, so it goes when that scene does. It's destroyed now if that scene is gone. The scene is `gameObject.scene`, saved in `Awake` as `homeScene`.
      - Why: scooters and the order manager outlive scenes (DontDestroyOnLoad), and an order parented under one moves with it.
      - How: `transform.SetParent(null)`, then `SceneManager.MoveGameObjectToScene(gameObject, homeScene)` when `homeScene.isLoaded`, else `Destroy(gameObject)`.
  - `OrderHandler`:
    - `public void DropEverything(Vector3 order1NewPos, Vector3 order2NewPos, bool shouldSpinout = true)`: an online client asks the host.
    - `public void DropHeld(Vector3 spot1, float height1, Vector3 spot2, float height2, bool spinOut)`: the drop itself.

What changes (the gate is each method's first line; the change scope wraps what actually happens):

| Where | Gate | Change sent |
|---|---|---|
| `Order.InitOrder(shouldAdd)` | `if (!OrderSync.MayChange) return;` | `Spawn(Key, shouldAdd)` around the body |
| `Order.EraseOrder()` | same | `Erase(Key)` |
| `Order.EraseGoldWithoutDelivering()` | same | `EraseGold(Key)` |
| `OrderHandler.AddOrder(order)` | same | `Pickup(order.Key, OrderSync.SeatOf(this))` inside the branch that takes it. Its cooldown check becomes `OrderSync.Showing \|\| inOrder.CanPickup \|\| inOrder.PlayerDropped != this`: the host already checked |
| `OrderHandler.DeliverOrder(order)` | same | `Deliver(order.Key, seat)` inside each matching branch. The score only changes `if (GameAuthority.IsAuthority)`: clients get the host's |
| `OrderHandler.DropEverything(p1, p2, spin)` | an online client not `Showing` calls `OrderSync.AskDrop(this, p1, p2, spin)` and returns | none: it calls `DropHeld(p1, Order.DropHeight(), p2, Order.DropHeight(), spin)` |
| `OrderHandler.DropHeld(...)` | `MayChange` | `Drop(seat, order1 key or NONE, spot1, height1, order2 key or NONE, spot2, height2, spinOut)`; its body is today's `DropEverything`, calling `Drop(spot, height)`, and `GotHit?.Invoke()` (another machine's scooter never starts, so nobody listens) |

Also:
- `OrderHandler.ResetHandler` (the way back to the menu), after its erases:
  - Any order still held goes back to its own scene: `ReturnHome()`. On a client the erases wait for the host, so without this the order would ride into the menu on the player's scooter, still showing.
  - Then `order1 = order2 = null; hasOrder = false;`.
- `Order.RemovePlayerHolding` and `OrderBeacon.SetDropoff` skip the compass when the holder has none (another machine's scooter): `Compass c = ....GetComponent<Compass>(); if (c != null) ...`.
- `OrderBeacon.IsPlayersBall(other)` becomes `other.name == "Ball Of Fun"`.
  - The beacon only acts on the authority, and the host decides every scooter's pickups.
  - Its summary says so.
- `OrderBeacon`, the cosmetic parts of `EraseBeacon` and `Order.ReInitOrder` (the fall landing) stay ungated. They run on every machine, and `EraseOrder`'s own gate keeps a client's throw from erasing by itself.

- [ ] **Step 1: Write the failing tests**

`OrderRulesTests`:
- `TearDown` sets `GameAuthority.Role = Offline`, then runs `OrderSync.Reset()` and `objects.DestroyAll()`.
- `NewPlayer` is `AuthorityGateTests`' helper: a root, `Control` with an `OrderHandler` whose `ball` is a `BallDriving`, and a `Ball Of Fun`.
- Offline, each of these calls goes on to scene objects a test doesn't build: the gate must come first.

```csharp
[Test] public void InitOrder_OnAClient_WaitsForTheHost()
{   // e.g. the golden round's FinalOrder.Start: the host's Spawn shows it
    GameAuthority.Role = NetworkRole.Client;
    Order order = objects.Add<Order>();
    order.InitOrder();
    Assert.IsFalse(order.IsActive);
}

[Test] public void EraseOrder_OnAClient_WaitsForTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    Order order = objects.Add<Order>();
    order.IsActive = true;
    order.EraseOrder();
    Assert.IsTrue(order.IsActive);
}

[Test] public void EraseGoldWithoutDelivering_OnAClient_WaitsForTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    Order order = objects.Add<Order>();
    order.IsActive = true;
    order.EraseGoldWithoutDelivering();
    Assert.IsTrue(order.IsActive);
}

[Test] public void AddOrder_OnAClient_WaitsForTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    OrderHandler player = NewPlayer("Player 2");
    Order order = objects.Add<Order>();
    player.AddOrder(order);
    Assert.IsFalse(player.HasOrder);
    Assert.IsNull(order.PlayerHolding);
}

[Test] public void DeliverOrder_OnAClient_WaitsForTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    OrderHandler player = NewPlayer("Player 2");
    Order order = objects.Add<Order>();
    Reflect.SetField(player, "order1", order);
    player.DeliverOrder(order);
    Assert.AreSame(order, Reflect.GetField(player, "order1"), "still held");
    Assert.AreEqual(0, player.Score);
}

[Test] public void DropEverything_OnAClient_AsksTheHost_AndKeepsTheOrders()
{
    GameAuthority.Role = NetworkRole.Client;
    OrderHandler player = NewPlayer("Player 2");
    Order order = objects.Add<Order>();
    Reflect.SetField(player, "order1", order);
    OrderHandler asked = null;
    Vector3 first = default;
    OrderSync.DropAsked += (h, a, b, s) => { asked = h; first = a; };

    player.DropEverything(Vector3.up, Vector3.right, false);

    Assert.AreSame(player, asked, "the host is asked");
    Assert.AreEqual(Vector3.up, first);
    Assert.AreSame(order, Reflect.GetField(player, "order1"), "the host drops it");
}

[Test] public void ReturnHome_TakesTheOrderOffItsScooter_BackIntoItsOwnScene()
{
    Scene home = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    try
    {
        Order order = new GameObject("Order").AddComponent<Order>();
        SceneManager.MoveGameObjectToScene(order.gameObject, home);
        Reflect.SetField(order, "homeScene", home);
        order.transform.SetParent(NewPlayer("Player 1").transform); // riding on a scooter, in another scene

        order.ReturnHome();

        Assert.IsNull(order.transform.parent, "off the scooter");
        Assert.AreEqual(home, order.gameObject.scene, "back where it loaded");
    }
    finally { EditorSceneManager.CloseScene(home, true); }
}
```

`RemoteScooterRulesTests`:
- Rename `OrderBeacons_OnlyServeTheScootersThisMachineDrives` to `OrderBeacons_ServeEveryScooter_OnTheHostThatDecides`.
- Another machine's ball is now `Assert.IsTrue(…, "another machine's: the host decides its pickups too (beacons act only on the host)")`.
- The class summary drops "collect orders".

- [ ] **Step 2: Run to verify they fail**
  - First add an empty `public void ReturnHome() { }` to `Order.cs`, so the tests compile.

Run: `bash tools/run-tests.sh "OrderRulesTests|RemoteScooterRulesTests"`
Expected: 8 failures.
- The 7 new tests fail, most with a `NullReferenceException`: with no gate, each call goes on to the scene objects a bare order lacks (the order manager, the arrow, the sound pool).
- `ReturnHome_…` fails its "off the scooter" assert.
- The renamed beacon test fails its "another machine's" assert.

- [ ] **Step 3: Implement the table above.** Use the Edit tool on `Order.cs`.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "OrderRulesTests|RemoteScooterRulesTests|AuthorityGateTests"`
Expected: `tests: 36 total, 36 passed` (7 new; 7 in `RemoteScooterRulesTests`; 22 in `AuthorityGateTests`).

- [ ] **Step 5: Run the smoke test** (offline must be unchanged)

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed`.

- [ ] **Step 6: No commit.**

---

### Task 4: The match carries orders, drop requests, scores and the golden value

**Files:**
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`, `Assets/Scripts/Online/OnlinePlayer.cs`
- Test: `Assets/Tests/Editor/OrderMessagesNetworkTests.cs` (port 7798; empty Play Mode scene like `OnlinePlayersNetworkTests`)

**Interfaces:**
- Consumes: Task 2 `OrderChange`, `OrderBook.NONE`.
- Produces:

```csharp
// OnlineMatch
public int GoldenValue { get; }                                    // NetworkVariable<int>, starts at (int)Constants.OrderValue.Golden (50)
public event Action<OrderChange> OrderReceived;                    // clients: each change the host made, in order with its states
public event Action<ulong, int, Vector3, Vector3, bool> DropAsked; // host: machine (client id), seat, spot1, spot2, spinOut
public void SendOrder(OrderChange change);                         // host -> OrderClientRpc; nothing on a client
public void AskDrop(int seat, Vector3 spot1, Vector3 spot2, bool spinOut); // client -> DropServerRpc (RequireOwnership = false); nothing on the host
public void ShareGoldenValue(int value);                           // host; only a changed value is written
// OnlinePlayer
public int Score { get; }                                          // NetworkVariable<int>, host-written
public void ShareScore(int value);                                 // host; nothing on a client
```

The host skips its own `OrderClientRpc`, as `StateClientRpc` does: it made the change.

- [ ] **Step 1: Write the failing tests**
  - Recorder classes:
    - `ChangeRecorder` records `OrderReceived` into `Changes`.
    - `DropRecorder` records `DropAsked` into `Machine`, `Seat`, `Spot1`, `Spot2`, `SpinOut` and a `Count`.
  - Helpers `PlayerInSeat(session, seat)` and `Sees(session, 2)` as in `OnlinePlayersNetworkTests`.
  - Every test:
    - `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`, then Play Mode.
    - Host with `HostDirect("127.0.0.1", 7798)`, client with `JoinDirect`; wait until both see 2 players.
    - Ends with `log.MachinesLeave()`, the client leaving, the host leaving, and `Assert.IsEmpty(log.Problems)`.

```csharp
[UnityTest] public IEnumerator TheHostsOrderChanges_ReachEveryClient_InOrder()
// hostHeard = new ChangeRecorder(host.Match); clientHeard = new ChangeRecorder(client.Match)
// host.Match.SendOrder(OrderChange.Spawn(5, true)); ...Pickup(5, 1); ...Drop(1, 5, new Vector3(1, 2, 3), 4f, OrderBook.NONE, Vector3.zero, 4f, false)
// wait (WAIT = 10 s) until clientHeard.Changes.Count == 3
CollectionAssert.AreEqual(new[] { OrderChange.Spawn(5, true), OrderChange.Pickup(5, 1),
    OrderChange.Drop(1, 5, new Vector3(1, 2, 3), 4f, OrderBook.NONE, Vector3.zero, 4f, false) }, clientHeard.Changes);
CollectionAssert.IsEmpty(hostHeard.Changes, "the host made them");
// client.Match.SendOrder(OrderChange.Erase(5)); wait 1 s
Assert.AreEqual(3, clientHeard.Changes.Count, "a client can't send changes");
CollectionAssert.IsEmpty(hostHeard.Changes);

[UnityTest] public IEnumerator AClientsDropRequest_ReachesTheHost_WithWhoAsked()
// asked = new DropRecorder(host.Match); client.Match.AskDrop(1, new Vector3(1, 2, 3), new Vector3(4, 5, 6), false)
// wait until asked.Count == 1
Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Seat);
Assert.AreEqual(new Vector3(1, 2, 3), asked.Spot1);
Assert.AreEqual(new Vector3(4, 5, 6), asked.Spot2);
Assert.IsFalse(asked.SpinOut);
// host.Match.AskDrop(0, Vector3.zero, Vector3.zero, false); wait 1 s
Assert.AreEqual(1, asked.Count, "the host drops its own at once: it never asks");

[UnityTest] public IEnumerator ScoresAndTheGoldenValue_ReachEveryClient_OnlyFromTheHost()
Assert.AreEqual(50, client.Match.GoldenValue, "the golden order's starting value");
// host.Match.ShareGoldenValue(75); PlayerInSeat(host, 1).ShareScore(120)
// wait until client.Match.GoldenValue == 75 && PlayerInSeat(client, 1).Score == 120
Assert.AreEqual(75, client.Match.GoldenValue);
Assert.AreEqual(120, PlayerInSeat(client, 1).Score);
// client.Match.ShareGoldenValue(5); PlayerInSeat(client, 1).ShareScore(1); wait 1 s
Assert.AreEqual(75, host.Match.GoldenValue, "a client can't change it");
Assert.AreEqual(120, PlayerInSeat(host, 1).Score, "nor a score");
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh OrderMessagesNetworkTests`
Expected: `NO RESULTS` with `error CS1061` (`SendOrder`, `AskDrop`, `ShareGoldenValue`, `ShareScore` missing).

- [ ] **Step 3: Implement the members above.** Add to the `OnlineMatch` summary: "- The host's order changes, clients' drop requests and the golden order's value (OnlineOrders)". Add to `OnlinePlayer`'s: "and their score, which only the host writes".

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "OrderMessagesNetworkTests|OnlinePlayersNetworkTests"`
Expected: `tests: 8 total, 8 passed`.

- [ ] **Step 5: No commit.**

---

### Task 5: The host shares the orders

**Files:**
- Create: `Assets/Scripts/Online/OnlineOrders.cs`
- Modify: `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Scripts/Player/OrderHandler.cs`, `Assets/Scripts/Player/PlayerInstantiate.cs` (`i/crlf`)
- Test: `Assets/Tests/Editor/OnlineOrdersNetworkTests.cs` (port 7799; the menu scene, like `OnlineMatchNetworkTests`)

**Interfaces:**
- Consumes: Tasks 2–4: `OrderSync.Changed`, `OrderSync.HandlerIn`, `OnlineMatch.SendOrder/DropAsked/ShareGoldenValue`, `OnlinePlayer.ShareScore`; `OnlineSession.SeatOf(ulong)`, `Players`, `Match`, `MatchSpawned`.
- Produces:
  - `public class OnlineOrders : MonoBehaviour` with `public void Begin(OnlineSession session)`. `OnlineGame.Begin` adds it next to `OnlineDriving`.
  - On the host:
    - `OrderSync.Changed` → `session.Match.SendOrder` while a match exists and is the server.
    - `Match.DropAsked(machine, seat, …)` → when `session.SeatOf(machine) == seat`: `OrderSync.HandlerIn(seat).DropEverything(spot1, spot2, spinOut)`. The host is the authority, so it drops with its own heights and the change goes out.
    - Each `Update`: every `OnlinePlayer`'s `ShareScore(HandlerIn(player.Seat).Score)`, and `ShareGoldenValue(OrderManager.Instance.FinalOrderValue)` while an `OrderManager` exists.
  - Subscriptions:
    - `DropAsked` on `MatchSpawned`, and at `Begin` for a match already there.
    - All undone in `OnDestroy`. `OrderSync`'s events are static, so an ended session must never keep listening.
  - `OrderHandler.ReleaseOrders()`: this player is leaving (their machine left), so their orders go back to the pool.
    - Each held order first goes `ReturnHome()` (Task 3), so it outlives the scooter.
    - Then `EraseOrder()` (gated: only the host erases; a client waits for the host's `Erase`).
    - Then `order1 = order2 = null; hasOrder = false`.
  - `PlayerInstantiate.RemoveRemotePlayer` calls `ReleaseOrders()` on the leaver's `OrderHandler` before `Destroy(slot.Player)`.

- [ ] **Step 1: Write the failing test**

Use the same setup as `OnlineMatchNetworkTests.Hosting_…`: the menu scene, `TestPlayers.Add()`, and the host with `OnlineGame.Attach`. The other machine is a bare session on port 7799.

Recorders:
- `ChangeRecorder` on `other.Match` (Task 4's, copied into this class).

Static helpers:
- Copied from the other network tests: `State`, `ActiveScene`, `Slot` and `CitySpawns` (`OnlineMatchNetworkTests`), `ScooterInSeat` (`OnlineDrivingNetworkTests`), and `PlayerInSeat` (`OnlinePlayersNetworkTests`).
- `Handler(seat)`: `Slot(seat).Player.GetComponentInChildren<OrderHandler>(true)`.
- `ShareAt(OnlineScooter scooter, Vector3 spot)`: `scooter.Share(new ScooterPose { Ball = spot, Heading = 0, ModelPosition = spot, ModelRotation = Quaternion.identity }, default(DriveFlags))`.
- `FirstNewSpawn(recorder, skip)`: the first `Spawn` in `Changes` whose key isn't in `skip`, or null.
- `CountOf(recorder, kind)`: how many changes of a kind it heard.
- A first pickup rides in `OrderHandler`'s **second** slot (`AddOrder` fills `order2` first). So a one-order drop carries that order as `Order2` / `Spot2` / `Height2`, with `Order` = `NONE`.

```csharp
[UnityTest]
public IEnumerator Hosting_TheHostDecidesEveryScootersOrders_AndSharesThemWithTheScores()
// The match starts (the other machine reports loaded), the tutorial is skipped, the first wave begins (wait for Begin, 60 s)
// The first wave's first order: spawned here, and the other machine hears it (wait up to 15 s for a Spawn)
Order first = OrderBook.Find(spawn.Order);
Assert.IsTrue(first != null && first.IsActive, "spawned here");
// The other machine's scooter drives into its light: the host sees it there and gives it the order
ShareAt(ScooterInSeat(other, 1), first.transform.position);            // wait for OrderChange.Pickup(first.Key, 1)
Assert.AreSame(Handler(1), first.PlayerHolding, "on the other machine's scooter, here");
// To the dropoff: delivered, erased after its throw, and its player scores its value (on their OnlinePlayer)
ShareAt(ScooterInSeat(other, 1), first.DropoffPoint.position);         // wait for Deliver(first.Key, 1), then Erase(first.Key)
// wait until PlayerInSeat(other, 1).Score == (int)first.Value
Assert.AreEqual((int)first.Value, PlayerInSeat(other, 1).Score);
// Another order: its player falls in the water holding it: the other machine asks, and the host drops it where asked
Order second = /* next new Spawn, picked up the same way */;
Vector3 spot = CitySpawns()[1].transform.position;
other.Match.AskDrop(1, spot, spot, false);                              // wait for a Drop change for seat 1
Assert.AreEqual(OrderBook.NONE, drop.Order, "its first rack was empty");
Assert.AreEqual(second.Key, drop.Order2, "a first pickup rides on the second rack");
Assert.AreEqual(spot, drop.Spot2);
Assert.That(drop.Height2, Is.InRange(Order.DROP_HEIGHT_MIN, Order.DROP_HEIGHT_MAX), "the host picked how high it flies");
Assert.IsFalse(Handler(1).HasOrder, "dropped here too");
// Asking for another seat's drop does nothing
other.Match.AskDrop(0, spot, spot, false);                              // wait 1 s
Assert.AreEqual(1, CountOf(heard, OrderChangeKind.Drop), "not their seat");
// The golden order's value reaches the other machine
OrderManager.Instance.FinalOrderValue = 90;                             // wait until other.Match.GoldenValue == 90
// The other machine leaves holding an order: here it goes back to the pool (not destroyed with their scooter)
Order third = /* next new Spawn, picked up the same way */;
log.MachinesLeave(); other.Leave();                                     // wait until host.PlayersIn == 1, then a frame
Assert.IsTrue(third != null, "still here");
Assert.IsFalse(third.IsActive, "back in the pool");
host.Leave(); log.Dispose(); Assert.IsEmpty(log.Problems, ...);
```

- [ ] **Step 2: Run to verify it fails**

Run: `bash tools/run-tests.sh OnlineOrdersNetworkTests`
Expected: FAIL: no `Spawn` reaches the other machine yet (the first-spawn wait runs out).

- [ ] **Step 3: Implement `OnlineOrders` (host side), `ReleaseOrders`, and the `RemoveRemotePlayer` call** (Edit tool on `PlayerInstantiate.cs`). Add "every order and score (OnlineOrders)" to `OnlineGame`'s summary list.

- [ ] **Step 4: Run to verify it passes**

Run: `bash tools/run-tests.sh "OnlineOrdersNetworkTests|OnlineMatchNetworkTests"`
Expected: `tests: 5 total, 5 passed`.

- [ ] **Step 5: No commit.**

---

### Task 6: A client shows the host's orders

**Files:**
- Create: `Assets/Scripts/Online/HostQueue.cs`
- Modify: `Assets/Scripts/Online/OnlineOrders.cs`, `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Scripts/Player/OrderHandler.cs`
- Test: `Assets/Tests/Editor/HostQueueTests.cs` (new), `Assets/Tests/Editor/OnlineOrdersNetworkTests.cs` (add a test)

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces:

```csharp
public class HostQueue                          // client: the host's messages, in order; they wait while this machine's scene changes
{
    public HostQueue(Func<bool> holding);
    public int Waiting { get; }
    public void Add(Action message);            // runs now unless holding() or others are waiting
    public void Release();                      // runs the waiting ones in order, while !holding()
    public void Clear();
}
// OnlineOrders: public void Show(OrderChange change)   // client: replays one host change here
// OrderHandler: public void ShowHostScore(int hostScore) // client: the host's score for this player, in their HUD
```

- `OnlineGame`:
  - `held` becomes `HostQueue hostMessages = new HostQueue(IsChanging)` (`sceneFlow.Changing`).
  - `FollowHost` does `hostMessages.Add(() => ApplyHostState(hostState))`. The client's `match.OrderReceived` does `hostMessages.Add(() => orders.Show(change))`.
  - `SceneChanged` does `hostMessages.Release()`, and going offline does `Clear()`.
- `OnlineOrders.Show`:
  - Finds the order (`OrderBook.Find`) and the seat's handler. A change whose order or seat isn't here is dropped silently: its scene went, or its player left.
  - Calls `order.FinishMoves()` on the orders involved **before** replaying, outside `Show`. A fall still landing, or a throw still flying, ends first. The throw's own erase stays gated.
  - Then `OrderSync.Show(...)` with:
    - Spawn → `InitOrder(Flag)`.
    - Pickup → `AddOrder`.
    - Deliver → `DeliverOrder`.
    - Drop → `DropHeld(Spot, Height, Spot2, Height2, Flag)`.
    - Erase → `EraseOrder()`.
    - EraseGold → `EraseGoldWithoutDelivering()`.
- On a client:
  - `OrderSync.DropAsked` → `match.AskDrop(OrderSync.SeatOf(handler), …)` for this machine's own seat.
  - Each `Update`: each `OnlinePlayer`'s handler gets `ShowHostScore(player.Score)` when it differs, then one `ScoreManager.Instance.UpdatePlacement()`. And `OrderManager.Instance.FinalOrderValue = match.GoldenValue`.

- [ ] **Step 1: Write the failing tests**

`HostQueueTests` (EditMode; `bool holding` and `List<int> ran` are fields; the queue is `new HostQueue(IsHolding)`):

```csharp
[Test] public void NotHolding_AMessageRunsAtOnce()
{ queue.Add(() => ran.Add(1)); CollectionAssert.AreEqual(new[] { 1 }, ran); }

[Test] public void Holding_MessagesWait_ThenRunInOrderOnRelease()
{
    holding = true;
    queue.Add(() => ran.Add(1)); queue.Add(() => ran.Add(2));
    Assert.AreEqual(2, queue.Waiting); CollectionAssert.IsEmpty(ran);
    holding = false; queue.Release();
    CollectionAssert.AreEqual(new[] { 1, 2 }, ran);
}

[Test] public void AfterTheHold_ANewMessageWaitsBehindTheOnesStillWaiting()
{   // a state and an order change keep the host's order even when the scene comes up in between
    holding = true; queue.Add(() => ran.Add(1));
    holding = false; queue.Add(() => ran.Add(2));
    CollectionAssert.IsEmpty(ran, "1 hasn't gone yet");
    queue.Release();
    CollectionAssert.AreEqual(new[] { 1, 2 }, ran);
}

[Test] public void Release_WhileStillHolding_RunsNothing()
{ holding = true; queue.Add(() => ran.Add(1)); queue.Release(); CollectionAssert.IsEmpty(ran); }

[Test] public void Clear_ForgetsWhatsWaiting()
{ holding = true; queue.Add(() => ran.Add(1)); queue.Clear(); holding = false; queue.Release(); CollectionAssert.IsEmpty(ran); }
```

`OnlineOrdersNetworkTests`, the second test:
- Setup as `OnlineMatchNetworkTests.Joining_…`: the host is a bare session, and this machine joins with the game, in seat 2.
- The host loads the game, shows it, and sends `StartingCutscene`, `MainLoop`, `Tutorial` and `Begin`.
- `DropRecorder` on `host.Match`.
- The orders are the scene's order manager's `normalOrders` (`Reflect`) at indexes 0–3: `first`, `second`, `third`, `fourth`.
- Helpers:
  - `GlowLayer(order)`: the layer of the order's beacon's `beaconFX` (`Reflect`).
  - `CameraMask(seat)`: `Slot(seat).Input.GetComponent<PlayerCameraResizer>().PlayerReferenceCamera.cullingMask`.
  - `PutBallAt(seat, spot)`: that player's ball `Rigidbody` stopped and moved there (`velocity`, `position` and its transform).
  - `AnyActive(orders)`: whether any of them `IsActive`.

```csharp
[UnityTest]
public IEnumerator Joining_TheHostsOrdersShowHere_OnWhicheverScooterHoldsThem()
// Nothing spawns here by itself: wait 5 s (a spawn cooldown is 4 s)
Assert.IsFalse(AnyActive(orders), "this machine's order manager doesn't spawn");
// The host spawns one: it shows at its pickup
host.Match.SendOrder(OrderChange.Spawn(first.Key, true));             // wait until first.IsActive
Assert.Less(Vector3.Distance(first.PickupPoint.position, first.transform.position), 0.01f);
// This machine's scooter in its light picks up nothing by itself: the host decides
PutBallAt(1, first.transform.position);                                  // wait 1 s
Assert.IsNull(first.PlayerHolding);
// The host gives it to this machine's player: it rides on their scooter, and its dropoff glows for their camera
host.Match.SendOrder(OrderChange.Pickup(first.Key, 1));                 // wait until Handler(1).HasOrder
Assert.AreSame(Handler(1), first.PlayerHolding);
Assert.AreEqual(18, GlowLayer(first), "Player2Cam: player 2's own");
// The host's scores show here, and replaying a delivery doesn't add to them
PlayerInSeat(host, 1).ShareScore(150);                                   // wait until Handler(1).Score == 150
host.Match.SendOrder(OrderChange.Deliver(first.Key, 1));                // wait until !Handler(1).HasOrder, then 1 s
Assert.AreEqual(150, Handler(1).Score, "the host's score, not 150 plus the order");
host.Match.SendOrder(OrderChange.Erase(first.Key));                     // wait until !first.IsActive
// An order on the host's scooter rides on it here, and its dropoff doesn't glow for this machine's camera
host.Match.SendOrder(OrderChange.Spawn(second.Key, true));
host.Match.SendOrder(OrderChange.Pickup(second.Key, 0));                // wait until second.PlayerHolding == Handler(0)
Assert.AreEqual(0, CameraMask(1) & (1 << GlowLayer(second)), "not on this machine's camera");
// This machine's player falls in the water holding one: the host is asked, and drops it where asked
host.Match.SendOrder(OrderChange.Spawn(third.Key, true));
host.Match.SendOrder(OrderChange.Pickup(third.Key, 1));                 // wait until Handler(1).HasOrder
Vector3 spot = CitySpawns()[1].transform.position;
Handler(1).DropEverything(spot, spot, false);                            // as Respawn does; wait until asked.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Seat);
Assert.AreEqual(spot, asked.Spot1);
Assert.IsTrue(Handler(1).HasOrder, "the host drops it");
host.Match.SendOrder(OrderChange.Drop(1, OrderBook.NONE, spot, 5f, third.Key, spot, 5f, false)); // its second rack; wait until !HasOrder
// wait (Order's pickupCooldown is 3 s, so up to 10 s) until third lands
Assert.Less(Vector3.Distance(spot, third.transform.position), 0.1f, "landed where the host said");
// The golden order's value is the host's
host.Match.ShareGoldenValue(75);                                          // wait until OrderManager.Instance.FinalOrderValue == 75
// A change for an order this machine doesn't have does nothing
host.Match.SendOrder(OrderChange.Erase(123456));                          // wait 1 s; the log stays clean
// This machine's player holds an order when the host takes everyone back: it goes with the game scene
host.Match.SendOrder(OrderChange.Spawn(fourth.Key, true));
host.Match.SendOrder(OrderChange.Pickup(fourth.Key, 1));                // wait until Handler(1).HasOrder
int fourthKey = fourth.Key;
host.Match.RequestReturn();                                               // wait (60 s) until the menu scene is up
Assert.IsNull(Slot(1).Player.GetComponentInChildren<Order>(true), "no order rode into the menu");
host.Match.SendOrder(OrderChange.Erase(fourthKey));                       // the host's erase comes late: wait 1 s, the log stays clean
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "HostQueueTests|OnlineOrdersNetworkTests"`
Expected: `NO RESULTS` (`HostQueue` missing). Once it compiles, `Joining_…` fails: "the host spawns one" times out, since nothing replays changes yet.

- [ ] **Step 3: Implement `HostQueue`, the client side of `OnlineOrders`, the `OnlineGame` queue, and `ShowHostScore`.**

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "HostQueueTests|OnlineOrdersNetworkTests|OnlineMatchNetworkTests|OnlineSceneFlowTests"`
Expected: all pass: 5 + 2 + 4 + 14 = `tests: 25 total, 25 passed`.

- [ ] **Step 5: No commit.**

---

### Task 7: Docs and the full suite

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`, `EDITOR-TODO.md`

- [ ] **Step 1: `docs/online.md`**
  - A "Phase 3E: orders online" section, covering Rulings 1–5 and 7 in the doc's plain style: order keys, host decides and clients replay, `OrderSync`'s rule, drops, scores and the golden value, a leaver's orders.
  - Known limits:
    - Pickups show after the host sees the scooter there.
    - No steals or clashes online (Task 3.6).
    - The tutorial is still skipped (Phase 3F).

- [ ] **Step 2: `docs/testing.md`**
  - The eight new test classes.
  - The network tests on ports 7798 and 7799.
  - The game-scene tests take a minute or two each.

- [ ] **Step 3: The roadmap**
  - Progress: a Phase 3E line. "Next" becomes Phase 3F (the tutorial online), then 3.6–3.8.
  - Task 3.5:
    - Bullets 1–3 ticked, with notes: "done differently: one change stream keyed by `OrderBook`, see the Phase 3E plan's Rulings 1–4".
    - Bullet 4: "→ Phase 3F".
  - Task 3.3 bullet 1: "score: Phase 3E, on `OnlinePlayer`".
  - Task 3.8 bullet 1: "orders back to the pool: Phase 3E; avatar and placements already".
  - `RemoteAvatar`'s summary and `OrderBeacon.IsPlayersBall`'s summary stop saying "until Task 3.5".

- [ ] **Step 4: `EDITOR-TODO.md`: a two-editor check of orders**
  - Host in the main editor, join from the clone.
  - Both see the same orders.
  - The clone's scooter picks one up: it rides on it in both editors.
  - Deliver it: the same score in both HUDs.
  - Fall in the water holding one: it drops at the respawn point in both.
  - The golden round's value climbs in both.
  - Results match.
  - Then again with **Tools → Dead on Arrival → Online → Bad Connection**.

- [ ] **Step 5: Run the full suite** (in the background)

Run: `bash tools/run-tests.sh`
Expected: 381 + 43 new = `tests: 424 total, 424 passed`. The new ones: 8 in Task 1, 18 in Task 2, 7 in Task 3, 3 in Task 4, 1 in Task 5 and 6 in Task 6. The one exception: `OnlinePrefabsTests.SavedPrefabIds_…` stays red until **Save Network Prefab IDs** is run (`EDITOR-TODO.md` step 1), so 423 pass until then.

- [ ] **Step 6: No commit.** Suggested title: "Phase 3E: orders online".
