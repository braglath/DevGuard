using System;
using System.Globalization;

namespace SuperDev.Models;

public static class DeadlineValue
{
    public static string ButtonLabel(DateOnly? deadline, int? minuteOfDay, int? minutesBefore, bool silent = false)
    {
        if (deadline is not { } date)
            return "Pick deadline";

        var text = $"Deadline: {Format(date, minuteOfDay)}";
        var notify = NotifyText(minutesBefore);
        if (notify.Length > 0)
            text = $"{text} · {notify}";
        return silent ? $"{text} · Silent" : text;
    }

    public static bool IsPast(DateOnly deadline, int? minuteOfDay) =>
        Instant(deadline, minuteOfDay) <= DateTime.UtcNow;

    public static string Format(DateOnly deadline) =>
        Format(deadline, minuteOfDay: null);

    public static string Format(DateOnly deadline, int? minuteOfDay)
    {
        var day = deadline.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        if (minuteOfDay is not int minute)
            return day;

        return $"{day}, {Clock(minute)}";
    }

    public static string Clock(int minuteOfDay)
    {
        var time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(NormalizeMinute(minuteOfDay)));
        return time.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }

    public static string NotifyText(int? minutesBefore)
    {
        if (minutesBefore is null)
            return string.Empty;

        if (minutesBefore <= 0)
            return "At the deadline";

        return $"{Amount(minutesBefore.Value)} before";
    }

    public static DateTime Instant(DateOnly deadline, int? minuteOfDay)
    {
        var minute = minuteOfDay is int value ? NormalizeMinute(value) : (23 * 60) + 59;
        var local = deadline.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute)), DateTimeKind.Local);
        return local.ToUniversalTime();
    }

    public static DateOnly FromCalendar(DateTime selected) =>
        DateOnly.FromDateTime(selected);

    public static DateTime ToCalendar(DateOnly deadline) =>
        deadline.ToDateTime(TimeOnly.MinValue);

    public static void SplitClock(int minuteOfDay, out int hour12, out int minute, out bool pm)
    {
        minuteOfDay = NormalizeMinute(minuteOfDay);
        var hour = minuteOfDay / 60;
        minute = minuteOfDay % 60;
        pm = hour >= 12;
        hour12 = hour % 12;
        if (hour12 == 0)
            hour12 = 12;
    }

    public static bool TryClock(string? hourText, string? minuteText, bool pm, out int minuteOfDay, out string hourLabel, out bool usePm, out string error)
    {
        minuteOfDay = 9 * 60;
        hourLabel = "9";
        usePm = pm;
        error = string.Empty;

        if (!int.TryParse(hourText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hour))
        {
            error = "Enter an hour from 1 to 12, or 00 to 23.";
            return false;
        }

        if (!int.TryParse(minuteText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
            minute is < 0 or > 59)
        {
            error = "Enter minutes from 00 to 59.";
            return false;
        }

        int hour24;
        if (hour is >= 0 and <= 23 && (hour > 12 || hour == 0))
        {
            hour24 = hour;
            usePm = hour24 >= 12;
        }
        else if (hour is >= 1 and <= 12)
        {
            hour24 = hour % 12;
            if (pm)
                hour24 += 12;
            usePm = pm;
        }
        else
        {
            error = "Enter an hour from 1 to 12, or 00 to 23.";
            return false;
        }

        minuteOfDay = (hour24 * 60) + minute;
        var shown = hour24 % 12;
        if (shown == 0)
            shown = 12;
        hourLabel = shown.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    public static bool TryLead(string? countText, int unitIndex, out int minutes, out string error)
    {
        minutes = 0;
        error = string.Empty;
        if (!int.TryParse(countText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) ||
            count is < 1 or > 999)
        {
            error = "Enter how long before the deadline, from 1 to 999.";
            return false;
        }

        minutes = unitIndex == 1 ? count * 60 : count;
        return true;
    }

    public static void SplitLead(int minutes, out int count, out int unitIndex)
    {
        if (minutes >= 60 && minutes % 60 == 0)
        {
            count = minutes / 60;
            unitIndex = 1;
            return;
        }

        count = Math.Max(1, minutes);
        unitIndex = 0;
    }

    private static string Amount(int minutes)
    {
        if (minutes % 1440 == 0)
        {
            var days = minutes / 1440;
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }

    private static int NormalizeMinute(int minuteOfDay)
    {
        if (minuteOfDay < 0)
            return 0;
        if (minuteOfDay > (23 * 60) + 59)
            return (23 * 60) + 59;
        return minuteOfDay;
    }
}
