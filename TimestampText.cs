namespace LoonClipper;

internal static class TimestampText
{
    private const long MaximumSeconds = (999 * 3600L) + (59 * 60L) + 59;

    public static string Format(TimeSpan value) =>
        $"{value.Ticks / TimeSpan.TicksPerHour:00}:{value.Minutes:00}:{value.Seconds:00}";

    public static string FormatFile(TimeSpan value) =>
        Format(value).Replace(':', '-');

    public static bool TryParseTwitchOffset(string value, out TimeSpan timestamp)
    {
        timestamp = default;
        value = value.Trim().ToLowerInvariant();

        if (value.Length == 0)
        {
            return false;
        }

        long totalSeconds = 0;
        var position = 0;
        var previousUnit = -1;

        while (position < value.Length)
        {
            var numberStart = position;
            while (position < value.Length && char.IsAsciiDigit(value[position]))
            {
                position++;
            }

            if (numberStart == position
                || position >= value.Length
                || !long.TryParse(value[numberStart..position], out var number))
            {
                return false;
            }

            var unit = value[position++];
            var unitOrder = unit switch
            {
                'h' => 0,
                'm' => 1,
                's' => 2,
                _ => -1
            };

            if (unitOrder <= previousUnit)
            {
                return false;
            }

            var multiplier = unit switch
            {
                'h' => 3600L,
                'm' => 60L,
                's' => 1L,
                _ => 0L
            };

            if (multiplier == 0 || number > MaximumSeconds / multiplier)
            {
                return false;
            }

            totalSeconds += number * multiplier;
            if (totalSeconds > MaximumSeconds)
            {
                return false;
            }

            previousUnit = unitOrder;
        }

        timestamp = TimeSpan.FromSeconds(totalSeconds);
        return true;
    }
}
