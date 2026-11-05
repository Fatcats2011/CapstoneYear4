using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gold Rush's Mother Lode (GoldRushRules). The host (or this PC offline) sends one out 30 to 40 seconds after the last has
/// gone, and pays for the gold nuggets its carrier spills: whoever drives through one but the carrier who spilled it. Every
/// machine says when one is out and draws the nuggets, which travel online as order changes (OnlineOrders). The package
/// itself is an order (Order.IsLode); its pillar of light is ModeVisuals'. Made at launch; does nothing outside Gold Rush
/// </summary>
public class MotherLode : MonoBehaviour
{
    const float NUGGET_ABOVE = 0.8f;
    const float NUGGET_SCALE = 0.45f;
    const float NUGGET_SPIN = 140f;
    static readonly Color GOLD = new Color(1f, 0.74f, 0.1f, 1f);

    static MotherLode instance;

    class Nugget
    {
        public int Id, SpilledBy;
        public float Expires;
        public Vector3 Spot;
        public GameObject Shape;
    }

    readonly List<Nugget> nuggets = new List<Nugget>();
    int nextId;
    float nextLode = -1f;
    Order announced;
    OrderHandler spilling;
    float untouched;
    Vector3 lastSpill;
    AudioSource sounds;
    Mesh nuggetMesh;
    Material nuggetMaterial;
    float nuggetSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (instance != null)
            return;
        GameObject go = new GameObject("Mother Lode");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MotherLode>();
    }

    void Awake()
    {
        sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
    }

    /// <summary>Client: replays one of the host's nugget changes</summary>
    internal static void Show(OrderChange change)
    {
        if (instance == null)
            return;
        if (change.Kind == OrderChangeKind.Nugget)
            instance.Add(change.Order, change.Spot, change.Seat);
        else if (change.Kind == OrderChangeKind.NuggetTaken)
            instance.Take(change.Order, change.Seat);
    }

    /// <summary>Whether Gold Rush's waves are running here (every machine knows from its own clock and the host's state)</summary>
    static bool On(OrderManager orders)
    {
        GameManager game = GameManager.Instance;
        return GoldRushRules.Playing && orders != null && !orders.FinalOrderActive && game != null
            && (game.MainState == GameState.Begin || game.MainState == GameState.MainLoop);
    }

    void Update()
    {
        OrderManager orders = OrderManager.Instance;
        if (!On(orders))
        {
            if (nuggets.Count > 0)
                Clear();
            nextLode = -1f;
            spilling = null;
            announced = null;
            return;
        }

        Announce();
        Animate();

        if (GameAuthority.IsAuthority && orders.RoundRunning)
        {
            SendOut(orders);
            Spill();
            Collect();
        }
    }

    // The whole town hears about it
    void Announce()
    {
        Order lode = Order.CurrentLode;
        if (lode == announced)
            return;
        announced = lode;
        if (lode == null)
            return;
        if (ControllerPrompts.Instance != null)
            ControllerPrompts.Instance.ShowHint(Loc.Get("goldrush.lode"), 4f);
        Play("whoosh");
    }

    void SendOut(OrderManager orders)
    {
        if (Order.CurrentLode != null)
        {
            nextLode = -1f;
            bool free = Order.CurrentLode.PlayerHolding == null;
            untouched = free ? untouched + Time.deltaTime : 0f;
            if (untouched >= GoldRushRules.LODE_UNTOUCHED_LIMIT)
                Order.CurrentLode.EraseOrder();
            return;
        }
        untouched = 0f;
        if (nextLode < 0f)
            nextLode = Time.time + GoldRushRules.LodeWait(Random.value);
        if (Time.time >= nextLode && orders.SpawnLode())
            nextLode = -1f;
    }

    // Its carrier leaves a trail for whoever's chasing
    void Spill()
    {
        Order lode = Order.CurrentLode;
        OrderHandler carrier = lode != null ? lode.PlayerHolding : null;
        BallDriving ball = carrier != null ? carrier.GetComponent<BallDriving>() : null;
        if (ball == null || ball.Sphere == null)
        {
            spilling = null;
            return;
        }

        Vector3 at = ball.Sphere.transform.position;
        // A new carrier (a pickup, a steal) starts their own trail
        if (spilling != carrier)
        {
            spilling = carrier;
            lastSpill = at;
            return;
        }
        if (Vector3.Distance(at, lastSpill) < GoldRushRules.NUGGET_SPACING)
            return;

        lastSpill = at;
        int id = nextId++;
        int seat = OrderSync.SeatOf(carrier);
        using OrderSync.Scope change = OrderSync.Change(OrderChange.Nugget(id, at, seat));
        Add(id, at, seat);
    }

    void Collect()
    {
        if (PlayerInstantiate.Instance == null)
            return;
        for (int i = nuggets.Count - 1; i >= 0; i--)
        {
            Nugget nugget = nuggets[i];
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
            {
                OrderHandler handler = OrderSync.HandlerIn(slot.Index);
                BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
                if (ball == null || ball.Sphere == null
                    || !GoldRushRules.TakesNugget(slot.Index, nugget.SpilledBy, Vector3.Distance(ball.Sphere.transform.position, nugget.Spot)))
                    continue;

                handler.AddModePoints(GoldRushRules.NUGGET_WORTH);
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.UpdatePlacement();
                using (OrderSync.Change(OrderChange.NuggetTaken(nugget.Id, slot.Index)))
                    Take(nugget.Id, slot.Index);
                break;
            }
        }
    }

    void Animate()
    {
        for (int i = nuggets.Count - 1; i >= 0; i--)
        {
            Nugget nugget = nuggets[i];
            if (Time.time >= nugget.Expires)
            {
                Remove(i);
                continue;
            }
            if (nugget.Shape != null)
                nugget.Shape.transform.Rotate(0f, NUGGET_SPIN * Time.deltaTime, 0f, Space.World);
        }
    }

    void Add(int id, Vector3 spot, int spilledBy)
    {
        nuggets.Add(new Nugget
        {
            Id = id,
            SpilledBy = spilledBy,
            Spot = spot,
            Expires = Time.time + GoldRushRules.NUGGET_LIFE,
            Shape = MakeShape(spot),
        });
    }

    void Take(int id, int seat)
    {
        int index = nuggets.FindIndex(nugget => nugget.Id == id);
        if (index < 0)
            return;
        Remove(index);
        OrderHandler handler = OrderSync.HandlerIn(seat);
        if (handler != null && !RemoteAvatar.IsRemote(handler) && handler.GetComponent<BotDriver>() == null)
            Play("pickup");
    }

    void Remove(int index)
    {
        if (nuggets[index].Shape != null)
            Destroy(nuggets[index].Shape);
        nuggets.RemoveAt(index);
    }

    void Clear()
    {
        for (int i = nuggets.Count - 1; i >= 0; i--)
            Remove(i);
    }

    // A little golden package, as the scene's orders draw one
    GameObject MakeShape(Vector3 spot)
    {
        if (nuggetMesh == null)
        {
            Order order = ModeVisuals.AnyOrder();
            if (order == null || order.MeshOf(3) == null)
                return null;
            nuggetMesh = order.MeshOf(3);
            nuggetSize = order.meshScale[3] * NUGGET_SCALE;
            nuggetMaterial = ModeVisuals.Glowing(order.MaterialOf(3), GOLD, GOLD * 0.35f, "Gold Nugget");
        }

        GameObject root = new GameObject("Gold Nugget");
        DontDestroyOnLoad(root);
        root.transform.position = spot + Vector3.up * NUGGET_ABOVE;
        GameObject shape = new GameObject("Shape", typeof(MeshFilter), typeof(MeshRenderer));
        shape.transform.SetParent(root.transform, false);
        shape.transform.localScale = Vector3.one * nuggetSize;
        shape.transform.localPosition = -nuggetMesh.bounds.center * nuggetSize;
        shape.GetComponent<MeshFilter>().sharedMesh = nuggetMesh;
        if (nuggetMaterial != null)
            shape.GetComponent<MeshRenderer>().sharedMaterial = nuggetMaterial;
        return root;
    }

    void Play(string key)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(key, sounds);
    }
}
