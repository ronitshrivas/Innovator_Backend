namespace ElearningService.Common;

public static class DateFormat
{
    /// <summary>ISO-8601 UTC timestamp, e.g. 2026-01-15T09:30:00Z.</summary>
    public static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
