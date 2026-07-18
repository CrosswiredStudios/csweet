using CSweet.Domain.Core;

namespace CSweet.Infrastructure.Core;

public static class ExecutiveBriefingScheduleCalculator
{
    public static DateTimeOffset Next(DateTimeOffset now, ManagementCycle cycle)
    {
        var zone = ResolveZone(cycle.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var time = ParseTime(cycle.ExecutiveBriefingLocalTime, new TimeOnly(9, 0));
        var candidate = localNow.Date.Add(time.ToTimeSpan());
        if (candidate <= localNow.DateTime) candidate = candidate.AddDays(1);
        if (cycle.ExecutiveBriefingCadence.Equals("Weekdays", StringComparison.OrdinalIgnoreCase))
            while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) candidate = candidate.AddDays(1);
        else if (cycle.ExecutiveBriefingCadence.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
        {
            var day = Enum.TryParse<DayOfWeek>(cycle.ExecutiveBriefingWeeklyDay, true, out var parsed) ? parsed : DayOfWeek.Friday;
            while (candidate.DayOfWeek != day) candidate = candidate.AddDays(1);
        }
        if (zone.IsInvalidTime(candidate)) candidate = candidate.AddHours(1);
        var offset = zone.IsAmbiguousTime(candidate)
            ? zone.GetAmbiguousTimeOffsets(candidate).Max()
            : zone.GetUtcOffset(candidate);
        return new DateTimeOffset(candidate, offset).ToUniversalTime();
    }

    public static bool IsQuietHours(DateTimeOffset now, ManagementCycle cycle)
    {
        var zone = ResolveZone(cycle.TimeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone).TimeOfDay;
        var start = ParseTime(cycle.QuietHoursStart, new TimeOnly(18, 0)).ToTimeSpan();
        var end = ParseTime(cycle.QuietHoursEnd, new TimeOnly(8, 0)).ToTimeSpan();
        return start <= end ? local >= start && local < end : local >= start || local < end;
    }

    public static bool IsValidTime(string value) => TimeOnly.TryParseExact(value, "HH:mm", out _);

    public static bool IsValidTimeZone(string value)
    {
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }

    private static TimeOnly ParseTime(string value, TimeOnly fallback) =>
        TimeOnly.TryParseExact(value, "HH:mm", out var parsed) ? parsed : fallback;

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
