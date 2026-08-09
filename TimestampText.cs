namespace LoonClipper;

internal static class TimestampText
{
    public static string Format(TimeSpan value) =>
        $"{value.Ticks / TimeSpan.TicksPerHour:00}:{value.Minutes:00}:{value.Seconds:00}";

    public static string FormatFile(TimeSpan value) =>
        Format(value).Replace(':', '-');
}
