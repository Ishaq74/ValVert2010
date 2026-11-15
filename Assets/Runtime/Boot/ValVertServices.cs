using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight service locator for Val Vert runtime systems (not a DI container).
/// Owner-safe: <see cref="Unregister{T}(T)"/> only removes the entry if it still points at the caller's
/// instance, so one system shutting down never evicts another system's registration.
/// Destroyed UnityEngine.Objects are treated as absent and purged lazily.
/// </summary>
public static class ValVertServices
{
    static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

    static bool Alive(object o) => o != null && !(o is UnityEngine.Object uo && uo == null);

    /// <summary>Register. If a different live instance is already registered: keep it and return false
    /// unless replace=true.</summary>
    public static bool Register<T>(T instance, bool replace = false) where T : class
    {
        if (!Alive(instance)) throw new ArgumentNullException(nameof(instance));
        if (_services.TryGetValue(typeof(T), out var cur) && Alive(cur) && !ReferenceEquals(cur, instance) && !replace)
            return false;
        _services[typeof(T)] = instance;
        return true;
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var o))
        {
            if (Alive(o) && o is T t) { service = t; return true; }
            _services.Remove(typeof(T));
        }
        service = null; return false;
    }

    public static T Get<T>() where T : class
    {
        if (TryGet<T>(out var s)) return s;
        throw new InvalidOperationException("Service not registered: " + typeof(T).Name);
    }

    /// <summary>Owner-safe removal: only removes when the registered instance is <paramref name="instance"/>
    /// (or is destroyed).</summary>
    public static bool Unregister<T>(T instance) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var cur) && (ReferenceEquals(cur, instance) || !Alive(cur)))
        { _services.Remove(typeof(T)); return true; }
        return false;
    }

    public static bool IsRegistered<T>() where T : class => TryGet<T>(out _);

    public static int Count
    {
        get { int n = 0; foreach (var v in _services.Values) if (Alive(v)) n++; return n; }
    }

    /// <summary>Full reset: domain/subsystem registration and tests ONLY.</summary>
    public static void ResetForDomain() => _services.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReset() => ResetForDomain();
}
