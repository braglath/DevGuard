using System;
using System.Globalization;

namespace SuperDev.Models;

public static class DeadlineValue
{
    public static string Format(DateOnly deadline) =>
        deadline.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static DateOnly FromCalendar(DateTime selected) =>
        DateOnly.FromDateTime(selected);

    public static DateTime ToCalendar(DateOnly deadline) =>
        deadline.ToDateTime(TimeOnly.MinValue);
}
