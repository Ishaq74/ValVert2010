using System;

namespace ValVertII.Core
{
    /// <summary>
    /// Common envelope of every durable event published on <see cref="ValVertEventBus"/> (ARCHITECTURE.md §7.1):
    /// unique event id, versioned type, origin, sector, and an idempotency key so that replaying a load never adds
    /// the same consequence twice.
    /// </summary>
    public readonly struct DurableEventEnvelope
    {
        public readonly string EventId;
        public readonly string Type;
        public readonly int Version;
        public readonly string Origin;
        public readonly string SectorId;
        public readonly string IdempotencyKey;

        public DurableEventEnvelope(string type, int version, string origin, string sectorId, string idempotencyKey)
        {
            EventId = Guid.NewGuid().ToString("N");
            Type = type;
            Version = version;
            Origin = origin;
            SectorId = sectorId ?? string.Empty;
            IdempotencyKey = idempotencyKey;
        }
    }

    /// <summary>The in-game clock crossed into a new hour.</summary>
    public readonly struct EnvironmentHourReached
    {
        public const string TypeName = "environment.hour";
        public readonly DurableEventEnvelope Envelope;
        public readonly int Month, Day, Hour;

        public EnvironmentHourReached(DurableEventEnvelope envelope, int month, int day, int hour)
        { Envelope = envelope; Month = month; Day = day; Hour = hour; }
    }

    /// <summary>The year crossed into another quarter (0..3, from 1 January).</summary>
    public readonly struct SeasonQuarterChanged
    {
        public const string TypeName = "environment.season";
        public readonly DurableEventEnvelope Envelope;
        public readonly int Quarter;

        public SeasonQuarterChanged(DurableEventEnvelope envelope, int quarter)
        { Envelope = envelope; Quarter = quarter; }
    }

    /// <summary>Precipitation started or stopped.</summary>
    public readonly struct PrecipitationToggled
    {
        public const string TypeName = "environment.precipitation";
        public readonly DurableEventEnvelope Envelope;
        public readonly bool Started;

        public PrecipitationToggled(DurableEventEnvelope envelope, bool started)
        { Envelope = envelope; Started = started; }
    }

    /// <summary>A world session started (published by <see cref="PersistentRoot.EnterSessionAsync"/>).</summary>
    public readonly struct WorldStartedEvent
    {
        public readonly string StartingSectorId;

        public WorldStartedEvent(string startingSectorId)
        {
            StartingSectorId = startingSectorId;
        }
    }
}
