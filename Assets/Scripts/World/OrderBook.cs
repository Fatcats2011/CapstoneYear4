using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Each scene order's key, the same on every machine, and the loaded orders by key. Online, an order change names its
/// order by key (OrderChange). The key hashes the order's scene, value, pickup point and dropoff point (FNV-1a over their
/// exact bits): no two orders in a match scene share both points (OrderBookSceneTests). Orders add themselves as they
/// load and take themselves out when they're destroyed
/// </summary>
public static class OrderBook
{
    /// <summary>No order; never a key</summary>
    public const int NONE = 0;

    const uint FNV_BASIS = 2166136261;
    const uint FNV_PRIME = 16777619;

    static readonly Dictionary<int, Order> orders = new Dictionary<int, Order>();

    /// <summary>
    /// The key of the order with these points in this scene
    /// </summary>
    public static int KeyOf(string scene, Constants.OrderValue value, Vector3 pickup, Vector3 dropoff)
    {
        uint hash = FNV_BASIS;
        foreach (char c in scene)
        {
            hash = Mix(hash, (byte)c);
            hash = Mix(hash, (byte)(c >> 8));
        }
        hash = Mix(hash, (int)value);
        hash = Mix(hash, pickup);
        hash = Mix(hash, dropoff);

        int key = unchecked((int)hash);
        return key == NONE ? 1 : key;
    }

    /// <summary>
    /// An order's key: its scene's name, its value, and its pickup and dropoff points (zero when one isn't set)
    /// </summary>
    public static int KeyOf(Order order)
    {
        Transform pickup = order.PickupPoint, dropoff = order.DropoffPoint;
        return KeyOf(order.gameObject.scene.name, order.Value,
            pickup != null ? pickup.position : Vector3.zero, dropoff != null ? dropoff.position : Vector3.zero);
    }

    /// <summary>
    /// Adds an order under its key. Two live orders under one key is a scene error: the first stays, and the second
    /// isn't shared online
    /// </summary>
    public static void Add(Order order)
    {
        int key = order.Key;
        if (orders.TryGetValue(key, out Order there) && there != null && there != order)
        {
            Debug.LogError("Orders " + there.name + " and " + order.name + " share the key " + key + ": online, the second isn't shared");
            return;
        }
        orders[key] = order;
    }

    /// <summary>
    /// Takes an order out, when it's the one under its key
    /// </summary>
    public static void Remove(Order order)
    {
        if (orders.TryGetValue(order.Key, out Order there) && ReferenceEquals(there, order))
            orders.Remove(order.Key);
    }

    /// <summary>
    /// The order under a key, or null when there's none or it was destroyed (its scene unloaded)
    /// </summary>
    public static Order Find(int key)
    {
        return orders.TryGetValue(key, out Order order) && order != null ? order : null;
    }

    /// <summary>
    /// Forgets every order (tests)
    /// </summary>
    public static void Clear()
    {
        orders.Clear();
    }

    static uint Mix(uint hash, byte b)
    {
        return unchecked((hash ^ b) * FNV_PRIME);
    }

    // 4 bytes, little-endian
    static uint Mix(uint hash, int value)
    {
        for (int i = 0; i < 4; i++)
            hash = Mix(hash, (byte)(value >> (8 * i)));
        return hash;
    }

    static uint Mix(uint hash, Vector3 point)
    {
        hash = Mix(hash, BitConverter.SingleToInt32Bits(point.x));
        hash = Mix(hash, BitConverter.SingleToInt32Bits(point.y));
        return Mix(hash, BitConverter.SingleToInt32Bits(point.z));
    }
}
