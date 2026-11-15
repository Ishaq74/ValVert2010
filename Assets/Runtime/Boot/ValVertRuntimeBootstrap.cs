using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene runtime entry point. Exactly ONE bootstrap is primary at any time (others stay inert until the
/// primary goes away, then the oldest enabled candidate is promoted — covers additive/duplicate scenes and
/// single-scene reloads regardless of destroy/enable order).
///
/// The primary:
///  * finds the existing ValVertClock / ValVertSeasons / ValVertWeather (if the scene has them) and registers
///    them as services (owner-safe: it only ever unregisters what it registered itself);
///  * forwards clock/season/weather changes onto ValVertEventBus (consumers: ValVertStreetLights,
///    ValVertGrassSeasonal, QA smoke);
///  * creates/configures ValVertWorldStreaming (region activation) from ValVertWorldConfig.
/// It NEVER clears the bus or the service registry globally (that is ResetForDomain, domain/test only).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class ValVertRuntimeBootstrap : MonoBehaviour
{
    public ValVertWorldConfig config;
    public bool autoFindEnvSystems = true;
    public bool autoRegisterStreamingRegions = true;

    static readonly List<ValVertRuntimeBootstrap> _candidates = new List<ValVertRuntimeBootstrap>();
    public static ValVertRuntimeBootstrap Primary { get; private set; }
    public bool IsPrimary => Primary == this;

    ValVertClock _clock;
    ValVertSeasons _seasons;
    ValVertWeather _weather;
    ValVertWorldStreaming _streaming;
    readonly List<Func<bool>> _unregister = new List<Func<bool>>();

    float _lastYearFrac = float.NaN;
    float _lastCloud = float.NaN, _lastFog = float.NaN, _lastPrecip = float.NaN, _lastSnow = float.NaN;

    public int OwnedServiceCount => _unregister.Count;
    public ValVertWorldStreaming Streaming => _streaming;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReset() { _candidates.Clear(); Primary = null; }

    void OnEnable()
    {
        _candidates.RemoveAll(c => c == null);
        if (!_candidates.Contains(this)) _candidates.Add(this);
        if (Primary == null) Promote(this);
        else if (Primary != this)
            Debug.LogWarning($"[VV][Boot] secondary bootstrap '{name}' inert while '{Primary.name}' is primary");
    }

    void OnDisable()
    {
        _candidates.Remove(this);
        if (Primary != this) return;
        Shutdown();
        Primary = null;
        _candidates.RemoveAll(c => c == null || !c.isActiveAndEnabled);
        if (_candidates.Count > 0) Promote(_candidates[0]);
    }

    static void Promote(ValVertRuntimeBootstrap b) { Primary = b; b.Startup(); }

    void Startup()
    {
        _lastYearFrac = _lastCloud = _lastFog = _lastPrecip = _lastSnow = float.NaN;
        if (autoFindEnvSystems) FindSystems();
        Own(_clock); Own(_seasons); Own(_weather); Own(this);
        HookClock();
        if (autoRegisterStreamingRegions && (!config || config.enableRegionActivation)) EnsureStreaming();
    }

    void Shutdown()
    {
        UnhookClock();
        if (_streaming)
        {
            _streaming.RestoreAll();
            // Component stays (no destroy during teardown); disabled it is inert and re-enabled on next Startup.
            _streaming.enabled = false;
        }
        _streaming = null;
        foreach (var u in _unregister) u();
        _unregister.Clear();
    }

    void Own<T>(T svc) where T : UnityEngine.Object
    {
        if (!svc) return;
        if (ValVertServices.Register(svc)) _unregister.Add(() => ValVertServices.Unregister(svc));
    }

    void FindSystems()
    {
        _clock = FindFirstObjectByType<ValVertClock>();
        _seasons = FindFirstObjectByType<ValVertSeasons>();
        _weather = FindFirstObjectByType<ValVertWeather>();
    }

    void HookClock()
    {
        if (!_clock) return;
        _clock.Changed -= OnClockChanged;
        _clock.Changed += OnClockChanged;
        OnClockChanged(); // publish initial (sticky) state
    }

    void UnhookClock() { if (_clock != null) _clock.Changed -= OnClockChanged; }

    void OnClockChanged()
    {
        if (!_clock || !IsPrimary) return;
        ValVertEventBus.Publish(new ValVertTimeChanged(_clock.month, _clock.day, _clock.hour, _clock.Daylight01, _clock.SunAltitudeDeg));
        float yf = _clock.YearFraction;
        if (float.IsNaN(_lastYearFrac) || Mathf.Abs(yf - _lastYearFrac) > 0.0005f)
        {
            _lastYearFrac = yf;
            if (_seasons)
            {
                // computed from the date directly (ValVertSeasons.Tick may not have run yet this frame: no stale values)
                float doy = (_seasons.yearFractionOverride >= 0 ? _seasons.yearFractionOverride : yf) * 365f;
                ValVertSeasons.Phenology(doy, out var leaf, out var aut, out _, out _);
                ValVertEventBus.Publish(new ValVertSeasonChanged(yf, leaf, aut));
            }
            else ValVertEventBus.Publish(new ValVertSeasonChanged(yf));
        }
        PublishWeatherIfChanged();
    }

    void LateUpdate() { if (IsPrimary) PublishWeatherIfChanged(); }

    void PublishWeatherIfChanged()
    {
        if (!_clock) return;
        float cloud = _clock.cloudCover, fog = _clock.fogAmount, precip = _clock.precip;
        float snow = _weather ? _weather.SnowCover : 0f;
        if (float.IsNaN(_lastCloud) || cloud != _lastCloud || fog != _lastFog || precip != _lastPrecip || snow != _lastSnow)
        {
            _lastCloud = cloud; _lastFog = fog; _lastPrecip = precip; _lastSnow = snow;
            ValVertEventBus.Publish(new ValVertWeatherChanged(cloud, fog, precip, snow));
        }
    }

    void EnsureStreaming()
    {
        _streaming = GetComponent<ValVertWorldStreaming>();
        if (!_streaming) _streaming = gameObject.AddComponent<ValVertWorldStreaming>();
        _streaming.enabled = true;
        if (!_streaming.viewer)
        {
            var fps = FindFirstObjectByType<SimpleFPS>();
            if (fps) _streaming.viewer = fps.transform;
            else if (Camera.main) _streaming.viewer = Camera.main.transform;
        }
        if (!config) return;   // no config: no regions (never guess)
        _streaming.updateInterval = config.updateInterval;
        _streaming.minHysteresis = config.minHysteresis;
        _streaming.safetyMargin = config.safetyMargin;
        foreach (var spec in config.regions)
        {
            if (spec == null || string.IsNullOrEmpty(spec.objectName)) continue;
            var go = GameObject.Find(spec.objectName);
            if (!go) continue;
            float load = spec.loadRadius > 0 ? spec.loadRadius : config.streamLoadRadius;
            float unload = spec.unloadRadius > 0 ? spec.unloadRadius : config.streamUnloadRadius;
            bool always = spec.policy == ValVertWorldStreaming.Policy.AlwaysActive;
            if (spec.perChild && !always) _streaming.RegisterChildren(spec.id, go.transform, load, unload);
            else _streaming.Register(spec.id, go.transform, load, unload, always);
        }
    }

    /// <summary>Editor/wire entry: ensure bootstrap exists on host (components/config only).</summary>
    public static ValVertRuntimeBootstrap EnsureOn(GameObject host, ValVertWorldConfig cfg = null)
    {
        var b = host.GetComponent<ValVertRuntimeBootstrap>();
        if (!b) b = FindFirstObjectByType<ValVertRuntimeBootstrap>(FindObjectsInactive.Include);   // one bootstrap per scene, never a second one
        if (!b) b = host.AddComponent<ValVertRuntimeBootstrap>();
        if (cfg) b.config = cfg;
        b.autoFindEnvSystems = true;
        b.autoRegisterStreamingRegions = true;
        return b;
    }
}
