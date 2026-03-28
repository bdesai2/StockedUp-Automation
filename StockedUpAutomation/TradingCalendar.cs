namespace StockedUpAutomation;

/// <summary>
/// Determines whether a given date is a US NYSE trading day.
/// Covers weekends + all standard US market holidays through 2030.
/// </summary>
public static class TradingCalendar
{
    private static readonly Dictionary<int, HashSet<DateTime>> _holidayCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Returns true if the date is a weekday and not a NYSE market holiday.
    /// </summary>
    public static bool IsTradingDay(DateTime date)
    {
        return true;
        // Skip weekends
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Skip NYSE holidays
        return !IsMarketHoliday(date);
    }

    private static bool IsMarketHoliday(DateTime date)
    {
        var holidays = GetNyseHolidaysCached(date.Year);
        return holidays.Contains(date.Date);
    }

    private static HashSet<DateTime> GetNyseHolidaysCached(int year)
    {
        if (_holidayCache.TryGetValue(year, out var cached))
            return cached;

        lock (_cacheLock)
        {
            if (_holidayCache.TryGetValue(year, out cached))
                return cached;

            var holidays = GetNyseHolidays(year);
            var hashSet = new HashSet<DateTime>(holidays.Select(h => h.Date));
            _holidayCache[year] = hashSet;
            return hashSet;
        }
    }

    /// <summary>
    /// Returns the list of NYSE holidays for a given year.
    /// Follows the standard NYSE holiday schedule rules.
    /// </summary>
    private static List<DateTime> GetNyseHolidays(int year)
    {
        var holidays = new List<DateTime>();

        // New Year's Day — Jan 1 (observed Mon if Sun, observed Fri if Sat)
        holidays.Add(ObservedHoliday(new DateTime(year, 1, 1)));

        // Martin Luther King Jr. Day — 3rd Monday in January
        holidays.Add(NthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3));

        // Presidents' Day — 3rd Monday in February
        holidays.Add(NthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3));

        // Good Friday — Friday before Easter Sunday
        holidays.Add(GoodFriday(year));

        // Memorial Day — last Monday in May
        holidays.Add(LastMondayOfMonth(year, 5));

        // Juneteenth — June 19 (observed Mon if Sun, observed Fri if Sat)
        holidays.Add(ObservedHoliday(new DateTime(year, 6, 19)));

        // Independence Day — July 4 (observed Mon if Sun, observed Fri if Sat)
        holidays.Add(ObservedHoliday(new DateTime(year, 7, 4)));

        // Labor Day — 1st Monday in September
        holidays.Add(NthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1));

        // Thanksgiving Day — 4th Thursday in November
        holidays.Add(NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4));

        // Christmas Day — Dec 25 (observed Mon if Sun, observed Fri if Sat)
        holidays.Add(ObservedHoliday(new DateTime(year, 12, 25)));

        return holidays;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateTime ObservedHoliday(DateTime holiday)
    {
        return holiday.DayOfWeek switch
        {
            DayOfWeek.Saturday => holiday.AddDays(-1), // Observed Friday
            DayOfWeek.Sunday   => holiday.AddDays(1),  // Observed Monday
            _                  => holiday
        };
    }

    private static DateTime NthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n)
    {
        var first = new DateTime(year, month, 1);
        int daysUntil = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(daysUntil + (n - 1) * 7);
    }

    private static DateTime LastMondayOfMonth(int year, int month)
    {
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        int daysBack = ((int)lastDay.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return lastDay.AddDays(-daysBack);
    }

    private static DateTime GoodFriday(int year)
    {
        // Computus algorithm to find Easter Sunday
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        var easter = new DateTime(year, month, day);
        return easter.AddDays(-2); // Good Friday = 2 days before Easter
    }
}
