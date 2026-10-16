using System;
using System.Globalization;

namespace SuperDev.Services;

public static class ReminderText
{
    public static string ButtonLabel(DateTime? utc, int everyMinutes, bool silent = false)
    {
        if (utc is not { } value)
            return "Set reminder";

        var when = Clock(value);
        var label = everyMinutes > 0
            ? $"Reminder: {when} · {Interval(everyMinutes)}"
            : $"Reminder: {when}";
        return silent ? $"{label} · Silent" : label;
    }

    public static string Clock(DateTime utc) =>
        utc.ToLocalTime().ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    public static string Interval(int minutes)
    {
        if (minutes % 1440 == 0)
        {
            var days = minutes / 1440;
            return days == 1 ? "every day" : $"every {days} days";
        }

        if (minutes % 60 == 0)
        {
            var hours = minutes / 60;
            return hours == 1 ? "every hour" : $"every {hours} hours";
        }

        return minutes == 1 ? "every minute" : $"every {minutes} minutes";
    }

    public static string NoticeDetail(int everyMinutes) =>
        everyMinutes > 0
            ? char.ToUpperInvariant(Interval(everyMinutes)[0]) + Interval(everyMinutes)[1..]
            : "One-time reminder";
}
