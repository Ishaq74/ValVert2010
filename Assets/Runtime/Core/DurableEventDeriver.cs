using System;
using UnityEngine;

namespace ValVertII.Core
{
    /// <summary>
    /// Derives the narrative-valued durable facts (new hour, new season quarter, rain starting or stopping) from the
    /// presentation events and publishes them, enveloped, on the single <see cref="ValVertEventBus"/>.
    /// Hosted by <see cref="PersistentRoot"/>: without a PersistentRoot (ValVert.unity opened alone) nothing is derived.
    /// The first observation of each fact only records state (no event), so a late start never fakes a change.
    /// Durable events are never fed back into presentation events: the game never drives the clock through the bus.
    /// </summary>
    public sealed class DurableEventDeriver : IDisposable
    {
        public const string Origin = "ValVertEventBus";
        public const float PrecipThreshold = 0.05f;

                private readonly Func<string> _sectorId;
        private IDisposable _timeSub, _seasonSub, _weatherSub;

        private bool _hasHour, _hasQuarter, _hasPrecip;
        private int _hourKey, _quarter;
        private bool _raining;

        public DurableEventDeriver(Func<string> sectorId = null)
        {
            _sectorId = sectorId ?? (() => string.Empty);

            _timeSub = ValVertEventBus.Subscribe<ValVertTimeChanged>(OnTime, replayLast: true);
            _seasonSub = ValVertEventBus.Subscribe<ValVertSeasonChanged>(OnSeason, replayLast: true);
            _weatherSub = ValVertEventBus.Subscribe<ValVertWeatherChanged>(OnWeather, replayLast: true);
        }

        private DurableEventEnvelope Envelope(string type, string key) =>
            new DurableEventEnvelope(type, 1, Origin, _sectorId(), key);

        private void OnTime(ValVertTimeChanged t)
        {
            int hour = Mathf.FloorToInt(t.Hour);
            int key = (t.Month * 100 + t.Day) * 100 + hour;

            if (!_hasHour) { _hasHour = true; _hourKey = key; return; }
            if (key == _hourKey) return;

            _hourKey = key;
            ValVertEventBus.Publish(new EnvironmentHourReached(
                Envelope(EnvironmentHourReached.TypeName, $"{EnvironmentHourReached.TypeName}/{t.Month:00}-{t.Day:00}-{hour:00}"),
                t.Month, t.Day, hour));
        }

        private void OnSeason(ValVertSeasonChanged s)
        {
            int q = Mathf.Clamp(Mathf.FloorToInt(s.YearFraction * 4f), 0, 3);

            if (!_hasQuarter) { _hasQuarter = true; _quarter = q; return; }
            if (q == _quarter) return;

            _quarter = q;
            ValVertEventBus.Publish(new SeasonQuarterChanged(
                Envelope(SeasonQuarterChanged.TypeName, $"{SeasonQuarterChanged.TypeName}/{q}"), q));
        }

        private void OnWeather(ValVertWeatherChanged w)
        {
            bool raining = w.Precip >= PrecipThreshold;

            if (!_hasPrecip) { _hasPrecip = true; _raining = raining; return; }
            if (raining == _raining) return;

            _raining = raining;
            ValVertEventBus.Publish(new PrecipitationToggled(
                Envelope(PrecipitationToggled.TypeName, $"{PrecipitationToggled.TypeName}/{(raining ? "start" : "stop")}"),
                raining));
        }

        public void Dispose()
        {
            _timeSub?.Dispose(); _seasonSub?.Dispose(); _weatherSub?.Dispose();
            _timeSub = _seasonSub = _weatherSub = null;
        }
    }
}
