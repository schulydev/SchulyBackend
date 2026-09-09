using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Schuly.Infrastructure
{
    // Every DateTime column is `timestamp with time zone` and Npgsql only accepts
    // Kind=Utc values. Clients send either an offset (e.g. "+01:00", which parses to
    // Local) or a bare local time (e.g. "2026-03-01T08:00:00", which parses to
    // Unspecified). A Local value is a real instant, so it is converted to UTC; an
    // Unspecified value carries no offset, so it is taken to already be UTC rather
    // than being shifted by the server's own time zone.
    public static class UtcDateTime
    {
        public static DateTime Normalize(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        public static DateTime? Normalize(DateTime? value) => value.HasValue ? Normalize(value.Value) : null;
    }

    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(v => UtcDateTime.Normalize(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
    }

    public sealed class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public UtcNullableDateTimeConverter() : base(v => UtcDateTime.Normalize(v), v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null) { }
    }
}
