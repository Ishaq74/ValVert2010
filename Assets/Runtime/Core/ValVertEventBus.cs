using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal in-process, main-thread pub/sub for Val Vert runtime systems (not networked, not thread-safe).
///
/// Lifetime rules (single source of truth, see Docs/architecture-runtime.md):
///  * Subscribers own their subscriptions: subscribe in OnEnable, dispose/unsubscribe in OnDisable.
///  * Nobody but the domain reset (<see cref="ResetForDomain"/>) or a test may clear the whole bus.
///    In particular the bootstrap never wipes other systems' subscribers.
///  * The last published value of each event type is retained ("sticky") so a system enabled after the
///    bootstrap can read current state with <see cref="TryGetLast{T}"/>, or receive it immediately by
///    subscribing with replayLast=true.
///  * A throwing handler is logged and does not stop delivery to the remaining handlers.
/// </summary>
public static class ValVertEventBus
{
    static readonly Dictionary<Type, List<Delegate>> _map = new Dictionary<Type, List<Delegate>>();
    static readonly Dictionary<Type, object> _last = new Dictionary<Type, object>();
    static int _publishDepth;

    sealed class Subscription<T> : IDisposable
    {
        Action<T> _h;
        public Subscription(Action<T> h) { _h = h; }
        public void Dispose() { if (_h != null) { Unsubscribe(_h); _h = null; } }
    }

    /// <summary>Subscribe a handler. Duplicate registration of the same delegate is ignored.
    /// Returns a token whose Dispose() unsubscribes.</summary>
    public static IDisposable Subscribe<T>(Action<T> handler, bool replayLast = false)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var t = typeof(T);
        if (!_map.TryGetValue(t, out var list)) { list = new List<Delegate>(); _map[t] = list; }
        if (!list.Contains(handler)) list.Add(handler);
        if (replayLast && _last.TryGetValue(t, out var o)) Invoke(handler, (T)o);
        return new Subscription<T>(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;
        if (_map.TryGetValue(typeof(T), out var list)) list.Remove(handler);
    }

    /// <summary>Deliver to current subscribers in subscription order (snapshot: handlers added or removed
    /// during delivery take effect on the next publish).</summary>
    public static void Publish<T>(T evt)
    {
        _last[typeof(T)] = evt;
        if (!_map.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
        var snap = list.ToArray();
        _publishDepth++;
        try { for (int i = 0; i < snap.Length; i++) Invoke((Action<T>)snap[i], evt); }
        finally { _publishDepth--; }
    }

    static void Invoke<T>(Action<T> h, T evt)
    {
        // Skip handlers whose owning UnityEngine.Object was destroyed without unsubscribing.
        if (h.Target is UnityEngine.Object uo && uo == null) { Unsubscribe(h); return; }
        try { h(evt); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static bool TryGetLast<T>(out T value)
    {
        if (_last.TryGetValue(typeof(T), out var o)) { value = (T)o; return true; }
        value = default; return false;
    }

    public static int ListenerCount<T>()
        => _map.TryGetValue(typeof(T), out var list) ? list.Count : 0;

    public static int PublishDepth => _publishDepth;

    /// <summary>Full reset: domain/subsystem registration and tests ONLY.</summary>
    public static void ResetForDomain() { _map.Clear(); _last.Clear(); _publishDepth = 0; }

    // Enter Play Mode with domain reload disabled keeps statics alive: reset once per play session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReset() => ResetForDomain();
}

public readonly struct ValVertTimeChanged
{
    public readonly int Month, Day;
    public readonly float Hour, Daylight01, SunAltitudeDeg;
    public ValVertTimeChanged(int month, int day, float hour, float daylight01, float sunAltitudeDeg = 0f)
    { Month = month; Day = day; Hour = hour; Daylight01 = daylight01; SunAltitudeDeg = sunAltitudeDeg; }
}

public readonly struct ValVertSeasonChanged
{
    public readonly float YearFraction;
    /// <summary>Deciduous canopy amount 0..1, autumn colouring 0..1 (from ValVertSeasons when present, else -1).</summary>
    public readonly float LeafCover, Autumn;
    public ValVertSeasonChanged(float yearFraction, float leafCover = -1f, float autumn = -1f)
    { YearFraction = yearFraction; LeafCover = leafCover; Autumn = autumn; }
}

public readonly struct ValVertWeatherChanged
{
    public readonly float Cloud, Fog, Precip, Snow;
    public ValVertWeatherChanged(float cloud, float fog, float precip, float snow)
    { Cloud = cloud; Fog = fog; Precip = precip; Snow = snow; }
}

public readonly struct ValVertRegionActivationChanged
{
    public readonly string RegionId;
    public readonly bool Active;
    public readonly float Distance;
    public ValVertRegionActivationChanged(string id, bool active, float distance) { RegionId = id; Active = active; Distance = distance; }
}
